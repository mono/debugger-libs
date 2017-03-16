using System.Threading;
using System.Threading.Tasks;
using Microsoft.Samples.Debugging.CorDebug;
using Microsoft.Samples.Debugging.CorDebug.NativeApi;
using Mono.Debugging.Evaluation;

namespace Mono.Debugging.Win32
{
	class CorMethodCall: AsyncOperationBase<CorValue>
	{
		readonly CorEvaluationContext context;
		readonly CorFunction function;
		readonly CorType[] typeArgs;
		readonly CorValue[] args;

		readonly CorEval eval;

		public CorMethodCall (CorEvaluationContext context, CorFunction function, CorType[] typeArgs, CorValue[] args)
		{
			this.context = context;
			this.function = function;
			this.typeArgs = typeArgs;
			this.args = args;
			eval = context.Eval;
		}

		void ProcessOnEvalComplete (object sender, CorEvalEventArgs evalArgs)
		{
			DoProcessEvalFinished (evalArgs, false);
		}

		void ProcessOnEvalException (object sender, CorEvalEventArgs evalArgs)
		{
			DoProcessEvalFinished (evalArgs, true);
		}

		void DoProcessEvalFinished (CorEvalEventArgs evalArgs, bool isException)
		{
			if (evalArgs.Eval != eval)
				return;
			context.Session.OnEndEvaluating ();
			evalArgs.Continue = false;
			tcs.TrySetResult(new OperationResult<CorValue> (evalArgs.Eval.Result, isException));
		}

		void SubscribeOnEvals ()
		{
			context.Session.Process.OnEvalComplete += ProcessOnEvalComplete;
			context.Session.Process.OnEvalException += ProcessOnEvalException;
		}

		void UnSubcribeOnEvals ()
		{
			context.Session.Process.OnEvalComplete -= ProcessOnEvalComplete;
			context.Session.Process.OnEvalException -= ProcessOnEvalException;
		}

		public override string Description
		{
			get
			{
				var met = function.GetMethodInfo (context.Session);
				if (met == null)
					return "<Unknown>";
				if (met.DeclaringType == null)
					return met.Name;
				return met.DeclaringType.FullName + "." + met.Name;
			}
		}

		readonly TaskCompletionSource<OperationResult<CorValue>> tcs = new TaskCompletionSource<OperationResult<CorValue>> ();
		const int DelayAfterAbort = 500;

		protected override void AfterCancelledImpl (int elapsedAfterCancelMs)
		{
			if (tcs.TrySetCanceled ()) {
				// really cancelled for the first time not before. so we should check that we awaited necessary amout of time after Abort() call
				// else if we return too earle after Abort() the process may be PROCESS_NOT_SYNCHRONIZED
				if (elapsedAfterCancelMs < DelayAfterAbort) {
					Thread.Sleep (DelayAfterAbort - elapsedAfterCancelMs);
				}
			}
			context.Session.OnEndEvaluating ();
		}

		protected override Task<OperationResult<CorValue>> InvokeAsyncImpl (CancellationToken token)
		{
			SubscribeOnEvals ();

			if (function.GetMethodInfo (context.Session).Name == ".ctor")
				eval.NewParameterizedObject (function, typeArgs, args);
			else
				eval.CallParameterizedFunction (function, typeArgs, args);
			context.Session.Process.SetAllThreadsDebugState (CorDebugThreadState.THREAD_SUSPEND, context.Thread);
			context.Session.ClearEvalStatus ();
			context.Session.OnStartEvaluating ();
			context.Session.Process.Continue (false);
			Task = tcs.Task;
			// Don't pass token here, because it causes immediately task cancellation which must be performed by debugger event or real timeout 
			// ReSharper disable once MethodSupportsCancellation
			return Task.ContinueWith (task => {
				UnSubcribeOnEvals ();
				return task.Result;
			});
		}


		protected override void CancelImpl ( )
		{
			eval.Abort ();
		}
	}
}
