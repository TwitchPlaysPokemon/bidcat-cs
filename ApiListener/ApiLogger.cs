namespace ApiListener
{
	public static class ApiLogger
	{
		public static ApiLogMessage Debug(string message) => new ApiLogMessage(message, ApiLogLevel.Debug);
		public static ApiLogMessage Info(string message) => new ApiLogMessage(message, ApiLogLevel.Info);
		public static ApiLogMessage Warning(string message) => new ApiLogMessage(message, ApiLogLevel.Warning);
		public static ApiLogMessage Error(string message) => new ApiLogMessage(message, ApiLogLevel.Error);
		public static ApiLogMessage Critical(string message) => new ApiLogMessage(message, ApiLogLevel.Critical);
	}

	public struct ApiLogMessage
	{
		public string Message;
		public ApiLogLevel Level;

		public ApiLogMessage(string message, ApiLogLevel level)
		{
			Message = message;
			Level = level;
		}
	}

	public enum ApiLogLevel
	{
		Debug = 0,
		Info = 1,
		Warning = 2,
		Error = 4,
		Critical = 8
	}
}
