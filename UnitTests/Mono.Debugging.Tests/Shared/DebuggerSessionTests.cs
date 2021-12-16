// 
// DebuggerSessionTests.cs
//  
// Author:
//       Greg Munn <gregm@microsoft.com>
// 
// Copyright (c) 2019 Microsoft
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Threading;
using System.Threading.Tasks;

using Mono.Debugging.Client;

using NUnit.Framework;

namespace Mono.Debugging.Tests
{
	[TestFixture]
	public class DebuggerSessionTests
	{
		TestDebuggerSession session;

		[OneTimeSetUp]
		public virtual void SetUp ()
		{
			session = new TestDebuggerSession ();
		}

		[OneTimeTearDown]
		public virtual void TearDown ()
		{
			session.Dispose ();
		}

		[Test]
#pragma warning disable VSTHRD200 // Use "Async" suffix for async methods
		public async Task WhenQueryigBreakpoints_ThenDoNotDeadlockDispatchedMethods()
#pragma warning restore VSTHRD200 // Use "Async" suffix for async methods
		{
			// thread 1 cancels debugging, which triggers hot reload to clear breakpoints 
			// thread 2 dispatches the OnExit method which is call via Dispatch and uses the same lock

			var thread1 = new TaskCompletionSource<bool> ();
			Task thread2 = null;

			session.HandleOnExit = () => {
				// we are on a background thread via session.Dispatch
				thread2 = Task.Run (() => {
					var bk = session.Breakpoints;
					thread1.TrySetResult (true);
				});

				// mimic the cause of the deadlock by waiting here for a while
				// we don't really want to deadlock, just long enough that we should have
				// completed the call to breakpoints first.
				Thread.Sleep (1000);

				thread1.TrySetResult (false);
			};

			session.Exit ();

			var result= await thread1.Task;
			await thread2;

			Assert.IsTrue (result, "the call to get breakpoints should have completed first");
		}

		class TestDebuggerSession : DebuggerSession
		{
			public Action HandleOnExit;

			protected override void OnAttachToProcess (long processId)
			{
				throw new NotImplementedException ();
			}

			protected override void OnContinue ()
			{
				throw new NotImplementedException ();
			}

			protected override void OnDetach ()
			{
				throw new NotImplementedException ();
			}

			protected override void OnEnableBreakEvent (BreakEventInfo eventInfo, bool enable)
			{
				throw new NotImplementedException ();
			}

			protected override void OnExit ()
			{
				// this is called in dispatch
				HandleOnExit?.Invoke ();
				//throw new NotImplementedException ();
			}

			protected override void OnFinish ()
			{
				throw new NotImplementedException ();
			}

			protected override ProcessInfo [] OnGetProcesses ()
			{
				throw new NotImplementedException ();
			}

			protected override Backtrace OnGetThreadBacktrace (long processId, long threadId)
			{
				throw new NotImplementedException ();
			}

			protected override ThreadInfo [] OnGetThreads (long processId)
			{
				throw new NotImplementedException ();
			}

			protected override BreakEventInfo OnInsertBreakEvent (BreakEvent breakEvent)
			{
				throw new NotImplementedException ();
			}

			protected override void OnNextInstruction ()
			{
				throw new NotImplementedException ();
			}

			protected override void OnNextLine ()
			{
				throw new NotImplementedException ();
			}

			protected override void OnRemoveBreakEvent (BreakEventInfo eventInfo)
			{
				throw new NotImplementedException ();
			}

			protected override void OnRun (DebuggerStartInfo startInfo)
			{
				throw new NotImplementedException ();
			}

			protected override void OnSetActiveThread (long processId, long threadId)
			{
				throw new NotImplementedException ();
			}

			protected override void OnStepInstruction ()
			{
				throw new NotImplementedException ();
			}

			protected override void OnStepLine ()
			{
				throw new NotImplementedException ();
			}

			protected override void OnStop ()
			{
				throw new NotImplementedException ();
			}

			protected override void OnUpdateBreakEvent (BreakEventInfo eventInfo)
			{
				throw new NotImplementedException ();
			}
		}
	}
}
