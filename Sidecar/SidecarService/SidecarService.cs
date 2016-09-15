using System;
using System.Collections.Generic;
using System.Fabric;
using System.Fabric.Health;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;

namespace SidecarService
{
	/// <summary>
	/// The FabricRuntime creates an instance of this class for each service type instance. 
	/// </summary>
	internal sealed class SidecarService : StatelessService
	{
		private readonly HttpClient httpClient = new HttpClient();
		private Task[] tasks;

		public SidecarService(StatelessServiceContext context)
			: base(context)
		{ }

		/// <summary>
		/// Optional override to create listeners (like tcp, http) for this service instance.
		/// </summary>
		/// <returns>The collection of listeners.</returns>
		protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
		{
			return new ServiceInstanceListener[]
			{
				new ServiceInstanceListener(
					serviceContext =>
						new OwinCommunicationListener(Startup.ConfigureApp, serviceContext, ServiceEventSource.Current,
							"ServiceEndpoint"))
			};
		}

		protected override Task OnCloseAsync(CancellationToken cancellationToken)
		{
			Task.WaitAll(tasks);
			foreach (var task in tasks)
			{
				task.Dispose();
			}

			httpClient.Dispose();

			return base.OnCloseAsync(cancellationToken);
		}

		protected override Task OnOpenAsync(CancellationToken cancellationToken)
		{
			var configurationPackage = Context.CodePackageActivationContext.GetConfigurationPackageObject("Config");
			string doHealthCheckValue = configurationPackage.Settings.Sections["MyConfigSection"].Parameters["Sidecar_DoHealthCheck"].Value;

			bool doHealthCheck;
			if (bool.TryParse(doHealthCheckValue, out doHealthCheck) && doHealthCheck)
			{
				string serviceEndpointsValue =
					configurationPackage.Settings.Sections["MyConfigSection"].Parameters["Sidecar_Endpoints"].Value;

				if (!string.IsNullOrWhiteSpace(serviceEndpointsValue))
				{
					tasks = serviceEndpointsValue
						.Split(',')
						.Select(
							serviceEndpoint => Task.Run(() => { ReportHealth(serviceEndpoint, cancellationToken); }, cancellationToken))
						.ToArray();
				}
			}

			return base.OnOpenAsync(cancellationToken);
		}

		private async void ReportHealth(string serviceEndpoint, CancellationToken token)
		{
			while (Partition == null)
			{
				if (token.IsCancellationRequested)
					token.ThrowIfCancellationRequested();

				await Task.Delay(TimeSpan.FromSeconds(1));
			}

			while (true)
			{
				if (token.IsCancellationRequested)
					token.ThrowIfCancellationRequested();

				HealthState healthState;
				using (var msg = new HttpRequestMessage(HttpMethod.Get, new Uri($"{serviceEndpoint}/Health")))
				{
					try
					{
						HttpResponseMessage response = await httpClient.SendAsync(msg);
						response.EnsureSuccessStatusCode();
						healthState = HealthState.Ok;
					}
					catch (Exception)
					{
						healthState = HealthState.Error;
					}
				}

				var healthInformation = new HealthInformation(serviceEndpoint, "Health", healthState);
				Partition.ReportInstanceHealth(healthInformation);

				await Task.Delay(TimeSpan.FromSeconds(1));
			}
		}
	}
}
