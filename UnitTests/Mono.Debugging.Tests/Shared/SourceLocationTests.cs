//
// SourceLocationTests.cs
//
// Author:
//       Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2019 Microsoft Corp.
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
using System.Text;
using System.Security.Cryptography;

using Mono.Debugging.Client;

using NUnit.Framework;

namespace Mono.Debugging.Tests
{
	[TestFixture]
	public class SourceLocationTests
	{
		static void TestCheckFileHash (string algorithm, string text, string mode)
		{
			var buffer = Encoding.ASCII.GetBytes (text);

			using (var hash = HashAlgorithm.Create (algorithm)) {
				var checksum = hash.ComputeHash (buffer);
				string tmp;

				tmp = Path.GetTempFileName ();

				try {
					File.WriteAllBytes (tmp, buffer);

					Assert.IsTrue (SourceLocation.CheckFileHash (tmp, checksum), "CheckFileHash {0} ({1})", algorithm, mode);

					if (checksum.Length > 16) {
						var roslyn = new byte [16];

						roslyn [0] = (byte)checksum.Length;
						for (int i = 1; i < 16; i++)
							roslyn [i] = checksum [i - 1];

						Assert.IsTrue (SourceLocation.CheckFileHash (tmp, checksum), "CheckFileHash Roslyn {0} ({1})", algorithm, mode);
					}
				} finally {
					File.Delete (tmp);
				}
			}
		}

		static void TestCheckFileHash (string algorithm)
		{
			string text = "This is line 1.\nThis is line 2.\r\nThis is line 3.\r\nThis is line 4.\nThis is line 5.\n";

			TestCheckFileHash (algorithm, text, "MIXED");

			text = text.Replace ("\r\n", "\n");

			TestCheckFileHash (algorithm, text, "UNIX");

			text = text.Replace ("\n", "\r\n");

			TestCheckFileHash (algorithm, text, "DOS");
		}

		[Test]
		public void TestCheckFileHashSha1 ()
		{
			TestCheckFileHash ("SHA1");
		}

		[Test]
		public void TestCheckFileHashSha256 ()
		{
			TestCheckFileHash ("SHA256");
		}

		[Test]
		public void TestCheckFileHashMd5 ()
		{
			TestCheckFileHash ("MD5");
		}
	}
}
