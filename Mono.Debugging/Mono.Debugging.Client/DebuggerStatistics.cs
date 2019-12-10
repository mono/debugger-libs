//
// DebuggerStatistics.cs
//
// Author:
//       Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2019 Microsoft Corp.
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
using System.Diagnostics;
using System.Collections.Generic;

namespace Mono.Debugging.Client
{
	public class DebuggerStatistics
	{
		static readonly int[] UpperTimeLimits = { 1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024, 2048 };

		readonly int[] buckets = new int[UpperTimeLimits.Length + 1];
		readonly object mutex = new object ();
		TimeSpan minTime = TimeSpan.MaxValue;
		TimeSpan maxTime, totalTime;

		public double MaxTime {
			get { return maxTime.TotalMilliseconds; }
		}

		public double MinTime {
			get { return minTime.TotalMilliseconds; }
		}

		public double AverageTime {
			get {
				if (TimingsCount > 0)
					return totalTime.TotalMilliseconds / TimingsCount;

				return 0;
			}
		}

		public int TimingsCount { get; private set; }

		public int FailureCount { get; private set; }

		public DebuggerTimer StartTimer ()
		{
			var timer = new DebuggerTimer (this);
			timer.Start ();
			return timer;
		}

		int GetBucketIndex (TimeSpan duration)
		{
			var ms = (long) duration.TotalMilliseconds;

			for (var bucket = 0; bucket < UpperTimeLimits.Length; bucket++) {
				if (ms <= UpperTimeLimits[bucket])
					return bucket;
			}

			return buckets.Length - 1;
		}

		public void AddTime (TimeSpan duration)
		{
			lock (mutex) {
				if (duration > maxTime)
					maxTime = duration;

				if (duration < minTime)
					minTime = duration;

				buckets[GetBucketIndex (duration)]++;

				totalTime += duration;
				TimingsCount++;
			}
		}

		public void IncrementFailureCount ()
		{
			lock (mutex) {
				FailureCount++;
			}
		}

		public void Serialize (Dictionary<string, object> metadata)
		{
			metadata["AverageDuration"] = AverageTime;
			metadata["MaximumDuration"] = MaxTime;
			metadata["MinimumDuration"] = MinTime;
			metadata["FailureCount"] = FailureCount;
			metadata["SuccessCount"] = TimingsCount;

			for (int i = 0; i < buckets.Length; i++)
				metadata[$"Bucket{i}"] = buckets[i];
		}
	}

	public class DebuggerTimer : IDisposable
	{
		readonly DebuggerStatistics stats;
		readonly Stopwatch stopwatch;

		public DebuggerTimer (DebuggerStatistics stats)
		{
			stopwatch = new Stopwatch ();
			this.stats = stats;
		}

		/// <summary>
		/// Indicates if the debugger operation was successful. If this is false the
		/// timing will not be reported and a failure will be indicated.
		/// </summary>
		public bool Success { get; set; }

		public TimeSpan Elapsed {
			get { return stopwatch.Elapsed; }
		}

		public void Start ()
		{
			stopwatch.Start ();
		}

		public void Stop (bool success)
		{
			stopwatch.Stop ();

			Success = success;

			if (stats == null)
				return;

			if (success)
				stats.AddTime (stopwatch.Elapsed);
			else
				stats.IncrementFailureCount ();
		}

		public void Stop (ObjectValue val)
		{
			stopwatch.Stop ();

			if (stats == null)
				return;

			if (val.IsEvaluating || val.IsEvaluatingGroup) {
				// Do not capture timing - evaluation not finished.
			} else if (val.IsError || val.IsImplicitNotSupported || val.IsNotSupported || val.IsUnknown) {
				stats.IncrementFailureCount ();
			} else {
				// Success
				stats.AddTime (stopwatch.Elapsed);
			}
		}

		public void Dispose ()
		{
			if (stopwatch.IsRunning) {
				stopwatch.Stop ();

				if (stats == null)
					return;

				if (Success)
					stats.AddTime (stopwatch.Elapsed);
				else
					stats.IncrementFailureCount ();
			}
		}
	}
}
