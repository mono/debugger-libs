using System;
using System.Threading;
using System.Threading.Tasks;
using Mono.Debugging.Client;

namespace Mono.Debugging.Evaluation
{
	public class OperationResult<TValue>
	{
		public TValue Result { get; private set; }
		public bool ResultIsException { get; private set; }

		public OperationResult (TValue result, bool resultIsException)
		{
			Result = result;
			ResultIsException = resultIsException;
		}
	}

	public static class OperationResultEx
	{
		public static OperationResult<TValue> ThrowIfException<TValue> (this OperationResult<TValue> result, EvaluationContext ctx)
		{
			if (!result.ResultIsException)
				return result;
			var exceptionTypeName = ctx.Adapter.GetValueTypeName (ctx, result.Result);
			throw new EvaluatorExceptionThrownException (result.Result, exceptionTypeName);
		}
	}

	public interface IAsyncOperationBase
	{
		Task RawTask { get; }
		string Description { get; }
		void Abort ();
	}

	public abstract class AsyncOperationBase<TValue> : IAsyncOperationBase
	{
		public Task<OperationResult<TValue>> Task { get; protected set; }

		public Task RawTask
		{
			get
			{
				return Task;
			}
		}

		public abstract string Description { get; }

		int abortCalls = 0;

		readonly CancellationTokenSource tokenSource = new CancellationTokenSource ();

		/// <summary>
		/// When evaluation is aborted and debugger callback is invoked the implementation has to check
		/// for Token.IsCancellationRequested and call Task.SetCancelled() instead of setting the result
		/// </summary>
		protected CancellationToken Token { get { return tokenSource.Token; } }

		public void Abort ()
		{
			try {
				tokenSource.Cancel();
				AbortImpl (Interlocked.Increment (ref abortCalls) - 1);
			}
			catch (OperationCanceledException) {
				// if CancelImpl throw OCE we shouldn't mute it
				throw;
			}
			catch (Exception e) {
				DebuggerLoggingService.LogMessage ("Exception in CancelImpl(): {0}", e.Message);
			}
		}

		public Task<OperationResult<TValue>> InvokeAsync ()
		{
			if (Task != null) throw new Exception("Task must be null");
			Task = InvokeAsyncImpl ();
			return Task;
		}

		protected abstract Task<OperationResult<TValue>> InvokeAsyncImpl ();

		/// <summary>
		/// The implementation has to tell the debugger to abort the evaluation. This method must bot block.
		/// </summary>
		/// <param name="abortCallTimes">indicates how many times this method has been already called for this evaluation.
		/// E.g. the implementation can perform some 'rude abort' after several previous ordinary 'aborts' were failed. For the first call this parameter == 0</param>
		protected abstract void AbortImpl (int abortCallTimes);

	}
}