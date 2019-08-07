using System;
using System.IO;

namespace Mono.Debugging.Client
{
	[Serializable]
	public class SourceLink
	{
		public string Uri { get; }
		public string RelativeFilePath { get; }

		public SourceLink (string uri, string relativeFilePath)
		{
			RelativeFilePath = relativeFilePath;
			Uri = uri;
		}

		public string GetDownloadLocation (string cachePath)
		{
			return Path.Combine (cachePath, new Uri (Uri).GetComponents (UriComponents.Path, UriFormat.SafeUnescaped));
		}
	}
}
