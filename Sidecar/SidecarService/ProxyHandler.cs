using System;
using System.Fabric;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SidecarService
{
	/// <summary>
	/// Inspired by http://kasperholdum.dk/2016/03/reverse-proxy-in-asp-net-web-api/
	/// </summary>
	internal class ProxyHandler : DelegatingHandler
	{
		private readonly string[] serviceEndpoints;
		private readonly Random random;
		private readonly HttpClient httpClient = new HttpClient();

		public ProxyHandler(ServiceContext context) : base()
		{
			var configurationPackage = context.CodePackageActivationContext.GetConfigurationPackageObject("Config");
			string serviceEndpointsValue =
				configurationPackage.Settings.Sections["MyConfigSection"].Parameters["Sidecar_Endpoints"].Value;

			if (!string.IsNullOrWhiteSpace(serviceEndpointsValue))
			{
				serviceEndpoints = serviceEndpointsValue.Split(',');
			}

			random = new Random();
		}

		private async Task<HttpResponseMessage> RedirectRequest(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			if (serviceEndpoints == null)
				return new HttpResponseMessage(HttpStatusCode.NotFound);

			var redirectLocation = serviceEndpoints[random.Next(0, serviceEndpoints.Length)];
			var localPath = request.RequestUri.LocalPath;

			using (var clonedRequest = await HttpRequestMessageExtensions.CloneHttpRequestMessageAsync(request))
			{
				clonedRequest.RequestUri = new Uri(redirectLocation + localPath);
				return await httpClient.SendAsync(clonedRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
			}
		}

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			return await RedirectRequest(request, cancellationToken);
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				httpClient.Dispose();
			}

			base.Dispose(disposing);
		}
	}
}