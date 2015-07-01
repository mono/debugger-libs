using System;
using System.Collections.Generic;
using Mono.Debugging.Client;

namespace Mono.Debugging.LLDB
{
	public class LLDBDebuggerStartInfo : DebuggerStartInfo
	{
		public string AppPath { get; private set; }

		public LLDBDebuggerStartInfo (string appPath)
		{
			AppPath = appPath;
		}
	}
}

