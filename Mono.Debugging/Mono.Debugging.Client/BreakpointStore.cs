// BreakpointStore.cs
//
// Author:
//   Lluis Sanchez Gual <lluis@novell.com>
//
// Copyright (c) 2008 Novell, Inc (http://www.novell.com)
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
//
//

using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;

namespace Mono.Debugging.Client
{
	public sealed class BreakpointStore: ICollection<BreakEvent>
	{
		static readonly StringComparer PathComparer;
		static readonly bool IsWindows;
		static readonly bool IsMac;

		static BreakpointStore ()
		{
			IsWindows = Path.DirectorySeparatorChar == '\\';
			IsMac = !IsWindows && IsRunningOnMac ();

			PathComparer = IsWindows || IsMac ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
		}

		[DllImport ("libc")]
		static extern int uname (IntPtr buf);

		//From Managed.Windows.Forms/XplatUI
		static bool IsRunningOnMac ()
		{
			IntPtr buf = IntPtr.Zero;
			try {
				buf = Marshal.AllocHGlobal (8192);
				// This is a hacktastic way of getting sysname from uname ()
				if (uname (buf) == 0) {
					string os = Marshal.PtrToStringAnsi (buf);
					if (os == "Darwin")
						return true;
				}
			} catch {
			} finally {
				if (buf != IntPtr.Zero)
					Marshal.FreeHGlobal (buf);
			}
			return false;
		}

		readonly List<BreakEvent> breakpoints = new List<BreakEvent> ();
		
		public int Count {
			get {
				return breakpoints.Count;
			}
		}

		public bool IsReadOnly {
			get {
				var args = new ReadOnlyCheckEventArgs ();
				var checkingReadOnly = CheckingReadOnly;
				if (checkingReadOnly != null)
					checkingReadOnly (this, args);
				return args.IsReadOnly;
			}
		}

		public Breakpoint Add (string filename, int line, int column)
		{
			return Add (filename, line, column, true);
		}

		public Breakpoint Add (string filename, int line)
		{
			return Add (filename, line, 1, true);
		}
		
		public Breakpoint Add (string filename, int line, int column, bool activate)
		{
			if (filename == null)
				throw new ArgumentNullException ("filename");

			if (line < 1)
				throw new ArgumentOutOfRangeException ("line");

			if (column < 1)
				throw new ArgumentOutOfRangeException ("column");

			if (IsReadOnly)
				return null;

			var bp = new Breakpoint (filename, line, column);
			Add (bp);

			return bp;
		}

		void ICollection<BreakEvent>.Add (BreakEvent bp)
		{
			Add (bp);
		}
		
		public bool Add (BreakEvent bp)
		{
			if (bp == null)
				throw new ArgumentNullException ("bp");

			if (IsReadOnly)
				return false;

			breakpoints.Add (bp);
			bp.Store = this;
			OnBreakEventAdded (bp);

			return true;
		}
		
		public Catchpoint AddCatchpoint (string exceptionName)
		{
			if (exceptionName == null)
				throw new ArgumentNullException ("exceptionName");

			if (IsReadOnly)
				return null;

			var cp = new Catchpoint (exceptionName);
			Add (cp);

			return cp;
		}
		
		public bool Remove (string filename, int line, int column)
		{
			if (filename == null)
				throw new ArgumentNullException ("filename");

			if (IsReadOnly)
				return false;

			filename = Path.GetFullPath (filename);
			
			for (int n = 0; n < breakpoints.Count; n++) {
				var bp = breakpoints[n] as Breakpoint;

				if (bp != null && FileNameEquals (bp.FileName, filename) &&
				    (bp.OriginalLine == line || bp.Line == line) &&
				    (bp.OriginalColumn == column || bp.Column == column)) {
					breakpoints.RemoveAt (n);
					OnBreakEventRemoved (bp);
					n--;
				}
			}
			return true;
		}
		
		public bool RemoveCatchpoint (string exceptionName)
		{
			if (exceptionName == null)
				throw new ArgumentNullException ("exceptionName");

			if (IsReadOnly)
				return false;

			for (int n = 0; n < breakpoints.Count; n++) {
				var cp = breakpoints[n] as Catchpoint;

				if (cp != null && cp.ExceptionName == exceptionName) {
					breakpoints.RemoveAt (n);
					OnBreakEventRemoved (cp);
					n--;
				}
			}
			return true;
		}

