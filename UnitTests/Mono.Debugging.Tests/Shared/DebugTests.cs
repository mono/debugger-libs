// 
// DebugTests.cs
//  
// Author:
//       Lluis Sanchez Gual <lluis@novell.com>
// 
// Copyright (c) 2009 Novell, Inc (http://www.novell.com)
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
using System.Threading;
using System.Reflection;
using System.Collections.Generic;

using Mono.Debugging.Soft;
using Mono.Debugging.Client;

using NUnit.Framework;

namespace Mono.Debugging.Tests
{
	[TestFixture]
	public abstract partial class DebugTests
	{
		protected readonly ManualResetEvent targetStoppedEvent = new ManualResetEvent (false);
		public readonly string EngineId;
		string TestName = "";
		ITextFile SourceFile;

		SourceLocation lastStoppedPosition;

		public bool AllowTargetInvokes { get; protected set; }

		public DebuggerSession Session { get; private set; }

		public StackFrame Frame { get; private set; }

		protected DebugTests (string engineId)
		{
			EngineId = engineId;
		}

		public bool IsCorDebugger {
			get { return EngineId == "MonoDevelop.Debugger.Win32"; }
		}

		public void IgnoreCorDebugger (string message = "")
		{
			if (IsCorDebugger)
				Assert.Ignore (message);
		}

		public bool IsSoftDebugger {
			get { return Session is SoftDebuggerSession; }
		}

		public void IgnoreSoftDebugger (string message = "")
		{
			if (IsSoftDebugger)
				Assert.Ignore (message);
		}

		public bool IsVsDebugger {
			get { return EngineId == "NetCoreDebugger"; }
		}

		public void IgnoreVsDebugger (string message = "")
		{
			if (IsVsDebugger)
				Assert.Ignore (message);
		}

		// TODO: implement in another part of the class
		#region Partial Definitions
		/*
		/// <summary>
		/// Returns parent directory of target executable
		/// </summary>
		protected string TargetExeDirectory { get { throw new NotImplementedException (); } }

		/// <summary>
		/// Returns parent directory of target project sources
		/// </summary>
		protected string TargetProjectSourceDir { get { throw new NotImplementedException (); } }

		/// <summary>
		/// Creates debugger session. The type of session is dependent on <paramref name="engineId"/>
		/// </summary>
		/// <param name="test">test name, usually used as entry point method in target exe</param>
		/// <param name="engineId">the ID of debugger engine</param>
		protected DebuggerSession CreateSession (string test, string engineId);

		/// <summary>
		/// Creates start info to run the app
		/// </summary>
		/// <param name="test">test name</param>
		/// <param name="engineId">the ID of debugger engine</param>
		protected DebuggerStartInfo CreateStartInfo (string test, string engineId);

		/// <summary>
		/// Reads file from given path
		/// </summary>
		/// <param name="sourcePath"></param>
		/// <returns></returns>
		protected ITextFile ReadFile (string sourcePath)
		*/
		#endregion


		[OneTimeSetUp]
		public virtual void SetUp ()
		{
			SetUpPartial ();
		}

		partial void SetUpPartial ();

		[OneTimeTearDown]
		public virtual void TearDown ()
		{
			TearDownPartial ();
			if (Session != null) {
				Session.Exit ();
				Session.Dispose ();
				Session = null;
			}
		}

		partial void TearDownPartial ();

		protected virtual string TargetExePath {
			get { return Path.Combine (TargetExeDirectory, TestAppExeName); }
		}

