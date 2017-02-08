using System;

namespace Mono.Debugging.Evaluation
{
	public class ValueModificationException : Exception
	{
		public ValueModificationException ()
		{
		}

		public ValueModificationException (string message) : base (message)
		{
		}

		public ValueModificationException (string message, Exception innerException) : base (message, innerException)
		{
		}
	}
}