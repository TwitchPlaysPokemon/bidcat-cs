using ApiListener;
using BidCat;
using Newtonsoft.Json;
using System;
using System.IO;
using System.ServiceProcess;

namespace ServiceWrapper
{
	public partial class BidCatService : ServiceBase
	{
		public BidCatService()
		{
			InitializeComponent();
		}

		const ApiLogLevel displayLogLevel = ApiLogLevel.Debug;
		private FileStream logStream = null;
		private StreamWriter logWriter = null;

		protected override void OnStart(string[] args)
		{
			string configJson = File.ReadAllText("BidCatConfig.json");
			try
			{
				Listener.Config = JsonConvert.DeserializeObject<Config>(configJson);
				if (!string.IsNullOrWhiteSpace(Listener.Config.LogDir) &&
					!File.Exists(Path.Combine(Listener.Config.LogDir, "BidCatLog.txt")))
				{
					logStream = File.Create(Path.Combine(Listener.Config.LogDir, "BidCatLog.txt"));
					logWriter = new StreamWriter(logStream);
				}
				else if (!string.IsNullOrWhiteSpace(Listener.Config.LogDir))
				{
					logStream = File.OpenWrite(Path.Combine(Listener.Config.LogDir, "BidCatLog.txt"));
					logWriter = new StreamWriter(logStream);
				}
			}
			catch
			{
				Listener.Config = Config.DefaultConfig;
			}

			Listener.AttachLogger(m =>
			{
				if (m.Level >= displayLogLevel)
				{
					if (logStream != null)
					{
						logWriter?.WriteLine($"{Enum.GetName(typeof(ApiLogLevel), m.Level)?.ToUpper()}: {m.Message}");
						logWriter?.Flush();
					}
				}
			});
			Listener.Start();
		}

		protected override void OnStop()
		{
			Listener.Stop();
			logWriter.Dispose();
			logStream.Dispose();
		}
	}
}
