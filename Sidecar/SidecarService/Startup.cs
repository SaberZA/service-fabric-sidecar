using System.Fabric;
using System.Web.Http;
using Owin;

namespace SidecarService
{
	public static class Startup
	{
		// This code configures Web API. The Startup class is specified as a type
		// parameter in the WebApp.Start method.
		public static void ConfigureApp(IAppBuilder appBuilder, ServiceContext context)
		{
			// Configure Web API for self-host. 
			HttpConfiguration config = new HttpConfiguration();

			config.MessageHandlers.Add(new ProxyHandler(context));
			config.Routes.MapHttpRoute("SidecarService", "{*path}");

			appBuilder.UseWebApi(config);
		}
	}
}
