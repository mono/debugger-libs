using System;

namespace Mono.Debugging.Client
{
	[Serializable]
	public class SourceLocation
	{
		public string MethodName { get; private set; }
		public string FileName { get; private set; }
		public int Line { get; private set; }
		public int Column { get; private set; }
		public int EndLine { get; private set; }
		public int EndColumn { get; private set; }
		public byte[] FileHash { get; private set; }

		[Obsolete]
		public SourceLocation (string methodName, string fileName, int line)
			: this (methodName, fileName, line, -1, -1, -1, null)
		{
		}

		public SourceLocation (string methodName, string fileName, int line, int column, int endLine, int endColumn, byte[] hash = null)
		{
			this.MethodName = methodName;
			this.FileName = fileName;
			this.Line = line;
			this.Column = column;
			this.EndLine = endLine;
			this.EndColumn = endColumn;
			this.FileHash = hash;
		}
		
		public override string ToString ()
		{
			return string.Format("[SourceLocation Method={0}, Filename={1}, Line={2}, Column={3}]", MethodName, FileName, Line, Column);
		}

	}
}
