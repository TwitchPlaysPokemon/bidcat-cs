using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace BidCat
{
	public class Config
	{
		public int HttpPort { get; set; }
		public string LogDir { get; set; }
		[JsonProperty("StorageType", ItemConverterType = typeof(StringEnumConverter))]
		public StorageType StorageType { get; set; }

		public MongoDbConnectionSettings MongoSettings { get; set; }

		public static Config DefaultConfig => new Config
		{
			HttpPort = 7338,
			LogDir = "C:\\Temp",
			StorageType = StorageType.Memory
		};

		public class MongoDbConnectionSettings
		{
			public string Database { get; set; }
			public string Host { get; set; }
			public string ApplicationName { get; set; } = null;
			public string Username { get; set; } = null;
			public string Password { get; set; } = null;
			public int? Port { get; set; } = null;
		}
	}

	public enum StorageType
	{
		Memory,
		Mongo,
		Postgres
	}
}
