using System.ServiceProcess;

namespace ServiceWrapper
{
	static class Program
	{
		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		static void Main()
		{
			ServiceBase[] ServicesToRun;
			ServicesToRun = new ServiceBase[]
			{
				new BidCatService()
			};
			ServiceBase.Run(ServicesToRun);
		}
	}
}
