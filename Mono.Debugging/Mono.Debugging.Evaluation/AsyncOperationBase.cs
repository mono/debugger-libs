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
		void AfterCancelled (int elapsedAfterCancelMs);
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

		public void AfterCancelled (int elapsedAfterCancelMs)
		{
			try {
				AfterCancelledImpl (elapsedAfterCancelMs);
			}
			catch (Exception e) {
				DebuggerLoggingService.LogError ("AfterCancelledImpl() thrown an exception", e);
			}
		}

		protected abstract void AfterCancelledImpl (int elapsedAfterCancelMs);

		public Task<OperationResult<TValue>> InvokeAsync (CancellationToken token)
		{
			if (Task != null) throw new Exception("Task must be null");

			token.Register (() => {
				try {
					CancelImpl ();
				}
				catch (OperationCanceledException) {
					// if CancelImpl throw OCE we shouldn't mute it
					throw;
				}
				catch (Exception e) {
					DebuggerLoggingService.LogMessage ("Exception in CancelImpl(): {0}", e.Message);
				}
			});
			Task = InvokeAsyncImpl (token);
			return Task;
		}
		protected abstract Task<OperationResult<TValue>> InvokeAsyncImpl (CancellationToken token);

		protected abstract void CancelImpl ();

	}
}