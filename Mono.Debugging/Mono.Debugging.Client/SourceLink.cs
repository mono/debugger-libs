using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Mono.Debugging.Client
{
	[Serializable]
	public class SourceLink
	{
		// Keep the / character to use as a path separator to help group related symbols
		static readonly HashSet<char> invalids = new HashSet<char>(Path.GetInvalidFileNameChars ().Except (new char[] { '/' }));

		public string Uri { get; }

		public string RelativeFilePath { get; }

		public SourceLink (string uri, string relativeFilePath)
		{
			RelativeFilePath = relativeFilePath;
			Uri = uri;
		}

		public string GetDownloadLocation (string cachePath)
		{
			var uri = new Uri (Uri);
			return Path.Combine (cachePath, MakeValidFileName (uri));
		}

		static string MakeValidFileName (Uri uri)
		{
			// Remove scheme from uri
			var text = uri.Host + uri.PathAndQuery;
			var sb = new StringBuilder (text.Length);
			for (int i = 0; i < text.Length; i++) {
				char c = text[i];
				if (invalids.Contains (c)) {
					sb.Append ('_');
				} else
					sb.Append (c);
			}
			if (sb.Length == 0)
				return "_";
			return sb.ToString ();
		}
	}
}
