using System;

namespace ApiListener
{
	public class ApiError : Exception
	{
		public ApiError(string message = null) : base(message) { }
	}
}
