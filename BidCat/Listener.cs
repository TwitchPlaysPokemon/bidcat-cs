using ApiListener;
using System;

namespace BidCat
{
	public static class Listener
	{
		public static Config Config { get; set; } = Config.DefaultConfig;

		public static void Start() => ApiListener.ApiListener.Start(Config.HttpPort);

		public static void Stop() => ApiListener.ApiListener.Stop();

		public static string CallEndpoint(string name, string[] args) => ApiListener.ApiListener.Commands[name].Function(args);

		public static void AttachLogger(Action<ApiLogMessage> logger) => ApiListener.ApiListener.AttachLogger(logger);
	}
}
