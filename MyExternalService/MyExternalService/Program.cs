using System;
using System.ComponentModel;
using System.Linq;
using Microsoft.Owin.Hosting;

namespace MyExternalService
{
	public class Program
	{
		public static int Port;
		public static string BaseAddress;

		static void Main(string[] args)
		{
			if (args.Length != 1 || !int.TryParse(args[0], out Port))
			{
				Console.WriteLine("Usage: MyExternalService.exe [port number]");
				return;
			}

			BaseAddress = $"http://localhost:{Port}";

			// Start OWIN host 
			WebApp.Start<Startup>(BaseAddress);

			Console.WriteLine($"MyExternalService is running on {BaseAddress}");
			Console.WriteLine("Press 'enter' to shut down.");
			Console.ReadLine();
		}
	}
}