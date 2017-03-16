using System.Threading.Tasks;
using Microsoft.Samples.Debugging.CorDebug;
using Microsoft.Samples.Debugging.CorDebug.NativeApi;
using Mono.Debugging.Client;
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
			if (Token.IsCancellationRequested) {
				DebuggerLoggingService.LogMessage ("EvalFinished() but evaluation was cancelled");
				tcs.TrySetCanceled ();
			}
			else {
				DebuggerLoggingService.LogMessage ("EvalFinished(). Setting the result");
				tcs.TrySetResult(new OperationResult<CorValue> (evalArgs.Eval.Result, isException));
			}
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
					return "[Unknown method]";
				if (met.DeclaringType == null)
					return met.Name;
				return met.DeclaringType.FullName + "." + met.Name;
			}
		}

		readonly TaskCompletionSource<OperationResult<CorValue>> tcs = new TaskCompletionSource<OperationResult<CorValue>> ();

		protected override Task<OperationResult<CorValue>> InvokeAsyncImpl ()
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
			return Task.ContinueWith (task => {
				UnSubcribeOnEvals ();
				return task.Result;
			});
		}


		protected override void AbortImpl (int abortCallTimes)
		{
			if (abortCallTimes < 10) {
				DebuggerLoggingService.LogMessage ("Calling Abort() for {0} time", abortCallTimes);
				eval.Abort ();
			}
			else {
				if (abortCallTimes == 20) {
					// if Abort() and RudeAbort() didn't bring any result let's try to resume all the threads to free possible deadlocks in target process
					// maybe this can help to abort hanging evaluations
					DebuggerLoggingService.LogMessage ("RudeAbort() didn't stop eval after {0} times", abortCallTimes - 1);
					DebuggerLoggingService.LogMessage ("Calling Stop()");
					context.Session.Process.Stop (0);
					DebuggerLoggingService.LogMessage ("Calling SetAllThreadsDebugState(THREAD_RUN)");
					context.Session.Process.SetAllThreadsDebugState (CorDebugThreadState.THREAD_RUN, null);
					DebuggerLoggingService.LogMessage ("Calling Continue()");
					context.Session.Process.Continue (false);
				}
				DebuggerLoggingService.LogMessage ("Calling RudeAbort() for {0} time", abortCallTimes);
				eval.RudeAbort();
			}
		}
	}
}
