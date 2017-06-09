//
// PortablePdbData.cs
//
// Author:
//       David Karlaš <david.karlas@xamarin.com>
//
// Copyright (c) 2017 Xamarin, Inc (http://www.xamarin.com)
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
using System.Reflection.Metadata;
using Mono.Debugger.Soft;
using System.IO;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.CompilerServices;
using System.Reflection;

namespace Mono.Debugging.Soft
{
	class PortablePdbData
	{
		public static readonly Guid AsyncMethodSteppingInformationBlob = new Guid ("54FD2AC5-E925-401A-9C2A-F94F171072F8");
		public static readonly Guid StateMachineHoistedLocalScopes = new Guid ("6DA9A61E-F8C7-4874-BE62-68BC5630DF71");
		public static readonly Guid DynamicLocalVariables = new Guid ("83C563C4-B4F3-47D5-B824-BA5441477EA8");
		public static readonly Guid TupleElementNames = new Guid ("ED9FDF71-8879-4747-8ED3-FE5EDE3CE710");
		public static readonly Guid DefaultNamespace = new Guid ("58b2eab6-209f-4e4e-a22c-b2d0f910c782");
		public static readonly Guid EncLocalSlotMap = new Guid ("755F52A8-91C5-45BE-B4B8-209571E552BD");
		public static readonly Guid EncLambdaAndClosureMap = new Guid ("A643004C-0240-496F-A783-30D64F4979DE");
		public static readonly Guid SourceLink = new Guid ("CC110556-A091-4D38-9FEC-25AB9A351A6A");
		public static readonly Guid EmbeddedSource = new Guid ("0E8A571B-6926-466E-B4AD-8AB04611F5FE");

		public static bool IsPortablePdb (string pdbFileName)
		{
			if (string.IsNullOrEmpty (pdbFileName) || !File.Exists (pdbFileName))
				return false;
			using (var file = new FileStream (pdbFileName, FileMode.Open)) {
				var data = new byte [4];
				int read = file.Read (data, 0, data.Length);
				return read == 4 && BitConverter.ToUInt32 (data, 0) == 0x424a5342;
			}
		}

		private string pdbFileName;

		public PortablePdbData (string pdbFileName)
		{
			this.pdbFileName = pdbFileName;
		}

		internal class SoftScope
		{
			public int LiveRangeStart;

			public int LiveRangeEnd;
		}

		// We need proxy method to make sure VS2013/15 doesn't crash(this method won't be called if portable .pdb file doesn't exist, which means 2017+)
		[MethodImpl (MethodImplOptions.NoInlining)]
		internal SoftScope [] GetHoistedScopes (MethodMirror method) => GetHoistedScopesPrivate (method);

		internal SoftScope [] GetHoistedScopesPrivate (MethodMirror method)
		{
			using (var fs = new FileStream (pdbFileName, FileMode.Open))
			using (var metadataReader = MetadataReaderProvider.FromPortablePdbStream (fs)) {
				var reader = metadataReader.GetMetadataReader ();
				var methodHandle = MetadataTokens.MethodDefinitionHandle (method.MetadataToken);
				var customDebugInfos = reader.GetCustomDebugInformation (methodHandle);
				foreach (var item in customDebugInfos) {
					var debugInfo = reader.GetCustomDebugInformation (item);
					if (reader.GetGuid (debugInfo.Kind) == StateMachineHoistedLocalScopes) {
						var bytes = reader.GetBlobBytes (debugInfo.Value);
						var result = new SoftScope [bytes.Length / 8];
						for (int i = 0; i < bytes.Length; i += 8) {
							var offset = BitConverter.ToInt32 (bytes, i);
							var len = BitConverter.ToInt32 (bytes, i + 4);
							result [i / 8] = new SoftScope () {
								LiveRangeStart = offset,
								LiveRangeEnd = offset + len
							};
						}
						return result;
					}
				}
			}
			return null;
		}
	}
}
