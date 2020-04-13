//
// IgnoreBreak.cs
//
// Author:
//       Cody Russell <coruss@microsoft.com>
//
// Copyright (c) 2020 Microsoft
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
using System.IO;
using System.Xml;

namespace Mono.Debugging.Client
{
	[Serializable]
	public class IgnoreBreak : IgnoreEvent
	{
		string type;
		string filename;
		int line;
		int column;

		public IgnoreBreak (string type, string filename, int line, int column)
		{
			this.type = type;
			this.filename = filename;
			this.line = line;
			this.column = column;
		}

		public IgnoreBreak (string type, string fileName, int line)
			: this (type, fileName, line, 1)
		{
		}

		public string FileName {
			get => filename;
			set => filename = value;
		}

		public int Line {
			get => line;
			set => line = value;
		}

		public int Column {
			get => column;
			set => column = value;
		}

		internal IgnoreBreak (XmlElement elem, string baseDir)
			: base (elem, baseDir)
		{
			string s = elem.GetAttribute ("relfile");
			if (!string.IsNullOrEmpty (s) && baseDir != null) {
				FileName = Path.Combine (baseDir, s);
			} else {
				s = elem.GetAttribute ("file");
				if (!string.IsNullOrEmpty (s))
					FileName = s;
			}

			s = elem.GetAttribute ("line");
			if (string.IsNullOrEmpty (s) || !int.TryParse (s, out line))
				line = 1;

			s = elem.GetAttribute ("column");
			if (string.IsNullOrEmpty (s) || !int.TryParse (s, out column))
				column = 1;
		}

		internal override XmlElement ToXml (XmlDocument doc, string baseDir)
		{
			var elem = base.ToXml (doc, baseDir);

			if (!string.IsNullOrEmpty(filename)) {
				elem.SetAttribute ("file", filename);
				if (baseDir != null) {
					if (filename.StartsWith (baseDir, StringComparison.Ordinal))
						elem.SetAttribute ("relfile", filename.Substring (baseDir.Length).TrimStart (Path.DirectorySeparatorChar));
				}
			}

			elem.SetAttribute ("line", line.ToString ());
			elem.SetAttribute ("column", column.ToString ());

			return elem;
		}
	}
}
