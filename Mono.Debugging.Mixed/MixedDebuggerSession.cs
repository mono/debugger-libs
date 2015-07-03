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
			return lldb.ResolveExpression (expression, location);
		}

		protected override void OnRun (DebuggerStartInfo startInfo)
		{
		}

		protected override void OnSetActiveThread (long processId, long threadId)
		{
		}

		protected override void OnStop ()
		{
		}

		protected override void OnContinue ()
		{
		}

		protected override void OnExit ()
		{
		}

		protected override void OnStepLine ()
		{
		}

		protected override void OnNextLine ()
		{
		}

		protected override void OnStepInstruction ()
		{
		}

		protected override void OnNextInstruction ()
		{
		}

		protected override void OnFinish ()
		{
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
		}

		protected override void OnAttachToProcess (long processId)
		{
			throw new NotImplementedException ();
		}

		protected override void OnDetach ()
		{
			throw new NotImplementedException ();
		}
	}
}

