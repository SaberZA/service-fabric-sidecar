using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Web.Http;

namespace MyExternalService
{
	public class PingController : ApiController
	{
		public string Get()
		{
			var hostName = new Uri(Program.BaseAddress).Host;
			IPAddress[] ipAddresses = Dns.GetHostAddresses(hostName);
			var ip = ipAddresses.First(x => x.AddressFamily == AddressFamily.InterNetwork);

			Console.WriteLine("Ping request received");

			return $"Endpoint: {ip}:{Program.Port}";
		}
	}
}