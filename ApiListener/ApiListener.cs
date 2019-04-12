using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using static ApiListener.ApiLogger;

namespace ApiListener
{
	public static class ApiListener
	{
		private static HttpListener HttpListener = new HttpListener();

		public static void Start(int port)
		{
			if (!HttpListener.IsSupported)
			{
				throw new Exception("HTTPListener is not supported on this system.");
			}
			if (Commands == null)
			{
				Init();
			}
			var listenTo = $"http://localhost:{port}/";
			if (!HttpListener.Prefixes.Contains(listenTo))
			{
				HttpListener.Prefixes.Add(listenTo);
			}
			if (!HttpListener.IsListening)
			{
				HttpListener.Start();
				Listen();
				Log(Info($"Listening for HTTP connections on {listenTo}"));
			}
		}

		public static void Stop()
		{
			Log(Info("Stopping."));
			HttpListener.Stop();
		}

		public static void HardStop()
		{
			Log(Info("Abandoning all connections and stopping."));
			HttpListener.Abort();
		}

		private static void Listen() => HttpListener.BeginGetContext(HttpListenHandler, HttpListener);

		private static void HttpListenHandler(IAsyncResult result)
		{
			HttpListenerContext context = null;
			try
			{
				context = HttpListener.EndGetContext(result);

				string body = null;
				if (context.Request.HasEntityBody)
				{
					using (Stream bodyStream = context.Request.InputStream)
					{
						using (StreamReader reader = new StreamReader(bodyStream, context.Request.ContentEncoding))
						{
							body = reader.ReadToEnd();
						}
					}
				}

				byte[] responseBuffer;

				var response = "ok";
				string command = "Unknown Command";
				try
				{
					var urlParams = new List<string>(context.Request.RawUrl.Split(new char[] { '/' })).Select(us => Uri.UnescapeDataString(us)).ToList();
					if (!string.IsNullOrWhiteSpace(body))
					{
						urlParams.Add(body);
					}
					if (string.IsNullOrWhiteSpace(urlParams.FirstOrDefault()))
					{
						urlParams.RemoveAt(0);
					}
					if (string.IsNullOrWhiteSpace(urlParams.LastOrDefault()) && urlParams.Any())
					{
						urlParams.RemoveAt(urlParams.Count - 1);
					}

					if (!urlParams.Any())
					{
						response = Documentation.Function(null);
					}
					else if (urlParams[0] == "favicon.ico")
					{
						context.Response.StatusCode = 404;
						response = "Not found.";
					}
					else
					{
						command = urlParams[0];
						urlParams.RemoveAt(0);

						if (!Commands.ContainsKey(command))
						{
							throw new ApiError($"Invalid Command \"{command}\"");
						}

						command = Commands[command]?.Name; //normalize name for display during errors
						response = Commands[command].Function(urlParams) ?? response;
					}
				}
				catch (Exception e)
				{
					response = $"{command}: {e.Message}";
					context.Response.StatusCode = e is ApiError ? 400 : 500;
				}
				responseBuffer = Encoding.UTF8.GetBytes(response);
				context.Response.ContentType = "application/json; charset=utf-8";
				// Get a response stream and write the response to it.
				context.Response.ContentLength64 = responseBuffer.Length;
				using (Stream output = context.Response.OutputStream)
				{
					output.Write(responseBuffer, 0, responseBuffer.Length);
					output.Close();
				}
				Log(Debug($"{context.Request.RawUrl} => {context.Response.StatusCode}: {response}"));
			}
			catch (Exception e)
			{
				Log(Critical($"{context?.Request?.RawUrl ?? "(Error getting accessed URL)"} => {e.Message}"));
			}
			finally
			{
				Listen();
			}
		}

		private static IEnumerable<ApiProvider> apiProviders;

		public static Dictionary<string, ApiCommand> Commands;

		private static ApiCommand Documentation;

		private static List<Action<ApiLogMessage>> LogHandlers = new List<Action<ApiLogMessage>>();

		private static Action<ApiLogMessage> Log = message => LogHandlers.ForEach(handler => handler(message));

		public static void AttachLogger(Action<ApiLogMessage> logger) => LogHandlers.Add(logger);

		public static void Init()
		{
			apiProviders = ReflectiveEnumerator.GetEnumerableOfType<ApiProvider>(Log);
			Commands = new Dictionary<string, ApiCommand>(StringComparer.OrdinalIgnoreCase);
			Documentation = new ApiCommand("Help", args => string.Join("\n", Commands.Select(c => c.Value.BuildDocString())), new List<ApiParameter>(), "Lists available commands (This message)");
			Commands["Help"] = Documentation;
			foreach (var provider in apiProviders)
			{
				foreach (var command in provider.Commands)
				{
					Commands.Add(command.Name, command);
				}
			}
		}
	}
}