		public void RemoveRunToCursorBreakpoints ()
		{
			if (IsReadOnly)
				return;

			for (int n = 0; n < breakpoints.Count; n++) {
				var bp = breakpoints[n] as RunToCursorBreakpoint;

				if (bp != null) {
					breakpoints.RemoveAt (n);
					OnBreakEventRemoved (bp);
					n--;
				}
			}
		}
		
		public bool Remove (BreakEvent bp)
		{
			if (bp == null)
				throw new ArgumentNullException ("bp");

			if (!IsReadOnly && breakpoints.Remove (bp)) {
				OnBreakEventRemoved (bp);
				return true;
			}

			return false;
		}
		
		public Breakpoint Toggle (string filename, int line, int column)
		{
			if (filename == null)
				throw new ArgumentNullException ("filename");

			if (line < 1)
				throw new ArgumentOutOfRangeException ("line");

			if (column < 1)
				throw new ArgumentOutOfRangeException ("column");

			if (IsReadOnly)
				return null;
			
			var col = GetBreakpointsAtFileLine (filename, line);
			if (col.Count > 0) {
				// Remove only the most-recently-added breakpoint on the specified line
				Remove (col[col.Count - 1]);
				return null;
			}

			return Add (filename, line, column);
		}

		public ReadOnlyCollection<BreakEvent> GetBreakevents ()
		{
			return breakpoints.AsReadOnly ();
		}

		public ReadOnlyCollection<Breakpoint> GetBreakpoints ()
		{
			var list = new List<Breakpoint> ();

			foreach (var bp in breakpoints.OfType<Breakpoint> ()) {
				if (!(bp is RunToCursorBreakpoint))
					list.Add (bp);
			}

			return list.AsReadOnly ();
		}
		
		public ReadOnlyCollection<Catchpoint> GetCatchpoints ()
		{
			return breakpoints.OfType<Catchpoint> ().ToList ().AsReadOnly ();
		}
		
		public ReadOnlyCollection<Breakpoint> GetBreakpointsAtFile (string filename)
		{
			if (filename == null)
				throw new ArgumentNullException ("filename");

			var list = new List<Breakpoint> ();
			
			try {
				filename = Path.GetFullPath (filename);
			} catch {
				return list.AsReadOnly ();
			}
			
			foreach (var bp in breakpoints.OfType<Breakpoint> ()) {
				if (!(bp is RunToCursorBreakpoint) && FileNameEquals (bp.FileName, filename))
					list.Add (bp);
			}
			
			return list.AsReadOnly ();
		}
		
		public ReadOnlyCollection<Breakpoint> GetBreakpointsAtFileLine (string filename, int line)
		{
			if (filename == null)
				throw new ArgumentNullException ("filename");

			var list = new List<Breakpoint> ();
			
			try {
				filename = Path.GetFullPath (filename);
			} catch {
				return list.AsReadOnly ();
			}
			
			foreach (var bp in breakpoints.OfType<Breakpoint> ()) {
				if (!(bp is RunToCursorBreakpoint) && FileNameEquals (bp.FileName, filename) && (bp.OriginalLine == line || bp.Line == line))
					list.Add (bp);
			}
			
			return list.AsReadOnly ();
		}

		public IEnumerator GetEnumerator ()
		{
			return breakpoints.GetEnumerator ();
		}

		IEnumerator<BreakEvent> IEnumerable<BreakEvent>.GetEnumerator ()
		{
			return breakpoints.GetEnumerator ();
		}

		public void Clear ()
		{
			var oldList = new List<BreakEvent> (breakpoints);

			foreach (BreakEvent bp in oldList)
				Remove (bp);
		}

		public void ClearBreakpoints ()
		{
			foreach (var bp in GetBreakpoints ())
				Remove (bp);
		}

		public void ClearCatchpoints ()
		{
			foreach (var bp in GetCatchpoints ())
				Remove (bp);
		}

		public bool Contains (BreakEvent item)
		{
			return breakpoints.Contains (item);
		}

		public void CopyTo (BreakEvent[] array, int arrayIndex)
		{
			breakpoints.CopyTo (array, arrayIndex);
		}
		
		public void UpdateBreakpointLine (Breakpoint bp, int newLine)
		{
			if (IsReadOnly)
				return;
			
			bp.SetLine (newLine);
			NotifyBreakEventChanged (bp);
		}
		
