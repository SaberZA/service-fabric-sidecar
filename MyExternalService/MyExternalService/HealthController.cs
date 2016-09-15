using System;
using System.Web.Http;

namespace MyExternalService
{
	public class HealthController : ApiController
	{
		public bool Get()
		{
			Console.WriteLine("Health request received");

			return true;
		}
	}
}