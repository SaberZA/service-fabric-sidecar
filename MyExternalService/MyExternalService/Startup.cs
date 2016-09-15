using System.Web.Http;
using Owin;

namespace MyExternalService
{
	public class Startup
	{
		// This code configures Web API. The Startup class is specified as a type
		// parameter in the WebApp.Start method.
		public void Configuration(IAppBuilder appBuilder)
		{
			// Configure Web API for self-host. 
			HttpConfiguration config = new HttpConfiguration();

			//config.MapHttpAttributeRoutes();
			config.Routes.MapHttpRoute(
				name: "Default",
				routeTemplate: "{controller}",
				defaults: new { id = RouteParameter.Optional }
			);

			appBuilder.UseWebApi(config);
		}
	}
}