		internal void AdjustBreakpointLine (Breakpoint bp, int newLine, int newColumn)
		{
			if (IsReadOnly)
				return;

			bp.SetAdjustedColumn (newColumn);
			bp.SetAdjustedLine (newLine);
			NotifyBreakEventChanged (bp);
		}
		
		internal void ResetBreakpoints ()
		{
			if (IsReadOnly)
				return;
			
			foreach (var bp in breakpoints.ToArray ()) {
				if (bp.Reset ())
					NotifyBreakEventChanged (bp);
			}
		}
		
		public XmlElement Save ()
		{
			XmlDocument doc = new XmlDocument ();
			XmlElement elem = doc.CreateElement ("BreakpointStore");
			foreach (BreakEvent ev in this) {
				XmlElement be = ev.ToXml (doc);
				elem.AppendChild (be);
			}
			return elem;
		}
		
		public void Load (XmlElement rootElem)
		{
			Clear ();
			foreach (XmlNode n in rootElem.ChildNodes) {
				XmlElement elem = n as XmlElement;
				if (elem == null)
					continue;
				BreakEvent ev = BreakEvent.FromXml (elem);
				if (ev != null)
					Add (ev);
			}
		}

		[DllImport ("libc")]
		static extern IntPtr realpath (string path, IntPtr buffer);

		static string ResolveFullPath (string path)
		{
			if (IsWindows)
				return Path.GetFullPath (path);

			const int PATHMAX = 4096 + 1;
			IntPtr buffer = IntPtr.Zero;

			try {
				buffer = Marshal.AllocHGlobal (PATHMAX);
				var result = realpath (path, buffer);
				return result == IntPtr.Zero ? "" : Marshal.PtrToStringAuto (buffer);
			} finally {
				if (buffer != IntPtr.Zero)
					Marshal.FreeHGlobal (buffer);
			}
		}

		public static bool FileNameEquals (string file1, string file2)
		{
			if (file1 == null)
				return file2 == null;

			if (file2 == null)
				return false;

			if (PathComparer.Compare (file1, file2) == 0)
				return true;

			var rfile1 = ResolveFullPath (file1);
			var rfile2 = ResolveFullPath (file2);

			return PathComparer.Compare (rfile1, rfile2) == 0;
		}
		
		internal bool EnableBreakEvent (BreakEvent be, bool enabled)
		{
			if (IsReadOnly)
				return false;

			OnChanged ();
			EventHandler<BreakEventArgs> evnt = BreakEventEnableStatusChanged;
			if (evnt != null)
				evnt (this, new BreakEventArgs (be));
			NotifyStatusChanged (be);

			return true;
		}
		
		void OnBreakEventAdded (BreakEvent be)
		{
			OnChanged ();
			EventHandler<BreakEventArgs> breakEventAdded = BreakEventAdded;
			if (breakEventAdded != null)
				breakEventAdded (this, new BreakEventArgs (be));
			if (be is Breakpoint) {
				EventHandler<BreakpointEventArgs> breakpointAdded = BreakpointAdded;
				if (breakpointAdded != null)
					breakpointAdded (this, new BreakpointEventArgs ((Breakpoint)be));
			} else if (be is Catchpoint) {
				EventHandler<CatchpointEventArgs> catchpointAdded = CatchpointAdded;
				if (catchpointAdded != null)
					catchpointAdded (this, new CatchpointEventArgs ((Catchpoint)be));
			}
		}
		
		void OnBreakEventRemoved (BreakEvent be)
		{
			OnChanged ();
			EventHandler<BreakEventArgs> breakEventRemoved = BreakEventRemoved;
			if (breakEventRemoved != null)
				breakEventRemoved (this, new BreakEventArgs (be));
			if (be is Breakpoint) {
				EventHandler<BreakpointEventArgs> breakpointRemoved = BreakpointRemoved;
				if (breakpointRemoved != null)
					breakpointRemoved (this, new BreakpointEventArgs ((Breakpoint)be));
			} else if (be is Catchpoint) {
				EventHandler<CatchpointEventArgs> catchpointRemoved = CatchpointRemoved;
				if (catchpointRemoved != null)
					catchpointRemoved (this, new CatchpointEventArgs ((Catchpoint)be));
			}
		}
		
