using System;
using Mono.Debugging.Client;
using System.IO;
using LLDBSharp = LLDB;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Mono.Debugging.LLDB
{
	public class LLDBDebuggerSession : DebuggerSession
	{
		const string RelativeDebugServerPath = "Contents/SharedFrameworks/LLDB.framework/Versions/Current/Resources/debugserver";
		bool disposed;

		LLDBSharp.Process process;
		LLDBSharp.Target target;
		LLDBSharp.Debugger debugger;
		LLDBSharp.Broadcaster broadcaster;
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

		void NotifyStopped ()
		{
			OnTargetEvent (new TargetEventArgs (TargetEventType.TargetStopped) {
				Process = new ProcessInfo ((long)process.ProcessID, process.PluginName),
				Thread = GetThread (),
				Backtrace = OnGetThreadBacktrace ((long)process.ProcessID, (long)GetThread ().Id),
			});
		}

		unsafe bool OnBreakHit(IntPtr baton, IntPtr proc, IntPtr thread, IntPtr location)
		{
			OnTargetEvent (new TargetEventArgs (TargetEventType.TargetHitBreakpoint) {
				Process = new ProcessInfo ((long)process.ProcessID, process.PluginName),
				Thread = GetThread (),
				Backtrace = OnGetThreadBacktrace ((long)process.ProcessID, (long)GetThread ().Id),
			});
			return true;
		}
		 
		protected override void OnRun (DebuggerStartInfo startInfo)
		{
			//var info = (LLDBDebuggerStartInfo)startInfo;

			// TODO: Use info.Command.
			target = debugger.CreateTarget ("mono");
			if (target == null)
				throw new Exception ("Could not create LLDB target");

			// TODO: Make this app current dir.
			var currentDir = Directory.GetCurrentDirectory ();

			// TODO: Feex meee!
			startInfo.Arguments = "/Users/therzok/Work/debugger-libs/Mono.Debugging.LLDB/LLDBSharp/gmake/lib/Debug_x32/Managed.exe";

			// Be afraid, be very afraid. Also, freeme!
			var argsArr = startInfo.Arguments.Split (' ');
			var stringArgs = argsArr.Select((string arg) => Marshal.StringToHGlobalAuto (arg)).ToList();
			stringArgs.Add (IntPtr.Zero);

			var stringArgsArr = stringArgs.ToArray ();
			unsafe {
				var error = new LLDBSharp.Error();
				fixed (IntPtr* ptr = stringArgsArr) {
					process = target.Launch (debugger.GetListener (), (sbyte**)ptr, null, null, null, null, currentDir,
						0, true, error);

					if (process == null || error.ErrorCode != 0)
						throw new Exception (string.Format("Could not create LLDB process: {0}", error.CString));

					OnStarted ();

					broadcaster = process.GetBroadcaster ();
					Task.Run (() => {
						LLDBSharp.Event ev = new LLDBSharp.Event ();
						var listener = new LLDBSharp.Listener ("Process State Listener");
						broadcaster.AddListener (listener, (uint)LLDBSharp.Process.BroadcastBit.BroadcastBitStateChanged);
						while (true) {
							listener.WaitForEventForBroadcasterWithType (200,
								broadcaster,
								(uint)LLDBSharp.Process.BroadcastBit.BroadcastBitStateChanged,
								ev);

							if (process.State == LLDBSharp.StateType.Exited)
								break;
						}
						OnTargetEvent (new TargetEventArgs (TargetEventType.TargetExited) {
							ExitCode = process.ExitStatus,
						});
					});
					process.Continue ();
				}
			}
		}

		ThreadInfo GetThread ()
		{
			process.GetSelectedThread ().Resume ();
			return new ThreadInfo ((long)process.ProcessID, (long)process.GetSelectedThread ().ThreadID, process.GetSelectedThread ().Name, null);
		}

		protected override void OnSetActiveThread (long processId, long threadId)
		{
			process.SetSelectedThreadByID ((ulong)threadId);
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
			process.GetSelectedThread ().StepInto (LLDBSharp.RunMode.OnlyDuringStepping);
			NotifyStopped ();
		}

		protected override void OnNextLine ()
		{
			process.GetSelectedThread ().StepOver (LLDBSharp.RunMode.OnlyDuringStepping);
			NotifyStopped ();
		}

		protected override void OnStepInstruction ()
		{
			process.GetSelectedThread ().StepInstruction (false);
			NotifyStopped ();
		}

		protected override void OnNextInstruction ()
		{
			process.GetSelectedThread ().StepInstruction (true);
			NotifyStopped ();
		}

		protected override void OnFinish ()
		{
			process.GetSelectedThread ().StepOut ();
			NotifyStopped ();
		}

		class UnmanagedBreakInfo : BreakEventInfo
		{
			public LLDBSharp.Breakpoint Breakpoint;
		}

		LLDBSharp.Breakpoint CreateBreakpoint (BreakEvent breakEvent)
		{
			var fb = breakEvent as FunctionBreakpoint;
			LLDBSharp.Breakpoint bp;
			if (fb != null)
				bp = target.BreakpointCreateByName (fb.FunctionName, null);
			else {
				var lb = breakEvent as Breakpoint;
				if (lb != null)
					bp = target.BreakpointCreateByLocation (lb.FileName, (uint)lb.Line);
				else
					// TODO: Tracepoints.
					throw new NotImplementedException ();
			}

			// TODO: Conditions.
			bp.Enabled = breakEvent.Enabled;
			bp.SetCallback (OnBreakHit, IntPtr.Zero);

			return bp;
		}

		protected override BreakEventInfo OnInsertBreakEvent (BreakEvent breakEvent)
		{
			if (process.State == LLDBSharp.StateType.Exited) {
				var bi = new UnmanagedBreakInfo ();
				bi.SetStatus (BreakEventStatus.Disconnected, null);
				return bi;
			}

			return new UnmanagedBreakInfo {
				Breakpoint = CreateBreakpoint (breakEvent),
			};
		}

		protected override void OnRemoveBreakEvent (BreakEventInfo eventInfo)
		{
			var info = (UnmanagedBreakInfo)eventInfo;
			target.BreakpointDelete (info.Breakpoint.ID);
			info.Breakpoint.Dispose ();
		}

		protected override void OnUpdateBreakEvent (BreakEventInfo eventInfo)
		{
			var info = (UnmanagedBreakInfo)eventInfo;
			target.BreakpointDelete (info.Breakpoint.ID);
			info.Breakpoint.Dispose ();

			info.Breakpoint = CreateBreakpoint (eventInfo.BreakEvent);
		}

		protected override void OnEnableBreakEvent (BreakEventInfo eventInfo, bool enable)
		{
			var info = (UnmanagedBreakInfo)eventInfo;
			info.Breakpoint.Enabled = enable;
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
			return new Backtrace (new LLDBDebuggerBacktrace (this, process.GetThreadByID ((ulong)threadId)));
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

			if (process.State != LLDBSharp.StateType.Stopped)
				process.Stop ();

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