		protected virtual void Start (string test)
		{
			TestName = test;
			Session = CreateSession (test, EngineId);
			// make sure we have a breakpoint store created and setup
			Session.Breakpoints.Clear ();

			var dsi = CreateStartInfo (test, EngineId);
			var soft = dsi as SoftDebuggerStartInfo;

			if (soft != null) {
				var assemblyName = AssemblyName.GetAssemblyName (TargetExePath);

				soft.UserAssemblyNames = new List<AssemblyName> {assemblyName};
			}

			var ops = new DebuggerSessionOptions {
				ProjectAssembliesOnly = true,
				EvaluationOptions = EvaluationOptions.DefaultOptions
			};
			ops.EvaluationOptions.AllowTargetInvoke = AllowTargetInvokes;
			ops.EvaluationOptions.EvaluationTimeout = 100000;

			var sourcePath = Path.Combine (TargetProjectSourceDir, test + ".cs");
			SourceFile = ReadFile(sourcePath);
			AddBreakpoint ("break");

			var done = new ManualResetEvent (false);

			Session.TargetHitBreakpoint += (sender, e) => {
				Frame = e.Backtrace.GetFrame (0);
				lastStoppedPosition = Frame.SourceLocation;
				targetStoppedEvent.Set ();
				done.Set ();
			};

			Session.TargetExceptionThrown += (sender, e) => {
				Frame = e.Backtrace.GetFrame (0);
				for (int i = 0; i < e.Backtrace.FrameCount; i++) {
					if (!e.Backtrace.GetFrame (i).IsExternalCode) {
						Frame = e.Backtrace.GetFrame (i);
						break;
					}
				}
				lastStoppedPosition = Frame.SourceLocation;
				targetStoppedEvent.Set ();
			};

			Session.TargetStopped += (sender, e) => {
				//This can be null in case of ForcedStop
				//which is called when exception is thrown
				//when Continue & Stepping is executed
				if (e.Backtrace != null) {
					Frame = e.Backtrace.GetFrame (0);
					lastStoppedPosition = Frame.SourceLocation;
					targetStoppedEvent.Set ();
				} else {
					Console.WriteLine ("e.Backtrace is null");
				}
			};

			var targetExited = new ManualResetEvent (false);
			Session.TargetExited += delegate {
				targetExited.Set ();
			};

			Session.Run (dsi, ops);
			Session.ExceptionHandler = (ex) => {
				Console.WriteLine ("Session.ExceptionHandler:" + Environment.NewLine + ex.ToString ());
				HandleAnyException(ex);
				return true;
			};
			switch (WaitHandle.WaitAny (new WaitHandle[]{ done, targetExited }, 30000)) {
			case 0:
				//Breakpoint is hit good... run tests now
				break;
			case 1:
				throw new Exception ("Test application exited before hitting breakpoint");
			default:
				throw new Exception ("Timeout while waiting for initial breakpoint");
			}

			if (Session is SoftDebuggerSession)
				Console.WriteLine ("SDB protocol version:" + ((SoftDebuggerSession)Session).ProtocolVersion);
		}

		void GetLineAndColumn (string breakpointMarker, int offset, string statement, out int line, out int col, ITextFile file)
		{
			int i = file.Text.IndexOf ("/*" + breakpointMarker + "*/", StringComparison.Ordinal);
			if (i == -1)
				Assert.Fail ("Break marker not found: " + breakpointMarker + " in " + file.Name);

			file.GetLineColumnFromPosition (i, out line, out col);
			line += offset;
			if (statement != null) {
				int lineStartPosition = file.GetPositionFromLineColumn (line, 1);
				string lineText = file.GetText (lineStartPosition, lineStartPosition + file.GetLineLength (line));
				col = lineText.IndexOf (statement, StringComparison.Ordinal) + 1;
				if (col == 0)
					Assert.Fail ("Failed to find statement:" + statement + " at " + file.Name + "(" + line + ")");
			} else {
				col = 1;
			}
		}

		public Breakpoint AddBreakpoint (string breakpointMarker, int offset = 0, string statement = null, ITextFile file = null)
		{
			var bp = CreateBreakpoint (breakpointMarker, offset, statement, file);
			Session.Breakpoints.Add (bp);
			return bp;
		}