		void OnChanged ()
		{
			EventHandler changed = Changed;
			if (changed != null)
				changed (this, EventArgs.Empty);
		}
		
		internal void NotifyStatusChanged (BreakEvent be)
		{
			try {
				EventHandler<BreakEventArgs> breakEventStatusChanged = BreakEventStatusChanged;
				if (breakEventStatusChanged != null)
					breakEventStatusChanged (this, new BreakEventArgs (be));
				if (be is Breakpoint) {
					EventHandler<BreakpointEventArgs> breakpointStatusChanged = BreakpointStatusChanged;
					if (breakpointStatusChanged != null)
						breakpointStatusChanged (this, new BreakpointEventArgs ((Breakpoint)be));
				} else if (be is Catchpoint) {
					EventHandler<CatchpointEventArgs > catchpointStatusChanged = CatchpointStatusChanged;
					if (catchpointStatusChanged != null)
						catchpointStatusChanged (this, new CatchpointEventArgs ((Catchpoint)be));
				}
			} catch {
				// Ignore
			}
		}
		
		internal void NotifyBreakEventChanged (BreakEvent be)
		{
			try {
				EventHandler<BreakEventArgs> breakEventModified = BreakEventModified;
				if (breakEventModified != null)
					breakEventModified (this, new BreakEventArgs (be));
				if (be is Breakpoint) {
					EventHandler<BreakpointEventArgs > breakpointModified = BreakpointModified;
					if (breakpointModified != null)
						breakpointModified (this, new BreakpointEventArgs ((Breakpoint)be));
				} else if (be is Catchpoint) {
					EventHandler<CatchpointEventArgs >  catchpointModified = CatchpointModified;
					if (catchpointModified != null)
						catchpointModified (this, new CatchpointEventArgs ((Catchpoint)be));
				}
				OnChanged ();
			} catch {
				// Ignore
			}
		}
		
		internal void NotifyBreakEventUpdated (BreakEvent be)
		{

			try {
				EventHandler<BreakEventArgs> breakEventUpdated = BreakEventUpdated;
				if (breakEventUpdated != null)
					breakEventUpdated (this, new BreakEventArgs (be));
				if (be is Breakpoint) {
					EventHandler<BreakpointEventArgs> breakpointUpdated = BreakpointUpdated;
					if (breakpointUpdated != null)
						breakpointUpdated (this, new BreakpointEventArgs ((Breakpoint)be));
				} else if (be is Catchpoint) {
					EventHandler<CatchpointEventArgs>  catchpointUpdated = CatchpointUpdated;
					if (catchpointUpdated != null)
						catchpointUpdated (this, new CatchpointEventArgs ((Catchpoint)be));
				}
			} catch {
				// Ignore
			}
		}
		
		public event EventHandler<BreakpointEventArgs> BreakpointAdded;
		public event EventHandler<BreakpointEventArgs> BreakpointRemoved;
		public event EventHandler<BreakpointEventArgs> BreakpointStatusChanged;
		public event EventHandler<BreakpointEventArgs> BreakpointModified;
		public event EventHandler<BreakpointEventArgs> BreakpointUpdated;
		public event EventHandler<CatchpointEventArgs> CatchpointAdded;
		public event EventHandler<CatchpointEventArgs> CatchpointRemoved;
		public event EventHandler<CatchpointEventArgs> CatchpointStatusChanged;
		public event EventHandler<CatchpointEventArgs> CatchpointModified;
		public event EventHandler<CatchpointEventArgs> CatchpointUpdated;
		public event EventHandler<BreakEventArgs> BreakEventAdded;
		public event EventHandler<BreakEventArgs> BreakEventRemoved;
		public event EventHandler<BreakEventArgs> BreakEventStatusChanged;
		public event EventHandler<BreakEventArgs> BreakEventModified;
		public event EventHandler<BreakEventArgs> BreakEventUpdated;
		public event EventHandler Changed;
		public event EventHandler<ReadOnlyCheckEventArgs> CheckingReadOnly;
		
		internal event EventHandler<BreakEventArgs> BreakEventEnableStatusChanged;
	}
	
	public class ReadOnlyCheckEventArgs: EventArgs
	{
		internal bool IsReadOnly;
		
		public void SetReadOnly (bool isReadOnly)
		{
			IsReadOnly = IsReadOnly || isReadOnly;
		}
	}
}
