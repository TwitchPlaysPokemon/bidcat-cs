using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace ApiListener
{
	public class ApiCommand
	{
		public string Name { get; private set; }
		public Func<IEnumerable<string>, string> Function { get; private set; }
		public IEnumerable<ApiParameter> Parameters { get; private set; }
		public string Description { get; private set; }

		public ApiCommand(string name, Func<IEnumerable<string>, string> func, IEnumerable<ApiParameter> parameters = null, string description = null)
		{
			Name = name;
			Function = func;
			Parameters = parameters;
			Description = description;
		}

		public ApiCommand(string name, Action<IEnumerable<string>> action, IEnumerable<ApiParameter> parameters = null, string description = null)
			: this(name, args =>
			{
				action(args);
				return null;
			}, parameters, description)
		{ }

		public ApiCommand(string name, Func<IEnumerable<string>, object> func, IEnumerable<ApiParameter> parameters = null, string descripton = null)
			: this(name, args => JsonConvert.SerializeObject(func(args), new SingleOrArrayEnumConverter()), parameters, descripton)
		{ }

		public string BuildDocString()
		{
			var docString = new StringBuilder(Name).Append(":\t");

			void DocParam(ApiParameter parameter)
			{
				if (parameter != null)
				{
					docString.Append("/").Append(parameter.Optional ? '[' : '<').Append(parameter.Name);
					if (!string.IsNullOrWhiteSpace(parameter.Type))
						docString.Append(':').Append(parameter.Type);
					docString.Append(parameter.Optional ? ']' : '>');
				}
			}

			if (Parameters != null)
			{
				docString.Append("(Usage: \"");
				docString.Append('/').Append(Name);
				foreach (var parameter in Parameters)
					DocParam(parameter);
				docString.Append("\")\t");
			}

			docString.Append(Description ?? "No description provided");

			return docString.ToString();
		}
	}

	public class ApiParameter
	{
		public string Name { get; private set; }
		public string Type { get; private set; }
		public bool Optional { get; private set; }

		public ApiParameter(string name, string type = "int", bool optional = false)
		{
			Name = name;
			Type = type;
			Optional = optional;
		}
	}
}
