using System;
using Mono.Debugging.Client;
using System.IO;
using LLDBSharp = LLDB;
using System.Linq;
using System.Runtime.InteropServices;

namespace Mono.Debugging.LLDB
{
	public class LLDBDebuggerSession : DebuggerSession
	{
		const string RelativeDebugServerPath = "Contents/SharedFrameworks/LLDB.framework/Versions/Current/Resources/debugserver";
		bool disposed;

		LLDBSharp.Debugger debugger;
		LLDBSharp.Process process;
		ThreadInfo[] threads;

		static LLDBDebuggerSession ()
		{
			// TODO: Windows, Linux.
			var xcodePath = CppSharp.XcodeToolchain.GetXcodePath ();
			Environment.SetEnvironmentVariable ("LLDB_DEBUGSERVER_PATH", Path.Combine (xcodePath, RelativeDebugServerPath));

			LLDBSharp.Debugger.Initialize ();
		}

		public LLDBDebuggerSession ()
		{
			debugger = LLDBSharp.Debugger.Create ();
			debugger.Async = false;
		}

		protected override string OnResolveExpression (string expression, SourceLocation location)
		{
			return string.Empty;
		}

		protected override void OnRun (DebuggerStartInfo startInfo)
		{
			var info = (LLDBDebuggerStartInfo)startInfo;

			var target = debugger.CreateTarget (info.Command);
			if (target == null)
				throw new Exception ("Could not create LLDB target");

			// TODO: Make this be on transitions managed to native.
			var mainBreakpoint = target.BreakpointCreateByName ("mono_runtime_invoke", target.GetExecutable().Filename);

			// TODO: Make this app current dir.
			var currentDir = Directory.GetCurrentDirectory ();

			// Be afraid, be very afraid.
			var argsArr = info.Arguments.Split (' ');
			var stringArgs = argsArr.Select((string arg) => Marshal.StringToHGlobalAuto (arg)).ToList();
			stringArgs.Add (IntPtr.Zero);

			var stringArgsArr = stringArgs.ToArray ();
			unsafe {
				var error = new LLDBSharp.Error();
				fixed (IntPtr* ptr = stringArgsArr) {
					process = target.Launch (debugger.GetListener (), (sbyte**)ptr, null, null, null, null, currentDir,
						0, false, error);

					if (process == null || error.ErrorCode != 0)
						throw new Exception (string.Format("Could not create LLDB process: {0}", error.CString));
				}
			}

			var state = process.State;
			Console.WriteLine ("Process state: {0}", state);

			for (uint threadIndex = 0; threadIndex < process.NumThreads; ++threadIndex) {
				var thread = process.GetThreadAtIndex (threadIndex);

				Console.WriteLine ("Stack trace");
				for (uint frameIndex = 0; frameIndex < thread.NumFrames; ++frameIndex) {
					var frame = thread.GetFrameAtIndex (frameIndex);
					var function = frame.GetFunction ();

					var symbol = frame.GetSymbol ();
					Console.WriteLine ("\t{0}", function.Name ?? symbol.Name);
				}
			}
		}

		protected override void OnSetActiveThread (long processId, long threadId)
		{
			process.SetSelectedThread (process.GetThreadByID ((ulong)threadId));
		}

		protected override void OnStop ()
		{
			process.Stop ();
		}

		protected override void OnContinue ()
		{
			threads = null;
			process.Continue ();
		}

		protected override void OnExit ()
		{
			process.Kill ();
		}

		protected override void OnStepLine ()
		{
			process.GetSelectedThread ().StepOver (LLDBSharp.RunMode.OnlyDuringStepping);
		}

		protected override void OnNextLine ()
		{
			process.GetSelectedThread ().StepOver (LLDBSharp.RunMode.OnlyDuringStepping);
		}

		protected override void OnStepInstruction ()
		{
			process.GetSelectedThread ().StepInstruction (false);
		}

		protected override void OnNextInstruction ()
		{
			process.GetSelectedThread ().StepInstruction (true);
		}

		protected override void OnFinish ()
		{
			process.GetSelectedThread ().StepOut ();
		}

		protected override BreakEventInfo OnInsertBreakEvent (BreakEvent breakEvent)
		{
			throw new NotImplementedException ();
		}

		protected override void OnRemoveBreakEvent (BreakEventInfo eventInfo)
		{
			throw new NotImplementedException ();
		}

		protected override void OnUpdateBreakEvent (BreakEventInfo eventInfo)
		{
			throw new NotImplementedException ();
		}

		protected override void OnEnableBreakEvent (BreakEventInfo eventInfo, bool enable)
		{
			throw new NotImplementedException ();
		}

		protected override ThreadInfo[] OnGetThreads (long processId)
		{
			if (threads == null) {
				threads = new ThreadInfo[process.NumThreads];
				for (uint i = 0; i < process.NumThreads; ++i) {
					var thread = process.GetThreadAtIndex (i);
					threads [i] = new ThreadInfo (processId, (long)thread.ThreadID, thread.Name, null);
				}
			}
			return threads;
		}

		protected override ProcessInfo[] OnGetProcesses ()
		{
			return new[] {
				new ProcessInfo ((long)process.ProcessID, process.PluginName),
			};
		}

		protected override Backtrace OnGetThreadBacktrace (long processId, long threadId)
		{
			throw new NotImplementedException ();
		}

		protected override void OnFetchFrames (ThreadInfo[] threads)
		{
			base.OnFetchFrames (threads);
		}

		public override void Dispose ()
		{
			base.Dispose ();

			if (disposed)
				return;

			disposed = true;

//			if (!HasExited)
//				EndLaunch ();

			if (!HasExited) {
			}

//			Adaptor.Dispose ();
		}

		#region MAYBE<SOMETIME>
		protected override void OnAttachToProcess (long processId)
		{
			throw new NotSupportedException ();
		}

		protected override void OnDetach ()
		{
			throw new NotSupportedException ();
		}

		public override bool CanCancelAsyncEvaluations {
			get {
				return base.CanCancelAsyncEvaluations;
			}
		}

		protected override void OnCancelAsyncEvaluations ()
		{
			base.OnCancelAsyncEvaluations ();
		}

		// This needs C/C++ integration.
		public override bool CanSetNextStatement {
			get {
				return base.CanSetNextStatement;
			}
		}

		protected override void OnSetNextStatement (long threadId, string fileName, int line, int column)
		{
			base.OnSetNextStatement (threadId, fileName, line, column);
		}

		protected override void OnSetNextStatement (long threadId, int ilOffset)
		{
			base.OnSetNextStatement (threadId, ilOffset);
		}

		protected override bool AllowBreakEventChanges {
			get {
				return true;
			}
		}

		protected override bool HandleException (Exception ex)
		{
			return base.HandleException (ex);
		}

		protected override AssemblyLine[] OnDisassembleFile (string file)
		{
			return base.OnDisassembleFile (file);
		}
		#endregion
	}
}

