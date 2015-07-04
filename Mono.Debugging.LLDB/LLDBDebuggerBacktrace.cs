//
// LLDBDebuggerBacktrace.cs
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
using System.Linq;
using Mono.Debugging.Evaluation;
using Mono.Debugging.Client;
using Mono.Debugging.Backend;
using LLDBSharp = LLDB;

namespace Mono.Debugging.LLDB
{
	public class LLDBDebuggerBacktrace : IBacktrace, IObjectValueSource
	{
		LLDBDebuggerSession session;
		LLDBSharp.Thread thread;

		public LLDBDebuggerBacktrace (LLDBDebuggerSession session, LLDBSharp.Thread thread)
		{
			this.session = session;
			this.thread = thread;
		}

		public StackFrame[] GetStackFrames (int firstIndex, int lastIndex)
		{
			int count = lastIndex - firstIndex + 1;
			var frames = new StackFrame[count];

			for (int i = 0; i < count; ++i)
				frames [i] = thread.GetFrameAtIndex ((uint)(firstIndex + i)).ToFrame ();
			return frames;
		}

		public ObjectValue[] GetLocalVariables (int frameIndex, EvaluationOptions options)
		{
			return thread
				.GetFrameAtIndex ((uint)frameIndex)
				.GetVariables (new LLDBSharp.VariablesOptions {
					IncludeLocals = true,
					IncludeArguments = false,
					InScopeOnly = true,
				})
				.ToValues (this);
		}

		public ObjectValue[] GetParameters (int frameIndex, EvaluationOptions options)
		{
			return thread
				.GetFrameAtIndex ((uint)frameIndex)
				.GetVariables (new LLDBSharp.VariablesOptions {
					IncludeLocals = false,
					InScopeOnly = true,
					IncludeArguments = true,
				})
				.ToValues (this);
		}

		public ObjectValue GetThisReference (int frameIndex, EvaluationOptions options)
		{
			var @this = thread.GetFrameAtIndex ((uint)frameIndex).GetValueForVariablePath ("this");
			return @this.IsValid () ? @this.ToValue (new LLDBDebuggerBacktrace (session, thread)) : null;
		}

		public ExceptionInfo GetException (int frameIndex, EvaluationOptions options)
		{
			return null;
		}

		public ObjectValue[] GetAllLocals (int frameIndex, EvaluationOptions options)
		{
			return GetLocalVariables (frameIndex, options).Concat (GetParameters (frameIndex, options)).ToArray ();
		}

		public ObjectValue[] GetExpressionValues (int frameIndex, string[] expressions, EvaluationOptions options)
		{
			var results = new ObjectValue[expressions.Length];
			var frame = thread.GetFrameAtIndex ((uint)frameIndex);

			for (int i = 0; i < results.Length; ++i)
				results [i] = frame.EvaluateExpression (expressions [i]).ToValue (this);
			return results;
		}

		public CompletionData GetExpressionCompletionData (int frameIndex, string exp)
		{
			return null;
		}

		public AssemblyLine[] Disassemble (int frameIndex, int firstLine, int count)
		{
			return new AssemblyLine[0];
		}

		public ValidationResult ValidateExpression (int frameIndex, string expression, EvaluationOptions options)
		{
			return new ValidationResult (true, null);
		}

		public int FrameCount {
			get {
				return (int)thread.NumFrames;
			}
		}

		public ObjectValue[] GetChildren (ObjectPath path, int index, int count, EvaluationOptions options)
		{
			// TODO:
			return new ObjectValue[0];
		}

		public EvaluationResult SetValue (ObjectPath path, string value, EvaluationOptions options)
		{
			// TODO: Check error.
			thread.GetSelectedFrame ().GetValueForVariablePath (path.ToString ()).SetValueFromCString (value);
			return new EvaluationResult (value);
		}

		public ObjectValue GetValue (ObjectPath path, EvaluationOptions options)
		{
			var value = thread.GetSelectedFrame ().GetValueForVariablePath (path.ToString ());
			return value.ToValue (this);
		}

		public object GetRawValue (ObjectPath path, EvaluationOptions options)
		{
			throw new NotSupportedException ();
		}

		public void SetRawValue (ObjectPath path, object value, EvaluationOptions options)
		{
			throw new NotSupportedException ();
		}

	}
}

