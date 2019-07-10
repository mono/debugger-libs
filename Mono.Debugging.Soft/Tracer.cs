//
// Tracer.cs
//
// Author:
//       jasonimison <jaimison@microsoft.com>
//
// Copyright (c) 2019 Microsoft
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;

namespace Mono.SymClient
{
	/// <summary>
	/// Simple trace/logging support.
	/// </summary>
	public class MyTracer : Microsoft.SymbolStore.ITracer
	{
		public bool Enabled = true;
		public bool EnabledVerbose = true;

		public void WriteLine (string message)
		{
			Console.WriteLine (message);
		}

		public void WriteLine (string format, params object [] arguments)
		{
			Console.WriteLine (format, arguments);
		}

		public void Information (string message)
		{
			if (Enabled) {
				Console.WriteLine (message);
			}
		}

		public void Information (string format, params object [] arguments)
		{
			if (Enabled) {
				Console.WriteLine (format, arguments);
			}
		}

		public void Warning (string message)
		{
			if (Enabled) {
				Console.WriteLine ("WARNING: " + message);
			}
		}

		public void Warning (string format, params object [] arguments)
		{
			if (Enabled) {
				Console.WriteLine ("WARNING: " + format, arguments);
			}
		}

		public void Error (string message)
		{
			Console.WriteLine ("ERROR: " + message);
		}

		public void Error (string format, params object [] arguments)
		{
			Console.WriteLine ("ERROR: " + format, arguments);
		}

		public void Verbose (string message)
		{
			if (EnabledVerbose) {
				Console.WriteLine (message);
			}
		}

		public void Verbose (string format, params object [] arguments)
		{
			if (EnabledVerbose) {
				Console.WriteLine (format, arguments);
			}
		}
	}
}
