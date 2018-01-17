//
// EvaluationStatistics.cs
//
// Author:
//       Matt Ward <matt.ward@microsoft.com>
//
// Copyright (c) 2018 Microsoft
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

namespace Mono.Debugging.Evaluation
{
	public class EvaluationStatistics
	{
		object lockObject = new object ();
		TimeSpan maxTime;
		TimeSpan minTime = TimeSpan.MaxValue;
		TimeSpan totalTime;
		int timingsCount;
		int failureCount;

		public int FailureCount {
			get { return failureCount; }
		}

		public double MaxTime {
			get { return maxTime.TotalMilliseconds; }
		}

		public double MinTime {
			get { return minTime.TotalMilliseconds; }
		}

		public double AverageTime {
			get {
				if (timingsCount > 0) {
					return totalTime.TotalMilliseconds / timingsCount;
				}
				return 0;
			}
		}

		public int TimingsCount {
			get { return timingsCount; }
		}

		public void AddTime (TimeSpan duration)
		{
			lock (lockObject) {
				if (duration > maxTime) {
					maxTime = duration;
				}

				if (duration < minTime) {
					minTime = duration;
				}

				totalTime += duration;
				timingsCount++;
			}
		}

		public void IncrementFailureCount ()
		{
			lock (lockObject) {
				failureCount++;
			}
		}
	}
}
