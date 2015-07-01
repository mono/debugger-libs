using System;
using Mono.Debugging.Client;

namespace Mono.Debugging.LLDB
{
	public class LLDBDebuggerSession : DebuggerSession
	{
		bool disposed;

		public LLDBDebuggerSession ()
		{

		}

		protected override string OnResolveExpression (string expression, SourceLocation location)
		{
			return base.OnResolveExpression (expression, location);
		}

		protected override void OnRun (DebuggerStartInfo startInfo)
		{
			throw new NotImplementedException ();
		}

		protected override void OnAttachToProcess (long processId)
		{
			throw new NotImplementedException ();
		}

		protected override void OnDetach ()
		{
			throw new NotImplementedException ();
		}

		protected override void OnSetActiveThread (long processId, long threadId)
		{
			throw new NotImplementedException ();
		}

		protected override void OnStop ()
		{
			throw new NotImplementedException ();
		}

		protected override void OnExit ()
		{
			throw new NotImplementedException ();
		}

		protected override void OnStepLine ()
		{
			throw new NotImplementedException ();
		}

		protected override void OnNextLine ()
		{
			throw new NotImplementedException ();
		}

		protected override void OnStepInstruction ()
		{
			throw new NotImplementedException ();
		}

		protected override void OnNextInstruction ()
		{
			throw new NotImplementedException ();
		}

		protected override void OnFinish ()
		{
			throw new NotImplementedException ();
		}

		protected override void OnContinue ()
		{
			throw new NotImplementedException ();
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
			throw new NotImplementedException ();
		}

		protected override ProcessInfo[] OnGetProcesses ()
		{
			throw new NotImplementedException ();
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

		// Maybe?
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
	}
}