		public Breakpoint CreateBreakpoint (string breakpointMarker, int offset = 0, string statement = null, ITextFile file = null)
		{
			file = file ?? SourceFile;
			int col, line;
			GetLineAndColumn (breakpointMarker, offset, statement, out line, out col, file);
			var bp = new Breakpoint (file.Name, line, col);
			return bp;
		}

		public void RunToCursor (string breakpointMarker, int offset = 0, string statement = null, ITextFile file = null)
		{
			file = file ?? SourceFile;
			int col, line;
			GetLineAndColumn (breakpointMarker, offset, statement, out line, out col, file);
			targetStoppedEvent.Reset ();
			Session.Breakpoints.RemoveRunToCursorBreakpoints ();
			var bp = new RunToCursorBreakpoint (file.Name, line, col);
			Session.Breakpoints.Add (bp);
			Session.Continue ();
			CheckPosition (breakpointMarker, offset, statement);
		}

		public void InitializeTest ()
		{
			Session.Breakpoints.Clear ();
			Session.Options.EvaluationOptions = EvaluationOptions.DefaultOptions;
			Session.Options.ProjectAssembliesOnly = true;
			Session.Options.StepOverPropertiesAndOperators = false;
			AddBreakpoint ("break");
			while (!CheckPosition ("break", 0, silent: true)) {
				targetStoppedEvent.Reset ();
				Session.Continue ();
			}
		}

		public ObjectValue Eval (string exp)
		{
			return Frame.GetExpressionValue (exp, true).Sync ();
		}

		public void WaitStop (int miliseconds)
		{
			if (!targetStoppedEvent.WaitOne (miliseconds))
				Assert.Fail ("WaitStop failure: Target stop timeout");
		}

		public bool CheckPosition (string guid, int offset = 0, string statement = null, bool silent = false, ITextFile file = null)
		{
			file = file ?? SourceFile;
			if (!targetStoppedEvent.WaitOne (6000)) {
				if (!silent)
					Assert.Fail ("CheckPosition failure: Target stop timeout");
				return false;
			}

			if (lastStoppedPosition.FileName == file.Name) {
				int i = file.Text.IndexOf ("/*" + guid + "*/", StringComparison.Ordinal);
				if (i == -1) {
					if (!silent)
						Assert.Fail ("CheckPosition failure: Guid marker not found:" + guid + " in file:" + file.Name);
					return false;
				}

				int line, col;
				file.GetLineColumnFromPosition (i, out line, out col);
				if ((line + offset) != lastStoppedPosition.Line) {
					if (!silent)
						Assert.Fail ("CheckPosition failure: Wrong line Expected:" + (line + offset) + " Actual:" + lastStoppedPosition.Line + " in file:" + file.Name);
					return false;
				}

				if (!string.IsNullOrEmpty (statement)) {
					int position = file.GetPositionFromLineColumn (lastStoppedPosition.Line, lastStoppedPosition.Column);
					string actualStatement = file.GetText (position, position + statement.Length);
					if (statement != actualStatement) {
						if (!silent)
							Assert.AreEqual (statement, actualStatement);
						return false;
					}
				}
			} else {
				if (!silent)
					Assert.Fail ("CheckPosition failure: Wrong file Excpected:" + file.Name + " Actual:" + lastStoppedPosition.FileName);
				return false;
			}

			return true;
		}

		public void StepIn (string guid, string statement)
		{
			StepIn (guid, 0, statement);
		}

		public void StepIn (string guid, int offset = 0, string statement = null)
		{
			targetStoppedEvent.Reset ();
			Session.StepInstruction ();
			CheckPosition (guid, offset, statement);
		}

		public void StepOver (string guid, string statement)
		{
			StepOver (guid, 0, statement);
		}

		public void StepOver (string guid, int offset = 0, string statement = null)
		{
			targetStoppedEvent.Reset ();
			Session.NextInstruction ();
			CheckPosition (guid, offset, statement);
		}

