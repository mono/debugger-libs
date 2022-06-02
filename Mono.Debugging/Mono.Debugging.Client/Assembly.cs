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
		string name;
		string path;
		bool optimized;
		string symbolStatus;
		string symbolFile;
		int order;
		string timestamp;
		string address;
		string process;
		string appdomain;

		public Assembly (string name, string path, bool optimized, string symbolStatus, string symbolFile, int? order, string timestamp, string address, string process, string appdomain)
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
		}
		public Assembly (string path)
		{
			Path = path;
		}
		public string Name {
			get { return name; }
			protected set { name = value; }
		}
		public string Path {
			get { return path; }
			protected set { path = value; }
		}
		public bool Optimized {
			get { return optimized; }
			protected set { optimized = value; }
		}

		public string SymbolStatus {
			get { return symbolStatus; }
			protected set { symbolStatus = value; }
		}
		public string SymbolFile {
			get { return symbolFile; }
			protected set { symbolFile = value; }
		}
		public int Order {
			get { return order; }
			protected set { order = value; }
		}
		public string TimeStamp {
			get { return timestamp; }
			protected set { timestamp = value; }
		}

		public string Address {
			get { return address; }
			protected set { address = value; }
		}
		public string Process {
			get { return process; }
			protected set { process = value; }
		}

		public string AppDomain {
			get { return appdomain; }
			protected set { appdomain = value; }
		}
	}
}