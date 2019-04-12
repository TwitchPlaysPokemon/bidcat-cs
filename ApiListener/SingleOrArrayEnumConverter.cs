using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

public class SingleOrArrayEnumConverter : JsonConverter
{
	public override bool CanConvert(Type objectType)
	{
		return (objectType.IsArray && objectType.GetElementType().IsEnum);
	}

	public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
	{
		JToken token = JToken.Load(reader);
		if (token.Type == JTokenType.Array)
		{
			return token.ToObject<string[]>().Select(x => Convert.ChangeType(Enum.Parse(objectType.GetElementType(), x.ToLower()), objectType)).ToArray();
		}

		return new List<object> { Convert.ChangeType(Enum.Parse(objectType.GetElementType(), token.ToObject<string>().ToLower()), objectType) };
	}

	public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
	{
		if (value.GetType().IsArray && value.GetType().GetElementType().IsEnum)
		{
			List<object> list2 = new List<object>();
			Array list = (Array)value;
			foreach (object obj in list)
				list2.Add(obj.ToString().ToLower());

			value = list2;
		}
		else if (value.GetType().IsEnum)
		{
			value = value.ToString().ToLower();
		}

		serializer.Serialize(writer, value);
	}

	public override bool CanWrite
	{
		get { return true; }
	}
}

