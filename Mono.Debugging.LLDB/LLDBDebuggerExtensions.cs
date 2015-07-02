//
// LLDBDebuggerExtensions.cs
//
// Author:
//       Marius Ungureanu <marius.ungureanu@xamarin.com>
//
// Copyright (c) 2015 Xamarin, Inc (http://www.xamarin.com)
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
using LLDBSharp = LLDB;
using Mono.Debugging.Client;

namespace Mono.Debugging.LLDB
{
	public static class LLDBDebuggerExtensions
	{
		public static StackFrame ToFrame (this LLDBSharp.Frame frame)
		{
			var funcStartAddress = frame.GetFunction ().GetStartAddress ();
			var funcStartFile = funcStartAddress.GetLineEntry ();
			var funcSpec = funcStartFile.GetFileSpec ();

			var funcEndAddress = frame.GetFunction ().GetEndAddress ();
			var funcEndFile = funcEndAddress.GetLineEntry ();

			var name = frame.FunctionName ?? frame.GetSymbol ().Name ?? "unknown";
			string file;
			if (funcSpec.Directory != null && funcSpec.Filename != null)
				file = System.IO.Path.Combine (funcSpec.Directory, funcSpec.Filename);
			else {
				file = funcSpec.Directory ?? funcSpec.Filename ?? "unknown";
			}

			return new StackFrame (
				(long)frame.GetPCAddress ().Offset,
				new SourceLocation (
					name,
					file,
					(int)frame.GetLineEntry ().Line,
					(int)frame.GetLineEntry ().Column,
					(int)funcEndFile.Line,
					(int)funcEndFile.Column),
				name == "unknown" ? "Unknown" : "Native");
		}

		public static ObjectValue ToValue (this LLDBSharp.Value value, LLDBDebuggerBacktrace trace)
		{
			ObjectValue[] children;

			// TODO: Non-primitives.
			const ObjectValueFlags flags = ObjectValueFlags.Variable;

			// Maybe use typeclass?
			var type = value.GetType ();
			if (type.IsArrayType ()) {
				children = new ObjectValue [value.NumChildren];
				for (uint i = 0; i < value.NumChildren; ++i)
					children [i] = value.GetChildAtIndex (i).ToValue (trace);
				
				return ObjectValue.CreateArray (trace,
					new ObjectPath (value.Name),
					value.TypeName,
					(int)value.NumChildren,
					flags,
					children
				);
			} else if (type.TypeClass == LLDBSharp.TypeClass.Class || type.TypeClass == LLDBSharp.TypeClass.Struct) {
				children = new ObjectValue [value.NumChildren];
				for (uint i = 0; i < value.NumChildren; ++i)
					children [i] = value.GetChildAtIndex (i).ToValue (trace);
			
				return ObjectValue.CreateObject (trace,
					new ObjectPath (value.Name),
					value.TypeName,
					value.DisplayTypeName,
					flags,
					children
				);
			} else
				return ObjectValue.CreatePrimitive (trace,
					new ObjectPath (value.Name),
					value.TypeName,
					new Mono.Debugging.Backend.EvaluationResult (value.ValueAsString),
					flags
				);
		}

		public static ObjectValue[] ToValues (this LLDBSharp.ValueList values, LLDBDebuggerBacktrace trace)
		{
			var result = new ObjectValue [values.Size];
			for (uint i = 0; i < values.Size; ++i)
				result [i] = values.GetValueAtIndex (i).ToValue (trace);
			return result;
		}
	}
}

