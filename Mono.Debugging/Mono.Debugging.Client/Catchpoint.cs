// Catchpoint.cs
//
// Author:
//   Lluis Sanchez Gual <lluis@novell.com>
//
// Copyright (c) 2008 Novell, Inc (http://www.novell.com)
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
//
//

using System;
using System.Xml;

namespace Mono.Debugging.Client
{
	[Serializable]
	public class Catchpoint: BreakEvent
	{
		string exceptionName;
		bool includeSubclasses;
		
		public Catchpoint (string exceptionName) : this (exceptionName, true)
		{
		}

		public Catchpoint (string exceptionName, bool includeSubclasses)
		{
			this.exceptionName = exceptionName;
			this.includeSubclasses = includeSubclasses;
		}
		
		internal Catchpoint (XmlElement elem): base (elem)
		{
			exceptionName = elem.GetAttribute ("exceptionName");

			var str = elem.GetAttribute ("includeSubclasses");
			if (string.IsNullOrEmpty (str) || !bool.TryParse (str, out includeSubclasses)) {
				// fall back to the old default behavior
				includeSubclasses = true;
			}
		}

		internal override XmlElement ToXml (XmlDocument doc)
		{
			XmlElement elem = base.ToXml (doc);
			elem.SetAttribute ("exceptionName", exceptionName);
			elem.SetAttribute ("includeSubclasses", includeSubclasses.ToString ());
			return elem;
		}

		
		public string ExceptionName {
			get { return exceptionName; }
		}

		public bool IncludeSubclasses {
			get { return includeSubclasses; }
		}
		
		public override void CopyFrom (BreakEvent ev)
		{
			base.CopyFrom (ev);
			Catchpoint cp = (Catchpoint) ev;
			exceptionName = cp.exceptionName;
			includeSubclasses = cp.includeSubclasses;
		}

	}
}