		public void StepOut (string guid, string statement)
		{
			StepOut (guid, 0, statement);
		}

		public void StepOut (string guid, int offset = 0, string statement = null)
		{
			targetStoppedEvent.Reset ();
			Session.Finish ();
			CheckPosition (guid, offset, statement);
		}

		public void Continue (string guid, string statement)
		{
			Continue (guid, 0, statement);
		}

		public void Continue (string guid, int offset = 0, string statement = null, ITextFile file = null)
		{
			targetStoppedEvent.Reset ();
			Session.Continue ();
			CheckPosition(guid, offset, statement, file: file);
		}

		public void StartTest (string methodName)
		{
			if (!targetStoppedEvent.WaitOne (3000))
				Assert.Fail ("StartTest failure: Target stop timeout");

			Assert.AreEqual ('"' + methodName + '"', Eval ("NextMethodToCall = \"" + methodName + "\";").Value);
			targetStoppedEvent.Reset ();
			Session.Continue ();
		}

		public void SetNextStatement (string guid, int offset = 0, string statement = null, ITextFile file = null)
		{
			file = file ?? SourceFile;
			int line, column;
			GetLineAndColumn (guid, offset, statement, out line, out column, file);
			Session.SetNextStatement (file.Name, line, column);
		}

		public void AddCatchpoint (string exceptionName, bool includeSubclasses)
		{
			Session.Breakpoints.Add (new Catchpoint (exceptionName, includeSubclasses));
		}

		partial void HandleAnyException(Exception exception);
	}

	static class EvalHelper
	{
		public static bool AtLeast (this Version ver, int major, int minor) {
			if ((ver.Major > major) || ((ver.Major == major && ver.Minor >= minor)))
				return true;
			else
				return false;
		}

		public static ObjectValue Sync (this ObjectValue val)
		{
			if (!val.IsEvaluating)
				return val;

			object locker = new object ();
			EventHandler h = delegate {
				lock (locker) {
					Monitor.PulseAll (locker);
				}
			};

			val.ValueChanged += h;

			lock (locker) {
				while (val.IsEvaluating) {
					if (!Monitor.Wait (locker, 8000))
						throw new Exception ("Timeout while waiting for value evaluation");
				}
			}

			val.ValueChanged -= h;
			return val;
		}

		public static ObjectValue GetChildSync (this ObjectValue val, string name, EvaluationOptions ops)
		{
			var children = val.GetAllChildrenSync (ops);
			foreach (var child in children) {
				if (child.Name == name)
					return child;
			}
			return null;
		}

		public static ObjectValue[] GetAllChildrenSync (this ObjectValue val, EvaluationOptions ops = null)
		{
			var children = new List<ObjectValue> ();
			var values = ops == null ? val.GetAllChildren () : val.GetAllChildren (ops);
			for (int i = 0; i < values.Length; i++) {
				var value = values[i].Sync ();

				if (value.IsEvaluatingGroup) {
					if (ops != null)
						children.AddRange (value.GetAllChildrenSync (ops));
					else
						children.AddRange (value.GetAllChildrenSync ());
				} else {
					children.Add (value);
				}
			}
			return children.ToArray ();
		}

		public static ObjectValue[] GetAllLocalsSync(this StackFrame frame)
		{
			var locals = new List<ObjectValue> ();
			var values = frame.GetAllLocals ();
			for (int i = 0; i < values.Length; i++) {
				var value = values[i].Sync ();

				if (value.IsEvaluatingGroup)
					locals.AddRange (value.GetAllChildrenSync ());
				else
					locals.Add (value);
			}
			return locals.ToArray ();
		}

		public static ExceptionInfo GetExceptionSync(this StackFrame frame, EvaluationOptions options)
		{
			var exception = frame.GetException (options);

			if (exception != null)
				exception.Instance.Sync ();

			return exception;
		}
	}
}
