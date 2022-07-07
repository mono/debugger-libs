//
// Assembly.cs
//
// Author:
//       Jonathan Chang <t-jochang@microsoft.com>
//
// Copyright (c) 2022 
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
namespace Mono.Debugging.Client
{
	public class Assembly
	{
		public Assembly (string name, string path, bool optimized, bool userCode, string symbolStatus, string symbolFile, int? order, string version, string timestamp, string address, string process, string appdomain, long? processId)
		{
			Name = name;
			Path = path;
			Optimized = optimized;
			SymbolStatus = symbolStatus;
			SymbolFile = symbolFile;
			if (order == null) {
				Order = -1;
			} else {
				Order = (int)order;
			}

			TimeStamp = timestamp;
			Address = address;
			Process = process;
			AppDomain = appdomain;
			Version = version;
			UserCode = userCode;
			ProcessId = processId;

		}
		public Assembly (string path)
		{
			Path = path;
		}

		public string Name { get; private set; }

		public string Path { get; private set; }

		public bool Optimized { get; private set; }

		public bool UserCode { get; private set; }

		public string SymbolStatus { get; private set; }

		public string SymbolFile { get; private set; }

		public int Order { get; private set; }

		public String Version { get; private set; }

		public string TimeStamp { get; private set; }

		public string Address { get; private set; }

		public string Process { get; private set; }

		public string AppDomain { get; private set; }

		public long? ProcessId { get; private set; } = -1;
	}
}