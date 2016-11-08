namespace Mono.Debugging.Evaluation
{
	public interface IAsyncOperation
	{
		/// <summary>
		/// Called to invoke the operation. The execution must be asynchronous (it must return immediatelly).
		/// </summary>
		void BeginInvoke ();

		/// <summary>
		/// Called to abort the execution of the operation. It has to throw an exception
		/// if the operation can't be aborted. This operation must not block. The engine
		/// will wait for the operation to be aborted by calling WaitForCompleted.
		/// </summary>
		void Abort ();

		/// <summary>
		/// Waits until the operation has been completed or aborted.
		/// </summary>
		bool WaitForCompleted (int timeout);
	}
}