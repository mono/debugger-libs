using System;
using Mono.Debugging.Client;
using Mono.Debugging.LLDB;
using Mono.Debugging.Soft;

namespace Mono.Debugging.Mixed
{
	public class MixedDebuggerSession : DebuggerSession
	{
		SoftDebuggerSession sdb = new SoftDebuggerSession ();
		LLDBDebuggerSession lldb = new LLDBDebuggerSession ();
		bool Managed = true;

		public MixedDebuggerSession ()
		{
		}

		protected override string OnResolveExpression (string expression, SourceLocation location)
		{
			if (Managed)
				return sdb.ResolveExpression (expression, location);
			return string.Empty;
		}

		protected override void OnRun (DebuggerStartInfo startInfo)
		{
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
	}
}

