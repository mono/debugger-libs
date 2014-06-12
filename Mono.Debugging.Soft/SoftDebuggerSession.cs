// 
// SoftDebuggerSession.cs
//  
// Authors: Lluis Sanchez Gual <lluis@novell.com>
//          Jeffrey Stedfast <jeff@xamarin.com>
// 
// Copyright (c) 2009 Novell, Inc (http://www.novell.com)
// Copyright (c) 2012 Xamarin Inc. (http://www.xamarin.com)
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

//#define DEBUG_EVENT_QUEUEING

using System;
using System.IO;
using System.Net;
using System.Linq;
using System.Text;
using System.Threading;
using System.Reflection;
using System.Net.Sockets;
using System.Globalization;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Mono.CompilerServices.SymbolWriter;
using Mono.Debugging.Client;
using Mono.Debugger.Soft;
using Mono.Debugging.Evaluation;
using MDB = Mono.Debugger.Soft;

namespace Mono.Debugging.Soft
{
	public class SoftDebuggerSession : DebuggerSession
	{
		readonly Dictionary<Tuple<TypeMirror, string>, MethodMirror[]> overloadResolveCache = new Dictionary<Tuple<TypeMirror, string>, MethodMirror[]> ();
		readonly Dictionary<string, List<TypeMirror>> source_to_type = new Dictionary<string, List<TypeMirror>> (PathComparer);
		readonly Dictionary<long,ObjectMirror> activeExceptionsByThread = new Dictionary<long, ObjectMirror> ();
		readonly Dictionary<EventRequest, BreakInfo> breakpoints = new Dictionary<EventRequest, BreakInfo> ();
		readonly Dictionary<string, MonoSymbolFile> symbolFiles = new Dictionary<string, MonoSymbolFile> ();
		readonly Dictionary<TypeMirror, string[]> type_to_source = new Dictionary<TypeMirror, string[]> ();
		readonly Dictionary<string, TypeMirror> aliases = new Dictionary<string, TypeMirror> ();
		readonly Dictionary<string, TypeMirror> types = new Dictionary<string, TypeMirror> ();
		readonly LinkedList<List<Event>> queuedEventSets = new LinkedList<List<Event>> ();
		readonly Dictionary<long,long> localThreadIds = new Dictionary<long, long> ();
		readonly List<BreakInfo> pending_bes = new List<BreakInfo> ();
		TypeLoadEventRequest typeLoadReq, typeLoadTypeNameReq;
		ExceptionEventRequest unhandledExceptionRequest;
		Dictionary<string, string> assemblyPathMap;
		ThreadMirror current_thread, recent_thread;
		List<AssemblyMirror> assemblyFilters;
		StepEventRequest currentStepRequest;
		IConnectionDialog connectionDialog;
		Thread outputReader, errorReader;
		bool loggedSymlinkedRuntimesBug;
		SoftDebuggerStartArgs startArgs;
		List<string> userAssemblyNames;
		ThreadInfo[] current_threads;
		string remoteProcessName;
		bool useFullPaths = true;
		long currentAddress = -1;
		IAsyncResult connection;
		ProcessInfo[] procs;
		Thread eventHandler;
		VirtualMachine vm;
		bool autoStepInto;
		bool disposed;
		bool started;

		internal int StackVersion;

		public SoftDebuggerAdaptor Adaptor {
			get; private set;
		}

		public SoftDebuggerSession ()
		{
			Adaptor = CreateSoftDebuggerAdaptor ();
			Adaptor.BusyStateChanged += (sender, e) => SetBusyState (e);
			Adaptor.Session = this;
		}

		protected virtual SoftDebuggerAdaptor CreateSoftDebuggerAdaptor ()
		{
			return new SoftDebuggerAdaptor ();
		}

		public Version ProtocolVersion {
			get { return new Version (vm.Version.MajorVersion, vm.Version.MinorVersion); }
		}

		protected override void OnRun (DebuggerStartInfo startInfo)
		{
			if (HasExited)
				throw new InvalidOperationException ("Already exited");
			
			var dsi = (SoftDebuggerStartInfo) startInfo;
			if (dsi.StartArgs is SoftDebuggerLaunchArgs) {
				StartLaunching (dsi);
			} else if (dsi.StartArgs is SoftDebuggerConnectArgs) {
				StartConnecting (dsi);
			} else if (dsi.StartArgs is SoftDebuggerListenArgs) {
				StartListening (dsi);
			} else if (dsi.StartArgs.ConnectionProvider != null) {
				StartConnection (dsi);
			} else {
				throw new ArgumentException ("StartArgs has no ConnectionProvider");
			}
		}
		
		void StartConnection (SoftDebuggerStartInfo dsi)
		{
			startArgs = dsi.StartArgs;
			
			RegisterUserAssemblies (dsi);
			
			if (!String.IsNullOrEmpty (dsi.LogMessage))
				OnDebuggerOutput (false, dsi.LogMessage + Environment.NewLine);
			
			AsyncCallback callback = null;
			int attemptNumber = 0;
			int maxAttempts = startArgs.MaxConnectionAttempts;
			int timeBetweenAttempts = startArgs.TimeBetweenConnectionAttempts;
			callback = delegate (IAsyncResult ar) {
				try {
					string appName;
					VirtualMachine machine;
					startArgs.ConnectionProvider.EndConnect (ar, out machine, out appName);
					remoteProcessName = appName;
					ConnectionStarted (machine);
					return;
				} catch (Exception ex) {
					attemptNumber++;
					if (!ShouldRetryConnection (ex, attemptNumber)
						|| !startArgs.ConnectionProvider.ShouldRetryConnection (ex)
						|| attemptNumber == maxAttempts
						|| HasExited) {
						OnConnectionError (ex);
						return;
					}
				}
				try {
					if (timeBetweenAttempts > 0)
						Thread.Sleep (timeBetweenAttempts);
					ConnectionStarting (startArgs.ConnectionProvider.BeginConnect (dsi, callback), dsi, false, 0);
				} catch (Exception ex2) {
					OnConnectionError (ex2);
				}
			};
			//the "listening" value is never used, pass a dummy value
			ConnectionStarting (startArgs.ConnectionProvider.BeginConnect (dsi, callback), dsi, false, 0);
		}
		
		void StartLaunching (SoftDebuggerStartInfo dsi)
		{
			var args = (SoftDebuggerLaunchArgs) dsi.StartArgs;
			var runtime = string.IsNullOrEmpty (args.MonoRuntimePrefix) ? "mono" : Path.Combine (Path.Combine (args.MonoRuntimePrefix, "bin"), "mono");
			RegisterUserAssemblies (dsi);
			
			var psi = new System.Diagnostics.ProcessStartInfo (runtime) {
				Arguments = string.Format ("\"{0}\" {1}", dsi.Command, dsi.Arguments),
				WorkingDirectory = dsi.WorkingDirectory,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true,
			};
			
			LaunchOptions options = null;
			
			if (dsi.UseExternalConsole && args.ExternalConsoleLauncher != null) {
				options = new LaunchOptions ();
				options.CustomTargetProcessLauncher = args.ExternalConsoleLauncher;
				psi.RedirectStandardOutput = false;
				psi.RedirectStandardError = false;
			}

			var sdbLog = Environment.GetEnvironmentVariable ("MONODEVELOP_SDB_LOG");
			if (!string.IsNullOrEmpty (sdbLog)) {
				options = options ?? new LaunchOptions ();
				options.AgentArgs = string.Format ("loglevel=10,logfile='{0}',setpgid=y", sdbLog);
			}
			
			foreach (var env in args.MonoRuntimeEnvironmentVariables)
				psi.EnvironmentVariables[env.Key] = env.Value;
			
			foreach (var env in dsi.EnvironmentVariables)
				psi.EnvironmentVariables[env.Key] = env.Value;
			
			if (!String.IsNullOrEmpty (dsi.LogMessage))
				OnDebuggerOutput (false, dsi.LogMessage + Environment.NewLine);
			
			var callback = HandleConnectionCallbackErrors (ar => ConnectionStarted (VirtualMachineManager.EndLaunch (ar)));
			ConnectionStarting (VirtualMachineManager.BeginLaunch (psi, callback, options), dsi, true, 0);
		}
		
		/// <summary>Starts the debugger listening for a connection over TCP/IP</summary>
		protected void StartListening (SoftDebuggerStartInfo dsi)
		{
			int dp, cp;
			StartListening (dsi, out dp, out cp);
		}
		
		/// <summary>Starts the debugger listening for a connection over TCP/IP</summary>
		protected void StartListening (SoftDebuggerStartInfo dsi, out int assignedDebugPort)
		{
			int cp;
			StartListening (dsi, out assignedDebugPort, out cp);
		}
		
		/// <summary>Starts the debugger listening for a connection over TCP/IP</summary>
		protected void StartListening (SoftDebuggerStartInfo dsi, out int assignedDebugPort, out int assignedConsolePort)
		{
		
			IPEndPoint dbgEP, conEP;
			InitForRemoteSession (dsi, out dbgEP, out conEP);
			
			var callback = HandleConnectionCallbackErrors (ar => ConnectionStarted (VirtualMachineManager.EndListen (ar)));
			var a = VirtualMachineManager.BeginListen (dbgEP, conEP, callback, out assignedDebugPort, out assignedConsolePort);
			ConnectionStarting (a, dsi, true, 0);
		}

		protected virtual bool ShouldRetryConnection (Exception ex, int attemptNumber)
		{
			var sx = ex as SocketException;
			if (sx != null) {
				if (sx.ErrorCode == 10061) //connection refused
					return true;
			}
			return false;
		}
		
		protected void StartConnecting (SoftDebuggerStartInfo dsi)
		{
			StartConnecting (dsi, dsi.StartArgs.MaxConnectionAttempts, dsi.StartArgs.TimeBetweenConnectionAttempts);
		}
		
		/// <summary>Starts the debugger connecting to a remote IP</summary>
		protected void StartConnecting (SoftDebuggerStartInfo dsi, int maxAttempts, int timeBetweenAttempts)
		{	
			if (timeBetweenAttempts < 0 || timeBetweenAttempts > 10000)
				throw new ArgumentException ("timeBetweenAttempts");
			
			IPEndPoint dbgEP, conEP;
			InitForRemoteSession (dsi, out dbgEP, out conEP);
			
			AsyncCallback callback = null;
			int attemptNumber = 0;
			callback = delegate (IAsyncResult ar) {
				try {
					ConnectionStarted (VirtualMachineManager.EndConnect (ar));
					return;
				} catch (Exception ex) {
					attemptNumber++;
					if (!ShouldRetryConnection (ex, attemptNumber) || attemptNumber == maxAttempts || HasExited) {
						OnConnectionError (ex);
						return;
					}
				}
				try {
					if (timeBetweenAttempts > 0)
						Thread.Sleep (timeBetweenAttempts);
					
					ConnectionStarting (VirtualMachineManager.BeginConnect (dbgEP, conEP, callback), dsi, false, attemptNumber);
					
				} catch (Exception ex2) {
					OnConnectionError (ex2);
				}
			};
			
			ConnectionStarting (VirtualMachineManager.BeginConnect (dbgEP, conEP, callback), dsi, false, 0);
		}
		
		void InitForRemoteSession (SoftDebuggerStartInfo dsi, out IPEndPoint dbgEP, out IPEndPoint conEP)
		{
			if (remoteProcessName != null)
				throw new InvalidOperationException ("Cannot initialize connection more than once");
			
			var args = (SoftDebuggerRemoteArgs) dsi.StartArgs;
			
			remoteProcessName = args.AppName;
			
			RegisterUserAssemblies (dsi);
			
			dbgEP = new IPEndPoint (args.Address, args.DebugPort);
			conEP = args.RedirectOutput? new IPEndPoint (args.Address, args.OutputPort) : null;
			
			if (!String.IsNullOrEmpty (dsi.LogMessage))
				OnDebuggerOutput (false, dsi.LogMessage + Environment.NewLine);
		}
		
		///<summary>Catches errors in async callbacks and hands off to OnConnectionError</summary>
		AsyncCallback HandleConnectionCallbackErrors (AsyncCallback callback)
		{
			return delegate (IAsyncResult ar) {
				connection = null;
				try {
					callback (ar);
				} catch (Exception ex) {
					OnConnectionError (ex);
				}
			};
		}
		
		/// <summary>
		/// Called if an error happens while making the connection. Default terminates the session.
		/// </summary>
		protected virtual void OnConnectionError (Exception ex)
		{
			//if the exception was caused by cancelling the session
			if (HasExited)
				return;
			
			if (!HandleException (new ConnectionException (ex))) {
				DebuggerLoggingService.LogAndShowException ("Unhandled error launching soft debugger", ex);
			}
			
			// The session is dead
			// HandleException doesn't actually handle exceptions, it just displays them.
			EndSession ();
		}
		
		void ConnectionStarting (IAsyncResult connectionHandle, DebuggerStartInfo dsi, bool listening, int attemptNumber) 
		{
			if (connection != null && (attemptNumber == 0 || !connection.IsCompleted))
				throw new InvalidOperationException ("Already connecting");
			
			connection = connectionHandle;
			
			if (ConnectionDialogCreator != null && attemptNumber == 0) {
				connectionDialog = ConnectionDialogCreator ();
				connectionDialog.UserCancelled += delegate {
					EndSession ();
				};
			}

			if (connectionDialog != null)
				connectionDialog.SetMessage (dsi, GetConnectingMessage (dsi), listening, attemptNumber);
		}
		
		protected virtual string GetConnectingMessage (DebuggerStartInfo dsi)
		{
			return null;
		}
		
		void EndLaunch ()
		{
			HideConnectionDialog ();
			if (connection != null) {
				if (startArgs != null && startArgs.ConnectionProvider != null) {
					startArgs.ConnectionProvider.CancelConnect (connection);
					startArgs = null;
				} else {
					VirtualMachineManager.CancelConnection (connection);
				}
				connection = null;
			}
		}
		
		protected virtual void EndSession ()
		{
			if (!HasExited) {
				EndLaunch ();
				OnTargetEvent (new TargetEventArgs (TargetEventType.TargetExited));
			}
		}

		public Dictionary<Tuple<TypeMirror, string>, MethodMirror[]> OverloadResolveCache {
			get {
				return overloadResolveCache;
			}
		}
		
		void HideConnectionDialog ()
		{
			if (connectionDialog != null) {
				connectionDialog.Dispose ();
				connectionDialog = null;
			}
		}
		
		/// <summary>
		/// If subclasses do an async connect in OnRun, they should pass the resulting VM to this method.
		/// If the vm is null, the session will be closed.
		/// </summary>
		void ConnectionStarted (VirtualMachine machine)
		{
			if (vm != null)
				throw new InvalidOperationException ("The VM has already connected");
			
			if (machine == null) {
				EndSession ();
				return;
			}
			
			connection = null;
			
			vm = machine;
			
			//full paths, from GetSourceFiles (true), are only supported by sdb protocol 2.2 and later
			useFullPaths = machine.Version.AtLeast (2, 2);
			
			ConnectOutput (machine.StandardOutput, false);
			ConnectOutput (machine.StandardError, true);
			
			HideConnectionDialog ();
			
			machine.EnableEvents (EventType.AssemblyLoad, EventType.ThreadStart, EventType.ThreadDeath,
				EventType.AssemblyUnload, EventType.UserBreak, EventType.UserLog);
			try {
				unhandledExceptionRequest = machine.CreateExceptionRequest (null, false, true);
				unhandledExceptionRequest.Enable ();
			} catch (NotSupportedException) {
				//Mono < 2.6.3 doesn't support catching unhandled exceptions
			}

			if (machine.Version.AtLeast (2, 9)) {
				/* Created later */
			} else {
				machine.EnableEvents (EventType.TypeLoad);
			}
			
			started = true;
			
			/* Wait for the VMStart event */
			HandleEventSet (machine.GetNextEventSet ());
			
			eventHandler = new Thread (EventHandler);
			eventHandler.Name = "SDB Event Handler";
			eventHandler.IsBackground = true;
			eventHandler.Start ();
		}
		
		void RegisterUserAssemblies (SoftDebuggerStartInfo dsi)
		{
			if (Options.ProjectAssembliesOnly && dsi.UserAssemblyNames != null) {
				assemblyFilters = new List<AssemblyMirror> ();
				userAssemblyNames = dsi.UserAssemblyNames.Select (x => x.ToString ()).ToList ();
			}
			
			assemblyPathMap = dsi.AssemblyPathMap;
			if (assemblyPathMap == null)
				assemblyPathMap = new Dictionary<string, string> ();
		}
		
		protected bool SetSocketTimeouts (int sendTimeout, int receiveTimeout, int keepaliveInterval)
		{
			try {
				if (vm.Version.AtLeast (2, 4)) {
					vm.EnableEvents (EventType.KeepAlive);
					vm.SetSocketTimeouts (sendTimeout, receiveTimeout, keepaliveInterval);
					return true;
				}

				return false;
			} catch {
				return false;
			}
		}

		protected void ConnectOutput (StreamReader reader, bool error)
		{
			Thread t = (error ? errorReader : outputReader);
			if (t != null || reader == null)
				return;

			t = new Thread (() => ReadOutput (reader, error));
			t.Name = error ? "SDB error reader" : "SDB output reader";
			t.IsBackground = true;
			t.Start ();

			if (error)
				errorReader = t;	
			else
				outputReader = t;
		}

		void ReadOutput (TextReader reader, bool isError)
		{
			try {
				var buffer = new char [1024];
				while (!HasExited) {
					int c = reader.Read (buffer, 0, buffer.Length);
					if (c > 0) {
						OnTargetOutput (isError, new string (buffer, 0, c));
					} else {
						//FIXME: workaround for buggy console stream that never blocks
						Thread.Sleep (250);
					}
				}
			} catch (IOException) {
				// Ignore
			}
		}

		protected virtual void OnResumed ()
		{
			current_threads = null;
			current_thread = null;
			procs = null;
			activeExceptionsByThread.Clear ();
		}
		
		public VirtualMachine VirtualMachine {
			get { return vm; }
		}
		
		public TypeMirror GetType (string fullName)
		{
			TypeMirror tm;

			if (!types.TryGetValue (fullName, out tm))
				aliases.TryGetValue (fullName, out tm);

			return tm;
		}
		
		public IEnumerable<TypeMirror> GetAllTypes ()
		{
			return types.Values;
		}

		protected override bool AllowBreakEventChanges {
			get { return true; }
		}

		public override void Dispose ()
		{
			base.Dispose ();

			if (disposed)
				return;

			disposed = true;

			if (!HasExited)
				EndLaunch ();

			foreach (var symfile in symbolFiles)
				symfile.Value.Dispose ();

			symbolFiles.Clear ();

			if (!HasExited) {
				if (vm != null) {
					ThreadPool.QueueUserWorkItem (delegate {
						try {
							vm.Exit (0);
						} catch (VMDisconnectedException) {
						} catch (Exception ex) {
							DebuggerLoggingService.LogError ("Error exiting SDB VM:", ex);
						}
					});
				}
			}
			
			Adaptor.Dispose ();
		}

		protected override void OnAttachToProcess (long processId)
		{
			throw new NotSupportedException ();
		}

		protected override void OnContinue ()
		{
			ThreadPool.QueueUserWorkItem (delegate {
				try {
					Adaptor.CancelAsyncOperations (); // This call can block, so it has to run in background thread to avoid keeping the main session lock
					OnResumed ();
					vm.Resume ();
					DequeueEventsForFirstThread ();
				} catch (Exception ex) {
					if (!HandleException (ex))
						OnDebuggerOutput (true, ex.ToString ());
				}
			});
		}

		protected override void OnDetach ()
		{
			throw new NotSupportedException ();
		}

		protected override void OnExit ()
		{
			HasExited = true;
			EndLaunch ();
			if (vm != null) {
				try {
					vm.Exit (0);
				} catch (VMDisconnectedException) {
					// The VM was already disconnected, ignore.
				} catch (SocketException se) {
					// This will often happen during normal operation
					DebuggerLoggingService.LogError ("Error closing debugger session", se);
				} catch (IOException ex) {
					// This will often happen during normal operation
					DebuggerLoggingService.LogError ("Error closing debugger session", ex);
				}
			}
			QueueEnsureExited ();
		}
		
		void QueueEnsureExited ()
		{
			if (vm != null) {
				//FIXME: this might never get reached if the IDE is Exited first
				try {
					if (vm.Process != null) {
						ThreadPool.QueueUserWorkItem (delegate {
							// This is a workaround for a mono bug
							// Without this call, the process may become zombie in mono < 2.10.2
							vm.Process.WaitForExit ();
						});
					}
				} catch (InvalidOperationException) {
					// ignore - this is thrown by the vm.Process getter when the process has already exited
				} catch (Exception ex) {
					DebuggerLoggingService.LogError ("Failed to launch a thread to wait for the process to exit", ex);
				}

				var t = new System.Timers.Timer ();
				t.Interval = 3000;
				t.Elapsed += delegate {
					try {
						t.Enabled = false;
						t.Dispose ();
						EnsureExited ();
					} catch (Exception ex) {
						DebuggerLoggingService.LogError ("Failed to force-terminate process", ex);
					}

					try {
						if (vm != null) {
							//this is a no-op if it already closed
							vm.ForceDisconnect ();
						}
					} catch (Exception ex) {
						DebuggerLoggingService.LogError ("Failed to force-close debugger connection", ex);
					}
				};

				t.Enabled = true;
			}	
		}
		
		/// <summary>This is a fallback in case the debugger agent doesn't respond to an exit call</summary>
		protected virtual void EnsureExited ()
		{
			try {
				if (vm != null && vm.TargetProcess != null && !vm.TargetProcess.HasExited)
					vm.TargetProcess.Kill ();
			} catch (Exception ex) {
				DebuggerLoggingService.LogError ("Error force-terminating soft debugger process", ex);
			}
		}

		protected override void OnFinish ()
		{
			Step (StepDepth.Out, StepSize.Line);
		}

		protected override ProcessInfo[] OnGetProcesses ()
		{
			if (procs == null) {
				if (remoteProcessName != null || vm.TargetProcess == null) {
					procs = new [] { new ProcessInfo (0, remoteProcessName ?? "mono") };
				} else {
					try {
						procs = new [] { new ProcessInfo (vm.TargetProcess.Id, vm.TargetProcess.ProcessName) };
					} catch (Exception ex) {
						if (!loggedSymlinkedRuntimesBug) {
							loggedSymlinkedRuntimesBug = true;
							DebuggerLoggingService.LogError ("Error getting debugger process info. Known Mono bug with symlinked runtimes.", ex);
						}
						procs = new [] { new ProcessInfo (0, "mono") };
					}
				}
			}
			return new [] { new ProcessInfo (procs[0].Id, procs[0].Name) };
		}

		protected override Backtrace OnGetThreadBacktrace (long processId, long threadId)
		{
			return GetThreadBacktrace (GetThread (threadId));
		}
		
		Backtrace GetThreadBacktrace (ThreadMirror thread)
		{
			return new Backtrace (new SoftDebuggerBacktrace (this, thread));
		}

		string GetThreadName (ThreadMirror t)
		{
			string name = t.Name;
			if (string.IsNullOrEmpty (name)) {
				try {
					if (t.IsThreadPoolThread)
						return "<Thread Pool>";
				} catch {
					if (vm.Version.AtLeast (2, 2)) {
						throw;
					}
					return "<Thread>";
				}
			}
			return name;
		}

		protected override void OnFetchFrames (ThreadInfo[] threads)
		{
			var mirrorThreads = new ThreadMirror[threads.Length];
			for (int i = 0; i < threads.Length; i++)
				mirrorThreads [i] = GetThread (threads [i].Id);
			ThreadMirror.FetchFrames (mirrorThreads);
		}

		protected override ThreadInfo[] OnGetThreads (long processId)
		{
			if (current_threads == null) {
				var mirrors = vm.GetThreads ();
				var threads = new ThreadInfo[mirrors.Count];

				for (int i = 0; i < mirrors.Count; i++) {
					var thread = mirrors[i];

					threads[i] = new ThreadInfo (processId, GetId (thread), GetThreadName (thread), null);
				}

				current_threads = threads;
			}

			return current_threads;
		}
		
		ThreadMirror GetThread (long threadId)
		{
			foreach (var thread in vm.GetThreads ()) {
				if (GetId (thread) == threadId)
					return thread;
			}

			return null;
		}
		
		ThreadInfo GetThread (ProcessInfo process, ThreadMirror thread)
		{
			long id = GetId (thread);

			foreach (var threadInfo in OnGetThreads (process.Id)) {
				if (threadInfo.Id == id)
					return threadInfo;
			}

			return null;
		}

		public override bool CanSetNextStatement {
			get { return vm.Version.AtLeast (2, 29); }
		}

		protected override void OnSetNextStatement (long threadId, string fileName, int line, int column)
		{
			if (!CanSetNextStatement)
				throw new NotSupportedException ();

			var thread = GetThread (threadId);
			if (thread == null)
				throw new ArgumentException ("Unknown thread.");

			var frames = thread.GetFrames ();
			if (frames.Length == 0)
				throw new NotSupportedException ();

			var location = FindLocationByMethod (frames[0].Method, fileName, line, column);
			if (location == null)
				throw new NotSupportedException ();

			try {
				thread.SetIP (location);
			} catch (ArgumentException) {
				throw new NotSupportedException ();
			}
		}

		protected override void OnSetNextStatement (long threadId, int ilOffset)
		{
			if (!CanSetNextStatement)
				throw new NotSupportedException ();

			var thread = GetThread (threadId);
			if (thread == null)
				throw new ArgumentException ("Unknown thread.");

			var frames = thread.GetFrames ();
			if (frames.Length == 0)
				throw new NotSupportedException ();

			var location = frames[0].Method.LocationAtILOffset (ilOffset);
			if (location == null)
				throw new NotSupportedException ();

			try {
				thread.SetIP (location);
			} catch (ArgumentException) {
				throw new NotSupportedException ();
			}
		}
		
		protected override BreakEventInfo OnInsertBreakEvent (BreakEvent breakEvent)
		{
			lock (pending_bes) {
				var bi = new BreakInfo ();

				if (HasExited) {
					bi.SetStatus (BreakEventStatus.Disconnected, null);
					return bi;
				}

				if (breakEvent is FunctionBreakpoint) {
					var fb = (FunctionBreakpoint) breakEvent;
					bool resolved = false;

					foreach (var location in FindFunctionLocations (fb.FunctionName, fb.ParamTypes)) {
						string paramList = string.Empty;

						if (fb.ParamTypes != null)
							paramList = "(" + string.Join (", ", fb.ParamTypes) + ")";

						OnDebuggerOutput (false, string.Format ("Resolved pending breakpoint for '{0}{1}' to {2}:{3} [0x{4:x5}].\n",
						                                        fb.FunctionName, paramList, location.SourceFile, location.LineNumber, location.ILOffset));

						bi.FileName = location.SourceFile;
						bi.Location = location;

						InsertBreakpoint (fb, bi);
						bi.SetStatus (BreakEventStatus.Bound, null);
						resolved = true;
					}

					if (!resolved) {
						// FIXME: handle types like GenericType<>, GenericType<SomeOtherType>, and GenericType<...>+NestedGenricType<...>
						int dot = fb.FunctionName.LastIndexOf ('.');
						if (dot != -1)
							bi.TypeName = fb.FunctionName.Substring (0, dot);

						bi.SetStatus (BreakEventStatus.NotBound, null);
						pending_bes.Add (bi);
					}
				} else if (breakEvent is Breakpoint) {
					var bp = (Breakpoint) breakEvent;
					bool insideLoadedRange;
					bool resolved = false;
					bool generic;

					bi.FileName = bp.FileName;

					foreach (var location in FindLocationsByFile (bp.FileName, bp.Line, bp.Column, out generic, out insideLoadedRange)) {
						OnDebuggerOutput (false, string.Format ("Resolved pending breakpoint at '{0}:{1},{2}' to {3} [0x{4:x5}].\n",
						                                        bp.FileName, bp.Line, bp.Column, GetPrettyMethodName (location.Method), location.ILOffset));

						bi.Location = location;
						InsertBreakpoint (bp, bi);
						bi.SetStatus (BreakEventStatus.Bound, null);
						resolved = true;
					}

					if (resolved) {
						// Note: if the type or method is generic, there may be more instances so don't assume we are done resolving the breakpoint
						if (generic)
							pending_bes.Add (bi);
					} else {
						pending_bes.Add (bi);
						if (insideLoadedRange)
							bi.SetStatus (BreakEventStatus.Invalid, null);
						else
							bi.SetStatus (BreakEventStatus.NotBound, null);
					}
				} else if (breakEvent is Catchpoint) {
					var cp = (Catchpoint) breakEvent;
					TypeMirror type;

					if (!types.TryGetValue (cp.ExceptionName, out type)) {
						//
						// Same as in FindLocationByFile (), fetch types matching the type name
						if (vm.Version.AtLeast (2, 9)) {
							foreach (TypeMirror t in vm.GetTypes (cp.ExceptionName, false))
								ProcessType (t);
						}
					}

					if (types.TryGetValue (cp.ExceptionName, out type)) {
						InsertCatchpoint (cp, bi, type);
						bi.SetStatus (BreakEventStatus.Bound, null);
					} else {
						bi.TypeName = cp.ExceptionName;
						pending_bes.Add (bi);
						bi.SetStatus (BreakEventStatus.NotBound, null);
					}
				}

				/*
				 * TypeLoad events lead to too much wire traffic + suspend/resume work, so
				 * filter them using the file names used by pending breakpoints.
				 */
				if (vm.Version.AtLeast (2, 9)) {
					var sourceFileList = pending_bes.Where (b => b.FileName != null).Select (b => b.FileName).ToArray ();
					if (sourceFileList.Length > 0) {
						//HACK: with older versions of sdb that don't support case-insenitive compares,
						//explicitly try lowercased drivename on windows, since csc (when not hosted in VS) lowercases
						//the drivename in the pdb files that get converted to mdbs as-is
						if (IsWindows && !vm.Version.AtLeast (2, 12)) {
							int originalCount = sourceFileList.Length;
							Array.Resize (ref sourceFileList, originalCount * 2);
							for (int i = 0; i < originalCount; i++) {
								string n = sourceFileList[i];
								sourceFileList[originalCount + i] = char.ToLower (n[0]) + n.Substring (1);
							}
						}

						if (typeLoadReq == null) {
							typeLoadReq = vm.CreateTypeLoadRequest ();
						}
						typeLoadReq.Enabled = false;
						typeLoadReq.SourceFileFilter = sourceFileList;
						typeLoadReq.Enabled = true;
					}

					var typeNameList = pending_bes.Where (b => b.TypeName != null).Select (b => b.TypeName).ToArray ();
					if (typeNameList.Length > 0) {
						// Use a separate request since the filters are ANDed together
						if (typeLoadTypeNameReq == null) {
							typeLoadTypeNameReq = vm.CreateTypeLoadRequest ();
						}
						typeLoadTypeNameReq.Enabled = false;
						typeLoadTypeNameReq.TypeNameFilter = typeNameList;
						typeLoadTypeNameReq.Enabled = true;
					}
				}

				return bi;
			}
		}

		protected override void OnRemoveBreakEvent (BreakEventInfo eventInfo)
		{
			if (HasExited)
				return;

			lock (pending_bes) {
				var bi = (BreakInfo) eventInfo;
				if (bi.Requests.Count != 0) {
					foreach (var request in bi.Requests)
						request.Enabled = false;

					RemoveQueuedBreakEvents (bi.Requests);
				}

				pending_bes.Remove (bi);
			}
		}

		protected override void OnEnableBreakEvent (BreakEventInfo eventInfo, bool enable)
		{
			if (HasExited)
				return;

			lock (pending_bes) {
				var bi = (BreakInfo) eventInfo;
				if (bi.Requests.Count != 0) {
					foreach (var request in bi.Requests)
						request.Enabled = enable;

					if (!enable)
						RemoveQueuedBreakEvents (bi.Requests);
				}
			}
		}

		protected override void OnUpdateBreakEvent (BreakEventInfo eventInfo)
		{
		}

		void InsertBreakpoint (Breakpoint bp, BreakInfo bi)
		{
			EventRequest request;
			
			request = vm.SetBreakpoint (bi.Location.Method, bi.Location.ILOffset);
			request.Enabled = bp.Enabled;
			bi.Requests.Add (request);
			
			breakpoints[request] = bi;
			
			if (bi.Location.LineNumber != bp.Line || bi.Location.ColumnNumber != bp.Column)
				bi.AdjustBreakpointLocation (bi.Location.LineNumber, bi.Location.ColumnNumber);
		}
		
		void InsertCatchpoint (Catchpoint cp, BreakInfo bi, TypeMirror excType)
		{
			ExceptionEventRequest request;

			request = vm.CreateExceptionRequest (excType, true, true);
			//Commenting Count so we have better control of counting
			//because VM only allows count equal to some number but we need also
			//lower, greater, equal or greater...
			//Plus some day we might want to put filtering before counting...
			//request.Count = cp.HitCount; // Note: need to set HitCount *before* enabling
			if (vm.Version.AtLeast (2, 25))
				request.IncludeSubclasses = cp.IncludeSubclasses; // Note: need to set IncludeSubclasses *before* enabling
			request.Enabled = cp.Enabled;
			bi.Requests.Add (request);

			breakpoints[request] = bi;
		}
		
		static bool CheckTypeName (string typeName, string name)
		{
			// if the name provided is empty, it matches anything.
			if (string.IsNullOrEmpty (name))
				return true;

			if (name.StartsWith ("global::", StringComparison.Ordinal)) {
				if (typeName != name.Substring ("global::".Length))
					return false;
			} else if (name.StartsWith ("::", StringComparison.Ordinal)) {
				if (typeName != name.Substring ("::".Length))
					return false;
			} else {
				// be a little more flexible with what we match... i.e. "Console" should match "System.Console"
				if (typeName != null && typeName.Length > name.Length) {
					if (!typeName.EndsWith (name, StringComparison.Ordinal))
						return false;

					char delim = typeName[typeName.Length - name.Length];
					if (delim != '.' && delim != '+')
						return false;
				} else if (typeName != name) {
					return false;
				}
			}

			return true;
		}

		static bool CheckTypeName (TypeMirror type, string name)
		{
			if (string.IsNullOrEmpty (name)) {
				// empty name matches anything
				return true;
			}

			if (name[name.Length - 1] == '?') {
				// canonicalize the user-specified nullable type
				return CheckTypeName (type, string.Format ("System.Nullable<{0}>", name.Substring (0, name.Length - 1)));
			}

			if (type.IsArray) {
				int startIndex = name.LastIndexOf ('[');
				int endIndex = name.Length - 1;

				if (startIndex == -1 || name[endIndex] != ']') {
					// the user-specified type is not an array
					return false;
				}

				var rank = name.Substring (startIndex + 1, endIndex - (startIndex + 1)).Split (new [] { ',' });
				if (rank.Length != type.GetArrayRank ())
					return false;

				return CheckTypeName (type.GetElementType (), name.Substring (0, startIndex).TrimEnd ());
			}

			if (type.IsPointer) {
				if (name.Length < 2 || name[name.Length - 1] != '*')
					return false;

				return CheckTypeName (type.GetElementType (), name.Substring (0, name.Length - 1).TrimEnd ());
			}

			if (type.IsGenericType) {
				int startIndex = name.IndexOf ('<');
				int endIndex = name.Length - 1;

				if (startIndex == -1 || name[endIndex] != '>') {
					// the user-specified type is not a generic type
					return false;
				}

				// make sure that the type name matches (minus generics)
				string subName = name.Substring (0, startIndex);
				string typeName = type.FullName;
				int tick;

				if ((tick = typeName.IndexOf ('`')) != -1)
					typeName = typeName.Substring (0, tick);

				if (!CheckTypeName (typeName, subName))
					return false;

				string[] paramTypes;
				if (!FunctionBreakpoint.TryParseParameters (name, startIndex + 1, endIndex, out paramTypes))
					return false;

				TypeMirror[] argTypes = type.GetGenericArguments ();
				if (paramTypes.Length != argTypes.Length)
					return false;

				for (int i = 0; i < paramTypes.Length; i++) {
					if (!CheckTypeName (argTypes[i], paramTypes[i]))
						return false;
				}
			} else if (!CheckTypeName (type.CSharpName, name)) {
				if (!CheckTypeName (type.FullName, name))
					return false;
			}

			return true;
		}

		static bool CheckMethodParams (MethodMirror method, string[] paramTypes)
		{
			if (paramTypes == null) {
				// User supplied no params to match against, match anything we find.
				return true;
			}

			var parameters = method.GetParameters ();
			if (parameters.Length != paramTypes.Length)
				return false;

			for (int i = 0; i < paramTypes.Length; i++) {
				if (!CheckTypeName (parameters[i].ParameterType, paramTypes[i]))
					return false;
			}

			return true;
		}
		
		bool IsGenericMethod (MethodMirror method)
		{
			return vm.Version.AtLeast (2, 12) && method.IsGenericMethod;
		}
		
		IEnumerable<Location> FindFunctionLocations (string function, string[] paramTypes)
		{
			if (!started)
				yield break;
			
			if (vm.Version.AtLeast (2, 9)) {
				int dot = function.LastIndexOf ('.');
				if (dot == -1 || dot + 1 == function.Length)
					yield break;

				// FIXME: handle types like GenericType<>, GenericType<SomeOtherType>, and GenericType<...>+NestedGenricType<...>
				string methodName = function.Substring (dot + 1);
				string typeName = function.Substring (0, dot);

				// FIXME: need a way of querying all types so we can substring match typeName (e.g. user may have typed "Console" instead of "System.Console")
				foreach (var type in vm.GetTypes (typeName, false)) {
					ProcessType (type);
					
					foreach (var method in type.GetMethodsByNameFlags (methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static, false)) {
						if (!CheckMethodParams (method, paramTypes))
							continue;
						
						Location location = GetLocFromMethod (method);
						if (location != null)
							yield return location;
					}
				}
			}
			
			yield break;
		}
		
		IList<Location> FindLocationsByFile (string file, int line, int column, out bool genericTypeOrMethod, out bool insideLoadedRange)
		{
			var locations = new List<Location> ();

			genericTypeOrMethod = false;
			insideLoadedRange = false;
			
			if (!started)
				return locations;

			string filename = PathToFileName (file);

			//
			// Fetch types matching the source file from the debuggee, and add them
			// to the source file->type mapping tables.
			// This is needed because we don't receive type load events for all types,
			// just the ones which match a source file with an existing breakpoint.
			//
			if (vm.Version.AtLeast (2, 9)) {
				IList<TypeMirror> typesInFile;
				if (vm.Version.AtLeast (2, 12)) {
					typesInFile = vm.GetTypesForSourceFile (filename, IgnoreFilenameCase);
				} else {
					typesInFile = vm.GetTypesForSourceFile (filename, false);
					//HACK: with older versions of sdb that don't support case-insenitive compares,
					//explicitly try lowercased drivename on windows, since csc (when not hosted in VS) lowercases
					//the drivename in the pdb files that get converted to mdbs as-is
					if (typesInFile.Count == 0 && IsWindows) {
						string alternateCaseFilename = char.ToLower (filename [0]) + filename.Substring (1);
						typesInFile = vm.GetTypesForSourceFile (alternateCaseFilename, false);
					}
				}
				
				foreach (TypeMirror t in typesInFile)
					ProcessType (t);
			}

			// Try already loaded types in the current source file
			List<TypeMirror> mirrors;

			if (source_to_type.TryGetValue (filename, out mirrors)) {
				foreach (TypeMirror type in mirrors) {
					bool genericMethod;
					bool insideRange;
					
					var loc = FindLocationByType (type, filename, line, column, out genericMethod, out insideRange);
					if (insideRange)
						insideLoadedRange = true;
					
					if (loc != null) {
						if (genericMethod || type.IsGenericType)
							genericTypeOrMethod = true;

						locations.Add (loc);
					}
				}
			}
			
			return locations;
		}
		
		public override bool CanCancelAsyncEvaluations {
			get {
				return Adaptor.IsEvaluating;
			}
		}
		
		protected override void OnCancelAsyncEvaluations ()
		{
			Adaptor.CancelAsyncOperations ();
		}
		
		protected override void OnNextInstruction ()
		{
			Step (StepDepth.Over, StepSize.Min);
		}

		protected override void OnNextLine ()
		{
			Step (StepDepth.Over, StepSize.Line);
		}
		
		void Step (StepDepth depth, StepSize size)
		{
			ThreadPool.QueueUserWorkItem (delegate {
				try {
					Adaptor.CancelAsyncOperations (); // This call can block, so it has to run in background thread to avoid keeping the main session lock
					var req = vm.CreateStepRequest (current_thread);
					req.Depth = depth;
					req.Size = size;
					req.Filter = StepFilter.StaticCtor | StepFilter.DebuggerHidden | StepFilter.DebuggerStepThrough;
					if (Options.ProjectAssembliesOnly)
						req.Filter |= StepFilter.DebuggerNonUserCode;
					if (assemblyFilters != null && assemblyFilters.Count > 0)
						req.AssemblyFilter = assemblyFilters;
					req.Enabled = true;
					currentStepRequest = req;
					OnResumed ();
					vm.Resume ();
					DequeueEventsForFirstThread ();
				} catch (CommandException ex) {
					string reason;

					switch (ex.ErrorCode) {
					case ErrorCode.INVALID_FRAMEID: reason = "invalid frame id"; break;
					case ErrorCode.NOT_SUSPENDED: reason = "VM not suspended"; break;
					case ErrorCode.ERR_UNLOADED: reason = "AppDomain has been unloaded"; break;
					case ErrorCode.NO_SEQ_POINT_AT_IL_OFFSET: reason = "no sequence point at the specified IL offset"; break;
					default: reason = ex.ErrorCode.ToString (); break;
					}

					OnDebuggerOutput (true, string.Format ("Step request failed: {0}.", reason));
					DebuggerLoggingService.LogError ("Step request failed", ex);
				} catch (Exception ex) {
					OnDebuggerOutput (true, string.Format ("Step request failed: {0}", ex.Message));
					DebuggerLoggingService.LogError ("Step request failed", ex);
				}
			});
		}

		void EventHandler ()
		{
			while (true) {
				try {
					EventSet e = vm.GetNextEventSet ();
					var type = e[0].EventType;
					if (type == EventType.VMDeath || type == EventType.VMDisconnect) {
						break;
					}
					HandleEventSet (e);
				} catch (Exception ex) {
					if (HasExited)
						break;

					if (!HandleException (ex))
						OnDebuggerOutput (true, ex.ToString ());

					if (ex is VMDisconnectedException || ex is IOException || ex is SocketException)
						break;
				}
			}
			
			try {
				// This is a workaround for a mono bug
				// Without this call, the process may become zombie in mono < 2.10.2
				if (vm.Process != null)
					vm.Process.WaitForExit (1);
			} catch (SystemException) {
			}

			OnTargetEvent (new TargetEventArgs (TargetEventType.TargetExited));
		}
		
		protected override bool HandleException (Exception ex)
		{
			HideConnectionDialog ();

			if (HasExited)
				return true;
			
			if (ex is VMDisconnectedException || ex is IOException) {
				ex = new DisconnectedException (ex);
				HasExited = true;
			} else if (ex is SocketException) {
				ex = new DebugSocketException (ex);
				HasExited = true;
			}
			
			return base.HandleException (ex);
		}

		void HandleEventSet (EventSet es)
		{
			var type = es[0].EventType;

#if DEBUG_EVENT_QUEUEING
			if (type != TypeLoadEvent)
				Console.WriteLine ("pp eventset({0}): {1}", es.Events.Length, es[0]);
#endif

			// If we are currently stopped on a thread, and the break events are on a different thread, we must queue
			// that event set and dequeue it next time we resume. This eliminates race conditions when multiple threads
			// hit breakpoints or catchpoints simultaneously.
			//
			bool isBreakEvent = type == EventType.Step || type == EventType.Breakpoint || type == EventType.Exception || type == EventType.UserBreak;
			if (isBreakEvent) {
				if (current_thread != null && es[0].Thread.Id != current_thread.Id) {
					QueueBreakEventSet (es.Events);
				} else {
					HandleBreakEventSet (es.Events, false);
				}
				return;
			}

			switch (type) {
			case EventType.AssemblyLoad:
				HandleAssemblyLoadEvents (Array.ConvertAll (es.Events, item => (AssemblyLoadEvent)item));
				break;
			case EventType.AssemblyUnload:
				HandleAssemblyUnloadEvents (Array.ConvertAll (es.Events, item => (AssemblyUnloadEvent)item));
				break;
			case EventType.VMStart:
				HandleVMStartEvents (Array.ConvertAll (es.Events, item => (VMStartEvent)item));
				break;
			case EventType.TypeLoad:
				HandleTypeLoadEvents (Array.ConvertAll (es.Events, item => (TypeLoadEvent)item));
				break;
			case EventType.ThreadStart:
				HandleThreadStartEvents (Array.ConvertAll (es.Events, item => (ThreadStartEvent)item));
				break;
			case EventType.ThreadDeath:
				HandleThreadDeathEvents (Array.ConvertAll (es.Events, item => (ThreadDeathEvent)item));
				break;
			case EventType.UserLog:
				HandleUserLogEvents (Array.ConvertAll (es.Events, item => (UserLogEvent)item));
				break;
			default:
				DebuggerLoggingService.LogMessage ("Ignoring unknown debugger event type {0}", type);
				break;
			}

			try {
				vm.Resume ();
			} catch (VMNotSuspendedException) {
				if (type != EventType.VMStart && vm.Version.AtLeast (2, 2))
					throw;
			}
		}
		
		static bool IsStepIntoRequest (StepEventRequest stepRequest)
		{
			return stepRequest.Depth == StepDepth.Into;
		}
		
		static bool IsStepOutRequest (StepEventRequest stepRequest)
		{
			return stepRequest.Depth == StepDepth.Out;
		}
		
		static bool IsPropertyOrOperatorMethod (MethodMirror method)
		{
			string name = method.Name;
			
			return method.IsSpecialName &&
				(name.StartsWith ("get_", StringComparison.Ordinal) ||
				 name.StartsWith ("set_", StringComparison.Ordinal) ||
				 name.StartsWith ("op_", StringComparison.Ordinal));
		}

		bool IsUserAssembly (AssemblyMirror assembly)
		{
			if (userAssemblyNames == null)
				return true;

			var name = assembly.GetName ().FullName;

			foreach (var n in userAssemblyNames) {
				if (n == name)
					return true;
			}

			return false;
		}

		bool IsAutoGeneratedFrameworkEnumerator (TypeMirror type)
		{
			if (IsUserAssembly (type.Assembly))
				return false;

			if (!SoftDebuggerAdaptor.IsGeneratedType (type))
				return false;

			foreach (var iface in type.GetInterfaces ()) {
				if (iface.Namespace == "System.Collections" && iface.Name == "IEnumerator")
					return true;
			}

			return false;
		}

		bool StepThrough (MethodMirror method)
		{
			if (Options.ProjectAssembliesOnly && !IsUserAssembly (method.DeclaringType.Assembly))
				return true;

			if (vm.Version.AtLeast (2, 21)) {
				foreach (var attr in method.GetCustomAttributes (false)) {
					var attrName = attr.Constructor.DeclaringType.FullName;

					switch (attrName) {
					case "System.Diagnostics.DebuggerHiddenAttribute":      return true;
					case "System.Diagnostics.DebuggerStepThroughAttribute": return true;
					case "System.Diagnostics.DebuggerNonUserCodeAttribute": return Options.ProjectAssembliesOnly;
					}
				}
			}

			if (Options.ProjectAssembliesOnly) {
				foreach (var attr in method.DeclaringType.GetCustomAttributes (false)) {
					var attrName = attr.Constructor.DeclaringType.FullName;

					if (attrName == "System.Diagnostics.DebuggerNonUserCodeAttribute")
						return Options.ProjectAssembliesOnly;
				}
			}

			return false;
		}

		bool ContinueOnStepInto (MethodMirror method)
		{
			if (vm.Version.AtLeast (2, 21)) {
				foreach (var attr in method.GetCustomAttributes (false)) {
					var attrName = attr.Constructor.DeclaringType.FullName;

					if (attrName == "System.Diagnostics.DebuggerStepperBoundaryAttribute")
						return true;
				}
			}

			return false;
		}

		bool IgnoreBreakpoint (MethodMirror method)
		{
			if (Options.ProjectAssembliesOnly && !IsUserAssembly (method.DeclaringType.Assembly))
				return true;

			if (vm.Version.AtLeast (2, 21)) {
				foreach (var attr in method.GetCustomAttributes (false)) {
					var attrName = attr.Constructor.DeclaringType.FullName;

					switch (attrName) {
					case "System.Diagnostics.DebuggerHiddenAttribute":      return true;
					case "System.Diagnostics.DebuggerStepThroughAttribute": return true;
					case "System.Diagnostics.DebuggerNonUserCodeAttribute": return Options.ProjectAssembliesOnly;
					case "System.Diagnostics.DebuggerStepperBoundaryAttribute": return true;
					}
				}
			}

			if (Options.ProjectAssembliesOnly) {
				foreach (var attr in method.DeclaringType.GetCustomAttributes (false)) {
					var attrName = attr.Constructor.DeclaringType.FullName;

					if (attrName == "System.Diagnostics.DebuggerNonUserCodeAttribute")
						return Options.ProjectAssembliesOnly;
				}
			}

			return false;
		}
		
		void HandleBreakEventSet (Event[] es, bool dequeuing)
		{
			if (dequeuing && HasExited)
				return;

			TargetEventType etype = TargetEventType.TargetStopped;
			ObjectMirror exception = null;
			BreakEvent breakEvent = null;
			bool redoCurrentStep = false;
			bool steppedInto = false;
			bool steppedOut = false;
			bool resume = true;
			
			if (es [0].EventType == EventType.Exception) {
				var bad = es.FirstOrDefault (ee => ee.EventType != EventType.Exception);
				if (bad != null)
					throw new Exception ("Catchpoint eventset had unexpected event type " + bad.GetType ());
				var ev = (ExceptionEvent)es [0];
				exception = ev.Exception;
				if (ev.Request == unhandledExceptionRequest) {
					etype = TargetEventType.UnhandledException;
					if (exception.Type.FullName != "System.Threading.ThreadAbortException")
						resume = false;
				} else {
					//Set exception for this thread so CatchPoint Print message(tracing) of {$exception} work
					activeExceptionsByThread [es[0].Thread.ThreadId] = exception;
					if (!HandleBreakpoint (es [0].Thread, ev.Request)) {
						etype = TargetEventType.ExceptionThrown;
						resume = false;
					}
					//Remove exception from thread so when program stops because stepFinished/programPause/breakPoint...
					//we don't have outdatted exception
					activeExceptionsByThread.Remove (es[0].Thread.ThreadId);
				}
			} else {
				//always need to evaluate all breakpoints, some might be tracepoints or conditional bps with counters
				foreach (Event e in es) {
					if (e.EventType == EventType.Breakpoint) {
						var be = (BreakpointEvent) e;
						BreakInfo binfo;

						if (!HandleBreakpoint (e.Thread, be.Request)) {
							etype = TargetEventType.TargetHitBreakpoint;
							autoStepInto = false;
							resume = false;
						}
						
						if (breakpoints.TryGetValue (be.Request, out binfo)) {
							if (currentStepRequest != null &&
							    binfo.Location.ILOffset == currentAddress && 
							    e.Thread.Id == currentStepRequest.Thread.Id)
								redoCurrentStep = true;
							
							breakEvent = binfo.BreakEvent;
						}
					} else if (e.EventType == EventType.Step) {
						var stepRequest = e.Request as StepEventRequest;
						steppedInto = IsStepIntoRequest (stepRequest);
						steppedOut = IsStepOutRequest (stepRequest);
						etype = TargetEventType.TargetStopped;
						resume = false;
					} else if (e.EventType == EventType.UserBreak) {
						etype = TargetEventType.TargetStopped;
						autoStepInto = false;
						resume = false;
					} else {
						throw new Exception ("Break eventset had unexpected event type " + e.GetType ());
					}
				}
			}
			
			if (redoCurrentStep) {
				StepDepth depth = currentStepRequest.Depth;
				StepSize size = currentStepRequest.Size;
				
				current_thread = recent_thread = es[0].Thread;
				currentStepRequest.Enabled = false;
				currentStepRequest = null;
				
				Step (depth, size);
			} else if (resume) {
				// all breakpoints were conditional and evaluated as false
				vm.Resume ();
				DequeueEventsForFirstThread ();
			} else {
				if (currentStepRequest != null) {
					currentStepRequest.Enabled = false;
					currentStepRequest = null;
				}
				
				current_thread = recent_thread = es[0].Thread;
				
				if (exception != null)
					activeExceptionsByThread [current_thread.ThreadId] = exception;
				
				var backtrace = GetThreadBacktrace (current_thread);
				bool stepInto = false;
				bool stepOut = false;
				
				if (backtrace.FrameCount > 0) {
					var frame = backtrace.GetFrame (0) as SoftDebuggerStackFrame;
					currentAddress = frame != null ? frame.Address : -1;

					if (frame != null && steppedInto) {
						if (ContinueOnStepInto (frame.StackFrame.Method)) {
							vm.Resume ();
							DequeueEventsForFirstThread ();
							return;
						}

						if (StepThrough (frame.StackFrame.Method)) {
							// The method has a Debugger[Hidden,StepThrough,NonUserCode]Attribute on it
							// Keep calling StepInto until we land somewhere without one of these attributes
							stepInto = true;
						} else if (Options.StepOverPropertiesAndOperators && IsPropertyOrOperatorMethod (frame.StackFrame.Method)) {
							// We will want to call StepInto once StepOut returns...
							autoStepInto = true;
							stepOut = true;
						} else if (IsAutoGeneratedFrameworkEnumerator (frame.StackFrame.Method.DeclaringType)) {
							// User asked to step in, but we landed in an autogenerated type (probably an iterator)
							autoStepInto = true;
							stepOut = true;
						}
					} else if (breakEvent != null && IgnoreBreakpoint (frame.StackFrame.Method)) {
						vm.Resume ();
						DequeueEventsForFirstThread ();
						return;
					}
				}

				if (stepOut) {
					Step (StepDepth.Out, StepSize.Min);
				} else if (stepInto) {
					Step (StepDepth.Into, StepSize.Min);
				} else if (steppedOut && autoStepInto) {
					autoStepInto = false;
					Step (StepDepth.Into, StepSize.Min);
				} else {
					var args = new TargetEventArgs (etype);
					args.Process = OnGetProcesses () [0];
					args.Thread = GetThread (args.Process, current_thread);
					args.Backtrace = backtrace;
					args.BreakEvent = breakEvent;
					
					OnTargetEvent (args);
				}
			}
		}

		void HandleAssemblyLoadEvents (AssemblyLoadEvent[] events)
		{
			var asm = events [0].Assembly;
			if (events.Length > 1 && events.Any (a => a.Assembly != asm))
				throw new InvalidOperationException ("Simultaneous AssemblyLoadEvent for multiple assemblies");

			bool isExternal;
			lock (pending_bes) {
				isExternal = !UpdateAssemblyFilters (asm) && userAssemblyNames != null;
			}

			string flagExt = isExternal ? " [External]" : "";
			OnDebuggerOutput (false, string.Format ("Loaded assembly: {0}{1}\n", asm.Location, flagExt));
		}

		void HandleAssemblyUnloadEvents (AssemblyUnloadEvent[] events)
		{
			var asm = events [0].Assembly;
			if (events.Length > 1 && events.Any (a => a.Assembly != asm))
				throw new InvalidOperationException ("Simultaneous AssemblyUnloadEvents for multiple assemblies");

			lock (pending_bes) {
				if (assemblyFilters != null) {
					int index = assemblyFilters.IndexOf (asm);
					if (index != -1)
						assemblyFilters.RemoveAt (index);
				}
				// Mark affected breakpoints as pending again
				var affectedBreakpoints = new List<KeyValuePair<EventRequest, BreakInfo>> (breakpoints.Where (x => PathComparer.Equals (x.Value.Location.Method.DeclaringType.Assembly.Location, asm.Location)));
				foreach (KeyValuePair<EventRequest, BreakInfo> breakpoint in affectedBreakpoints) {
					string file = PathToFileName (breakpoint.Value.Location.SourceFile);
					int line = breakpoint.Value.Location.LineNumber;
					OnDebuggerOutput (false, string.Format ("Re-pending breakpoint at {0}:{1}\n", file, line));
					breakpoints.Remove (breakpoint.Key);
					pending_bes.Add (breakpoint.Value);
				}

				// Remove affected types from the loaded types list
				var affectedTypes = new List<string> (from pair in types
					 where PathComparer.Equals (pair.Value.Assembly.Location, asm.Location)
					 select pair.Key);

				foreach (string typeName in affectedTypes) {
					TypeMirror tm;

					if (types.TryGetValue (typeName, out tm)) {
						if (tm.IsNested)
							aliases.Remove (NestedTypeNameToAlias (typeName));

						types.Remove (typeName);
					}
				}

				foreach (var pair in source_to_type) {
					pair.Value.RemoveAll (m => PathComparer.Equals (m.Assembly.Location, asm.Location));
				}
			}
			OnDebuggerOutput (false, string.Format ("Unloaded assembly: {0}\n", asm.Location));
		}

		void HandleVMStartEvents (VMStartEvent[] events)
		{
			var thread = events [0].Thread;
			if (events.Length > 1)
				throw new InvalidOperationException ("Simultaneous VMStartEvents");

			OnStarted (new ThreadInfo (0, GetId (thread), GetThreadName (thread), null));
			//HACK: 2.6.1 VM doesn't emit type load event, so work around it
			var t = vm.RootDomain.Corlib.GetType ("System.Exception", false, false);
			if (t != null) {
				lock (pending_bes) {
					ResolveBreakpoints (t);
				}
			}
		}

		void HandleTypeLoadEvents (TypeLoadEvent[] events)
		{
			var type = events [0].Type;
			if (events.Length > 1 && events.Any (a => a.Type != type))
				throw new InvalidOperationException ("Simultaneous TypeLoadEvents for multiple types");

			lock (pending_bes) {
				if (!types.ContainsKey (type.FullName))
					ResolveBreakpoints (type);
			}
		}

		void HandleThreadStartEvents (ThreadStartEvent[] events)
		{
			var thread = events [0].Thread;
			if (events.Length > 1 && events.Any (a => a.Thread != thread))
				throw new InvalidOperationException ("Simultaneous ThreadStartEvents for multiple threads");

			var name = GetThreadName (thread);
			var id = GetId (thread);
			OnDebuggerOutput (false, string.Format ("Thread started: {0} #{1}\n", name, id));
			OnTargetEvent (new TargetEventArgs (TargetEventType.ThreadStarted) {
				Thread = new ThreadInfo (0, id, name, null),
			});
		}

		void HandleThreadDeathEvents (ThreadDeathEvent[] events)
		{
			var thread = events [0].Thread;
			if (events.Length > 1 && events.Any (a => a.Thread != thread))
				throw new InvalidOperationException ("Simultaneous ThreadDeathEvents for multiple threads");

			var name = GetThreadName (thread);
			var id = GetId (thread);
			OnDebuggerOutput (false, string.Format ("Thread finished: {0} #{1}\n", name, id));
			OnTargetEvent (new TargetEventArgs (TargetEventType.ThreadStopped) {
				Thread = new ThreadInfo (0, id, name, null),
			});
		}

		void HandleUserLogEvents (UserLogEvent[] events)
		{
			foreach (var ul in events)
				OnTargetDebug (ul.Level, ul.Category, ul.Message);
		}

		public ObjectMirror GetExceptionObject (ThreadMirror thread)
		{
			ObjectMirror obj;

			return activeExceptionsByThread.TryGetValue (thread.ThreadId, out obj) ? obj : null;
		}
		
		void QueueBreakEventSet (Event[] eventSet)
		{
#if DEBUG_EVENT_QUEUEING
			Console.WriteLine ("qq eventset({0}): {1}", eventSet.Length, eventSet[0]);
#endif
			var events = new List<Event> (eventSet);
			lock (queuedEventSets) {
				queuedEventSets.AddLast (events);
			}
		}
		
		void RemoveQueuedBreakEvents (List<EventRequest> requests)
		{
			int resume = 0;
			
			lock (queuedEventSets) {
				var node = queuedEventSets.First;
				
				while (node != null) {
					List<Event> q = node.Value;
					
					for (int i = 0; i < q.Count; i++) {
						foreach (var request in requests) {
							if (q[i].Request == request) {
								q.RemoveAt (i--);
								break;
							}
						}
					}
					
					if (q.Count == 0) {
						var d = node;
						node = node.Next;
						queuedEventSets.Remove (d);
						resume++;
					} else {
						node = node.Next;
					}
				}
			}
			
			for (int i = 0; i < resume; i++)
				vm.Resume ();
		}
		
		void DequeueEventsForFirstThread ()
		{
			List<List<Event>> dequeuing;
			lock (queuedEventSets) {
				if (queuedEventSets.Count < 1)
					return;
				
				dequeuing = new List<List<Event>> ();
				var node = queuedEventSets.First;
				
				//making this the current thread means that all events from other threads will get queued
				current_thread = node.Value[0].Thread;
				while (node != null) {
					if (node.Value[0].Thread.Id == current_thread.Id) {
						var d = node;
						node = node.Next;
						dequeuing.Add (d.Value);
						queuedEventSets.Remove (d);
					} else {
						node = node.Next;
					}
				}
			}

#if DEBUG_EVENT_QUEUEING
			foreach (var e in dequeuing)
				Console.WriteLine ("dq eventset({0}): {1}", e.Count, e[0]);
#endif

			//firing this off in a thread prevents possible infinite recursion
			ThreadPool.QueueUserWorkItem (delegate {
				if (!HasExited) {
					foreach (var es in dequeuing) {
						try {
							 HandleBreakEventSet (es.ToArray (), true);
						} catch (Exception ex) {
							if (!HandleException (ex))
								OnDebuggerOutput (true, ex.ToString ());

							if (ex is VMDisconnectedException || ex is IOException || ex is SocketException) {
								OnTargetEvent (new TargetEventArgs (TargetEventType.TargetExited));
								break;
							}
						}
					}
				}
			});
		}
		
		bool HandleBreakpoint (ThreadMirror thread, EventRequest er)
		{
			BreakInfo binfo;
			if (!breakpoints.TryGetValue (er, out binfo))
				return false;
			
			var bp = binfo.BreakEvent;
			if (bp == null)
				return false;

			binfo.IncrementHitCount ();
			if (!binfo.HitCountReached)
				return true;
			
			if (!string.IsNullOrEmpty (bp.ConditionExpression)) {
				string res = EvaluateExpression (thread, bp.ConditionExpression, bp);
				if (bp.BreakIfConditionChanges) {
					if (res == binfo.LastConditionValue)
						return true;
					binfo.LastConditionValue = res;
				} else {
					if (res == null || res.ToLowerInvariant () != "true")
						return true;
				}
			}
			if ((bp.HitAction & HitAction.CustomAction) != HitAction.None) {
				// If custom action returns true, execution must continue
				return binfo.RunCustomBreakpointAction (bp.CustomActionId);
			}

			if ((bp.HitAction & HitAction.PrintExpression) != HitAction.None) {
				string exp = EvaluateTrace (thread, bp.TraceExpression);
				binfo.UpdateLastTraceValue (exp);
			}

			// Continue execution if we don't have break action.
			return (bp.HitAction & HitAction.Break) == HitAction.None;
		}
		
		string EvaluateTrace (ThreadMirror thread, string exp)
		{
			var sb = new StringBuilder ();
			int last = 0;
			int i = exp.IndexOf ('{');
			while (i != -1) {
				if (i < exp.Length - 1 && exp [i+1] == '{') {
					sb.Append (exp.Substring (last, i - last + 1));
					last = i + 2;
					i = exp.IndexOf ('{', i + 2);
					continue;
				}
				int j = exp.IndexOf ('}', i + 1);
				if (j == -1)
					break;
				string se = exp.Substring (i + 1, j - i - 1);
				se = EvaluateExpression (thread, se, null);
				sb.Append (exp.Substring (last, i - last));
				sb.Append (se);
				last = j + 1;
				i = exp.IndexOf ('{', last);
			}
			sb.Append (exp.Substring (last, exp.Length - last));
			return sb.ToString ();
		}

		static SourceLocation GetSourceLocation (MDB.StackFrame frame)
		{
			return new SourceLocation (frame.Method.Name, frame.FileName, frame.LineNumber, frame.ColumnNumber);
		}

		static string FormatSourceLocation (BreakEvent breakEvent)
		{
			var bp = breakEvent as Breakpoint;
			if (bp == null || string.IsNullOrEmpty (bp.FileName))
				return null;

			var location = Path.GetFileName (bp.FileName);
			if (bp.OriginalLine > 0) {
				location += ":" + bp.OriginalLine;
				if (bp.OriginalColumn > 0)
					location += "," + bp.OriginalColumn;
			}

			return location;
		}

		static bool IsBoolean (ValueReference vr)
		{
			if (vr.Type is Type && ((Type) vr.Type) == typeof (bool))
				return true;

			if (vr.Type is TypeMirror && ((TypeMirror) vr.Type).FullName == "System.Boolean")
				return true;

			return false;
		}
		
		string EvaluateExpression (ThreadMirror thread, string expression, BreakEvent bp)
		{
			try {
				var frames = thread.GetFrames ();
				if (frames.Length == 0)
					return string.Empty;

				EvaluationOptions ops = Options.EvaluationOptions.Clone ();
				ops.AllowTargetInvoke = true;

				var ctx = new SoftEvaluationContext (this, frames[0], ops);

				if (bp != null) {
					// validate conditional breakpoint expressions so that we can provide error reporting to the user
					var vr = ctx.Evaluator.ValidateExpression (ctx, expression);
					if (!vr.IsValid) {
						string message = string.Format ("Invalid expression in conditional breakpoint. {0}", vr.Message);
						string location = FormatSourceLocation (bp);

						if (!string.IsNullOrEmpty (location))
							message = location + ": " + message;

						OnDebuggerOutput (true, message);
						return string.Empty;
					}

					// resolve types...
					if (ctx.SourceCodeAvailable)
						expression = ctx.Evaluator.Resolve (this, GetSourceLocation (frames[0]), expression);
				}

				ValueReference val = ctx.Evaluator.Evaluate (ctx, expression);
				if (bp != null && !bp.BreakIfConditionChanges && !IsBoolean (val)) {
					string message = string.Format ("Expression in conditional breakpoint did not evaluate to a boolean value: {0}", bp.ConditionExpression);
					string location = FormatSourceLocation (bp);

					if (!string.IsNullOrEmpty (location))
						message = location + ": " + message;

					OnDebuggerOutput (true, message);
					return string.Empty;
				}

				return val.CreateObjectValue (false).Value;
			} catch (EvaluatorException ex) {
				string message;

				if (bp != null) {
					message = string.Format ("Failed to evaluate expression in conditional breakpoint. {0}", ex.Message);
					string location = FormatSourceLocation (bp);

					if (!string.IsNullOrEmpty (location))
						message = location + ": " + message;
				} else {
					message = ex.ToString ();
				}

				OnDebuggerOutput (true, message);
				return string.Empty;
			} catch (Exception ex) {
				OnDebuggerOutput (true, ex.ToString ());
				return string.Empty;
			}
		}

		static string NestedTypeNameToAlias (string typeName)
		{
			int index = typeName.IndexOfAny (new [] { '[', ',' });

			if (index == -1)
				return typeName.Replace ('+', '.');

			var prefix = typeName.Substring (0, index).Replace ('+', '.');
			var suffix = typeName.Substring (index);

			return prefix + suffix;
		}
		
		void ProcessType (TypeMirror t)
		{
			string typeName = t.FullName;

			if (types.ContainsKey (typeName))
				return;

			if (t.IsNested)
				aliases[NestedTypeNameToAlias (typeName)] = t;

			types[typeName] = t;

			//get the source file paths
			//full paths, from GetSourceFiles (true), are only supported by sdb protocol 2.2 and later
			string[] sourceFiles;
			if (useFullPaths) {
				sourceFiles = t.GetSourceFiles (true);
			} else {
				sourceFiles = t.GetSourceFiles ();
				
				//HACK: if mdb paths are windows paths but the sdb agent is on unix, it won't map paths to filenames correctly
				if (IsWindows) {
					for (int i = 0; i < sourceFiles.Length; i++) {
						string s = sourceFiles[i];
						if (s != null && !s.StartsWith ("/", StringComparison.Ordinal))
							sourceFiles[i] = Path.GetFileName (s);
					}
				}
			}
			
			for (int n = 0; n < sourceFiles.Length; n++)
				sourceFiles[n] = NormalizePath (sourceFiles[n]);

			foreach (string s in sourceFiles) {
				List<TypeMirror> typesList;
				
				if (source_to_type.TryGetValue (s, out typesList)) {
					typesList.Add (t);
				} else {
					typesList = new List<TypeMirror> ();
					typesList.Add (t);
					source_to_type[s] = typesList;
				}
			}

			type_to_source [t] = sourceFiles;
		}
		
		static string[] GetParamTypes (MethodMirror method)
		{
			var paramTypes = new List<string> ();
			
			foreach (var param in method.GetParameters ())
				paramTypes.Add (param.ParameterType.CSharpName);
			
			return paramTypes.ToArray ();
		}

		string GetPrettyMethodName (MethodMirror method)
		{
			var name = new StringBuilder ();

			name.Append (Adaptor.GetDisplayTypeName (method.ReturnType.FullName));
			name.Append (" ");
			name.Append (Adaptor.GetDisplayTypeName (method.DeclaringType.FullName));
			name.Append (".");
			name.Append (method.Name);

			if (method.VirtualMachine.Version.AtLeast (2, 12)) {
				if (method.IsGenericMethodDefinition || method.IsGenericMethod) {
					name.Append ("<");
					if (method.VirtualMachine.Version.AtLeast (2, 15)) {
						var argTypes = method.GetGenericArguments ();
						for (int i = 0; i < argTypes.Length; i++) {
							if (i != 0)
								name.Append (", ");
							name.Append (Adaptor.GetDisplayTypeName (argTypes[i].FullName));
						}
					}
					name.Append (">");
				}
			}

			name.Append (" (");
			var @params = method.GetParameters ();
			for (int i = 0; i < @params.Length; i++) {
				if (i != 0)
					name.Append (", ");
				if (@params[i].Attributes.HasFlag (ParameterAttributes.Out)) {
					if (@params[i].Attributes.HasFlag (ParameterAttributes.In))
						name.Append ("ref ");
					else
						name.Append ("out ");
				}
				name.Append (Adaptor.GetDisplayTypeName (@params[i].ParameterType.FullName));
				name.Append (" ");
				name.Append (@params[i].Name);
			}
			name.Append (")");

			return name.ToString ();
		}

		void ResolveBreakpoints (TypeMirror type)
		{
			var resolved = new List<BreakInfo> ();
			Location loc;
			
			ProcessType (type);
			
			// First, resolve FunctionBreakpoints
			foreach (var bi in pending_bes.Where (b => b.BreakEvent is FunctionBreakpoint)) {
				if (CheckTypeName (type, bi.TypeName)) {
					var bp = (FunctionBreakpoint) bi.BreakEvent;
					string methodName;

					if (!string.IsNullOrEmpty (bi.TypeName))
						methodName = bp.FunctionName.Substring (bi.TypeName.Length + 1);
					else
						methodName = bp.FunctionName;
					
					foreach (var method in type.GetMethodsByNameFlags (methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static, false)) {
						if (!CheckMethodParams (method, bp.ParamTypes))
							continue;
						
						loc = GetLocFromMethod (method);
						if (loc != null) {
							string paramList = "(" + string.Join (", ", bp.ParamTypes ?? GetParamTypes (method)) + ")";
							OnDebuggerOutput (false, string.Format ("Resolved pending breakpoint for '{0}{1}' to {2}:{3} [0x{4:x5}].\n",
							                                        bp.FunctionName, paramList, loc.SourceFile, loc.LineNumber, loc.ILOffset));

							ResolvePendingBreakpoint (bi, loc);
							
							// Note: if the type or method is generic, there may be more instances so don't assume we are done resolving the breakpoint
							if (bp.ParamTypes != null && !type.IsGenericType && !IsGenericMethod (method))
								resolved.Add (bi);
						}
					}
				}
			}
			
			foreach (var be in resolved)
				pending_bes.Remove (be);
			resolved.Clear ();

			// Now resolve normal Breakpoints
			foreach (string s in type_to_source [type]) {
				foreach (var bi in pending_bes.Where (b => (b.BreakEvent is Breakpoint) && !(b.BreakEvent is FunctionBreakpoint))) {
					var bp = (Breakpoint) bi.BreakEvent;
					if (PathsAreEqual (PathToFileName (bp.FileName), s)) {
						bool insideLoadedRange;
						bool genericMethod;
						
						loc = FindLocationByType (type, s, bp.Line, bp.Column, out genericMethod, out insideLoadedRange);
						if (loc != null) {
							OnDebuggerOutput (false, string.Format ("Resolved pending breakpoint at '{0}:{1},{2}' to {3} [0x{4:x5}].\n",
							                                        s, bp.Line, bp.Column, GetPrettyMethodName (loc.Method), loc.ILOffset));
							ResolvePendingBreakpoint (bi, loc);
							
							// Note: if the type or method is generic, there may be more instances so don't assume we are done resolving the breakpoint
							if (!genericMethod && !type.IsGenericType)
								resolved.Add (bi);
						} else {
							if (insideLoadedRange) {
								bi.SetStatus (BreakEventStatus.Invalid, null);
							}
						}
					}
				}
				
				foreach (var be in resolved)
					pending_bes.Remove (be);
				resolved.Clear ();
			}
			
			// Thirdly, resolve pending catchpoints
			foreach (var bi in pending_bes.Where (b => b.BreakEvent is Catchpoint)) {
				var cp = (Catchpoint) bi.BreakEvent;
				if (cp.ExceptionName == type.FullName) {
					ResolvePendingCatchpoint (bi, type);
					resolved.Add (bi);
				}
			}
			
			foreach (var be in resolved)
				pending_bes.Remove (be);
		}
		
		internal static string NormalizePath (string path)
		{
			if (!IsWindows && path.StartsWith ("\\", StringComparison.Ordinal))
				return path.Replace ('\\', '/');

			return path;
		}
		
		string PathToFileName (string path)
		{
			return useFullPaths ? path : Path.GetFileName (path);
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
		
		static bool PathsAreEqual (string p1, string p2)
		{
			if (string.IsNullOrWhiteSpace (p1) || string.IsNullOrWhiteSpace (p2))
				return false;

			if (PathComparer.Compare (p1, p2) == 0)
				return true;

			var rp1 = ResolveFullPath (p1);
			var rp2 = ResolveFullPath (p2);

			return PathComparer.Compare (rp1, rp2) == 0;
		}
		
		static Location GetLocFromMethod (MethodMirror method)
		{
			// Return the location of the method.
			return method.Locations.Count > 0 ? method.Locations[0] : null;
		}
		
		bool CheckBetterMatch (TypeMirror type, string file, int line, Location found)
		{
			if (type.Assembly == null)
				return false;
			
			string assemblyFileName;
			if (!assemblyPathMap.TryGetValue (type.Assembly.GetName ().FullName, out assemblyFileName))
				assemblyFileName = type.Assembly.Location;
			
			if (assemblyFileName == null)
				return false;
			
			string mdbFileName = assemblyFileName + ".mdb";
			int foundDelta = found.LineNumber - line;
			MonoSymbolFile mdb;
			int fileId = -1;
			
			try {
				if (!symbolFiles.TryGetValue (mdbFileName, out mdb)) {
					if (!File.Exists (mdbFileName))
						return false;
					
					mdb = MonoSymbolFile.ReadSymbolFile (mdbFileName);
					symbolFiles.Add (mdbFileName, mdb);
				}
			} catch {
				return false;
			}

			foreach (var src in mdb.Sources) {
				if (src.FileName == file) {
					fileId = src.Index;
					break;
				}
			}

			if (fileId == -1)
				return false;

			foreach (var method in mdb.Methods) {
				var table = method.GetLineNumberTable ();
				foreach (var entry in table.LineNumbers) {
					if (entry.File != fileId)
						continue;

					if (entry.Row >= line && (entry.Row - line) < foundDelta)
						return true;
				}
			}

			return false;
		}

		Location FindLocationByMethod (MethodMirror method, string file, int line, ref bool fuzzy, ref bool insideTypeRange, out List<Location> locations)
		{
			int rangeFirstLine = int.MaxValue;
			int rangeLastLine = -1;
			Location target = null;

			locations = new List<Location> ();

			foreach (var location in method.Locations) {
				string srcFile = location.SourceFile;

				//Console.WriteLine ("\tExamining {0}:{1}...", srcFile, location.LineNumber);

				if (srcFile != null && PathsAreEqual (PathToFileName (NormalizePath (srcFile)), file)) {
					if (location.LineNumber < rangeFirstLine)
						rangeFirstLine = location.LineNumber;

					if (location.LineNumber > rangeLastLine)
						rangeLastLine = location.LineNumber;

					if (line >= rangeFirstLine && line <= rangeLastLine)
						insideTypeRange = true;

					if (location.LineNumber >= line && line >= rangeFirstLine) {
						if (target != null) {
							if (location.LineNumber > line) {
								if (target.LineNumber - line > location.LineNumber - line) {
									// Grab the location closest to the requested line
									//Console.WriteLine ("\t\tLocation is closest match. (ILOffset = 0x{0:x5})", location.ILOffset);
									locations.Clear ();
									locations.Add (location);
									target = location;
								}
							} else if (target.LineNumber != line) {
								// Previous match was a fuzzy match, but now we've found an exact line match
								//Console.WriteLine ("\t\tLocation is exact line match. (ILOffset = 0x{0:x5})", location.ILOffset);
								locations.Clear ();
								locations.Add (location);
								target = location;
								fuzzy = false;
							} else {
								// Line number matches exactly, use the location with the lowest ILOffset
								if (location.ILOffset < target.ILOffset)
									target = location;

								locations.Add (location);
								fuzzy = false;
							}
						} else {
							//Console.WriteLine ("\t\tLocation is first possible match. (ILOffset = 0x{0:x5})", location.ILOffset);
							fuzzy = location.LineNumber != line;
							locations.Add (location);
							target = location;
						}
					}
				} else {
					rangeFirstLine = int.MaxValue;
					rangeLastLine = -1;
				}
			}

			return target;
		}

		Location FindLocationByMethod (MethodMirror method, string file, int line, int column)
		{
			bool insideTypeRange = false;
			List<Location> locations;
			bool fuzzy = true;
			Location target;

			if ((target = FindLocationByMethod (method, file, line, ref fuzzy, ref insideTypeRange, out locations)) != null) {
				// If we got a fuzzy match, then we need to make sure that there isn't a better
				// match in another method (e.g. code might have been extracted out into another
				// method by the compiler.
				if (!fuzzy) {
					// Exact line match... now find the best column match.
					locations.Sort (new LocationComparer ());

					// Find the closest-matching location based on column.
					target = locations[0];
					for (int i = 1; i < locations.Count; i++) {
						if (locations[i].ColumnNumber > column)
							break;

						// if the column numbers match, then target_loc should have the lower ILOffset (which we want)
						if (target.ColumnNumber == locations[i].ColumnNumber)
							continue;

						target = locations[i];
					}

					return target;
				}
			}

			if (target != null && fuzzy && CheckBetterMatch (method.DeclaringType, file, line, target))
				return null;

			return target;
		}

		Location FindLocationByType (TypeMirror type, string file, int line, int column, out bool genericMethod, out bool insideTypeRange)
		{
			Location target = null;
			bool fuzzy = true;

			insideTypeRange = false;
			genericMethod = false;

			//Console.WriteLine ("Trying to resolve {0}:{1},{2} in type {3}", file, line, column, type.Name);
			foreach (var method in type.GetMethods ()) {
				List<Location> locations;

				if ((target = FindLocationByMethod (method, file, line, ref fuzzy, ref insideTypeRange, out locations)) != null) {
					genericMethod = IsGenericMethod (method);
					
					// If we got a fuzzy match, then we need to make sure that there isn't a better
					// match in another method (e.g. code might have been extracted out into another
					// method by the compiler.
					if (!fuzzy) {
						// Exact line match... now find the best column match.
						locations.Sort (new LocationComparer ());

						// Find the closest-matching location based on column.
						target = locations[0];
						for (int i = 1; i < locations.Count; i++) {
							if (locations[i].ColumnNumber > column)
								break;

							// if the column numbers match, then target_loc should have the lower ILOffset (which we want)
							if (target.ColumnNumber == locations[i].ColumnNumber)
								continue;

							target = locations[i];
						}

						return target;
					}
				}
			}
			
			if (target != null && fuzzy && CheckBetterMatch (type, file, line, target)) {
				insideTypeRange = false;
				return null;
			}
			
			return target;
		}

		void ResolvePendingBreakpoint (BreakInfo bi, Location l)
		{
			bi.Location = l;
			InsertBreakpoint ((Breakpoint) bi.BreakEvent, bi);
			bi.SetStatus (BreakEventStatus.Bound, null);
		}
				
		void ResolvePendingCatchpoint (BreakInfo bi, TypeMirror type)
		{
			InsertCatchpoint ((Catchpoint) bi.BreakEvent, bi, type);
			bi.SetStatus (BreakEventStatus.Bound, null);
		}
		
		bool UpdateAssemblyFilters (AssemblyMirror asm)
		{
			var name = asm.GetName ().FullName;
			bool found = false;
			if (userAssemblyNames != null) {
				//HACK: not sure how else to handle xsp-compiled pages
				if (name.StartsWith ("App_", StringComparison.Ordinal)) {
					found = true;
				} else {
					foreach (var n in userAssemblyNames) {
						if (n == name) {
							found = true;
							break;
						}
					}
				}
			}

			if (found) {
				assemblyFilters.Add (asm);
				return true;
			}

			return false;
		}
		
		internal void WriteDebuggerOutput (bool isError, string msg)
		{
			OnDebuggerOutput (isError, msg);
		}
		
		protected override void OnSetActiveThread (long processId, long threadId)
		{
		}

		protected override void OnStepInstruction ()
		{
			Step (StepDepth.Into, StepSize.Min);
		}

		protected override void OnStepLine ()
		{
			Step (StepDepth.Into, StepSize.Line);
		}

		protected override void OnStop ()
		{
			vm.Suspend ();
			
			//emit a stop event at the current position of the most recent thread
			//we use "getprocesses" instead of "ongetprocesses" because it attaches the process to the session
			//using private Mono.Debugging API, so our thread/backtrace calls will cache stuff that will get used later
			var process = GetProcesses () [0];				
			EnsureRecentThreadIsValid (process);
			current_thread = recent_thread;
			OnTargetEvent (new TargetEventArgs (TargetEventType.TargetStopped) {
				Process = process,
				Thread = GetThread (process, recent_thread),
				Backtrace = GetThreadBacktrace (recent_thread)});
		}
		
		void EnsureRecentThreadIsValid (ProcessInfo process)
		{
			var infos = process.GetThreads ();
			
			if (ThreadIsAlive (recent_thread) && HasUserFrame (GetId (recent_thread), infos))
				return;

			var threads = vm.GetThreads ();
			foreach (var thread in threads) {
				if (ThreadIsAlive (thread) && HasUserFrame (GetId (thread), infos)) {
					recent_thread = thread;
					return;
				}
			}
			recent_thread = threads[0];	
		}
		
		long GetId (ThreadMirror thread)
		{
			long id;
			if (!localThreadIds.TryGetValue (thread.ThreadId, out id)) {
				id = localThreadIds.Count + 1;
				localThreadIds [thread.ThreadId] = id;
			}
			return id;
		}
		
		static bool ThreadIsAlive (ThreadMirror thread)
		{
			if (thread == null)
				return false;
			var state = thread.ThreadState;
			return state != ThreadState.Stopped && state != ThreadState.Aborted;
		}
		
		//we use the Mono.Debugging classes because they are cached
		static bool HasUserFrame (long tid, ThreadInfo[] threads)
		{
			foreach (var thread in threads) {
				if (thread.Id != tid)
					continue;

				var bt = thread.Backtrace;
				for (int i = 0; i < bt.FrameCount; i++) {
					var frame = bt.GetFrame (i);
					if (frame != null && !frame.IsExternalCode)
						return true;
				}

				return false;
			}

			return false;
		}
		
		public bool IsExternalCode (Mono.Debugger.Soft.StackFrame frame)
		{
			return frame.Method == null || string.IsNullOrEmpty (frame.FileName)
				|| (assemblyFilters != null && !assemblyFilters.Contains (frame.Method.DeclaringType.Assembly));
		}
		
		public bool IsExternalCode (TypeMirror type)
		{
			return assemblyFilters != null && !assemblyFilters.Contains (type.Assembly);
		}
		
		protected override AssemblyLine[] OnDisassembleFile (string file)
		{
			List<TypeMirror> mirrors;

			if (!source_to_type.TryGetValue (file, out mirrors))
				return new AssemblyLine [0];
			
			var lines = new List<AssemblyLine> ();
			foreach (var type in mirrors) {
				foreach (var method in type.GetMethods ()) {
					string srcFile = method.SourceFile != null ? NormalizePath (method.SourceFile) : null;
					
					if (srcFile == null || !PathsAreEqual (srcFile, file))
						continue;
					
					var body = method.GetMethodBody ();
					int lastLine = -1;
					int firstPos = lines.Count;
					string addrSpace = method.FullName;
					
					foreach (var ins in body.Instructions) {
						var loc = method.LocationAtILOffset (ins.Offset);

						if (loc != null && lastLine == -1) {
							lastLine = loc.LineNumber;
							for (int n = firstPos; n < lines.Count; n++) {
								AssemblyLine old = lines [n];
								lines [n] = new AssemblyLine (old.Address, old.AddressSpace, old.Code, loc.LineNumber);
							}
						}

						lines.Add (new AssemblyLine (ins.Offset, addrSpace, Disassemble (ins), loc != null ? loc.LineNumber : lastLine));
					}
				}
			}

			lines.Sort (delegate (AssemblyLine a1, AssemblyLine a2) {
				int res = a1.SourceLine.CompareTo (a2.SourceLine);

				return res != 0 ? res : a1.Address.CompareTo (a2.Address);
			});

			return lines.ToArray ();
		}

		public AssemblyLine[] Disassemble (Mono.Debugger.Soft.StackFrame frame)
		{
			var body = frame.Method.GetMethodBody ();
			var instructions = body.Instructions;
			var lines = new List<AssemblyLine> ();

			foreach (var instruction in instructions) {
				var location = frame.Method.LocationAtILOffset (instruction.Offset);
				int lineNumber = location != null ? location.LineNumber : -1;
				var code = Disassemble (instruction);

				lines.Add (new AssemblyLine (instruction.Offset, frame.Method.FullName, code, lineNumber));
			}

			return lines.ToArray ();
		}
		
		public AssemblyLine[] Disassemble (Mono.Debugger.Soft.StackFrame frame, int firstLine, int count)
		{
			var body = frame.Method.GetMethodBody ();
			var instructions = body.Instructions;
			ILInstruction current = null;

			foreach (var instruction in instructions) {
				if (instruction.Offset >= frame.ILOffset) {
					current = instruction;
					break;
				}
			}

			if (current == null)
				return new AssemblyLine [0];
			
			var lines = new List<AssemblyLine> ();
			
			while (firstLine < 0 && count > 0) {
				if (current.Previous == null) {
					lines.Add (AssemblyLine.OutOfRange);
					firstLine = 0;
					break;
				}

				current = current.Previous;
				firstLine++;
			}
			
			while (current != null && firstLine > 0) {
				current = current.Next;
				firstLine--;
			}
			
			while (count > 0) {
				if (current != null) {
					var location = frame.Method.LocationAtILOffset (current.Offset);
					int lineNumber = location != null ? location.LineNumber : -1;
					var code = Disassemble (current);

					lines.Add (new AssemblyLine (current.Offset, frame.Method.FullName, code, lineNumber));
					current = current.Next;
				} else {
					lines.Add (AssemblyLine.OutOfRange);
				}

				count--;
			}

			return lines.ToArray ();
		}

		static string EscapeString (string text)
		{
			var escaped = new StringBuilder ();
			
			escaped.Append ('"');
			for (int i = 0; i < text.Length; i++) {
				char c = text[i];
				string txt;
				switch (c) {
				case '"': txt = "\\\""; break;
				case '\0': txt = @"\0"; break;
				case '\\': txt = @"\\"; break;
				case '\a': txt = @"\a"; break;
				case '\b': txt = @"\b"; break;
				case '\f': txt = @"\f"; break;
				case '\v': txt = @"\v"; break;
				case '\n': txt = @"\n"; break;
				case '\r': txt = @"\r"; break;
				case '\t': txt = @"\t"; break;
				default:
					if (char.GetUnicodeCategory (c) == UnicodeCategory.OtherNotAssigned) {
						escaped.AppendFormat ("\\u{0:X4}", c);
					} else {
						escaped.Append (c);
					}
					continue;
				}
				escaped.Append (txt);
			}
			escaped.Append ('"');
			
			return escaped.ToString ();
		}
		
		static string Disassemble (ILInstruction ins)
		{
			string oper;
			if (ins.Operand is MethodMirror)
				oper = ((MethodMirror)ins.Operand).FullName;
			else if (ins.Operand is TypeMirror)
				oper = ((TypeMirror)ins.Operand).FullName;
			else if (ins.Operand is ILInstruction)
				oper = ((ILInstruction)ins.Operand).Offset.ToString ("x8");
			else if (ins.Operand is string)
				oper = EscapeString ((string) ins.Operand);
			else if (ins.Operand == null)
				oper = string.Empty;
			else
				oper = ins.Operand.ToString ();
			
			return ins.OpCode + " " + oper;
		}
		
		readonly static bool IsWindows;
		readonly static bool IsMac;
		readonly static StringComparer PathComparer;
		
		static bool IgnoreFilenameCase {
			get { return IsMac || IsWindows; }
		}
		
		static SoftDebuggerSession ()
		{
			IsWindows = Path.DirectorySeparatorChar == '\\';
			IsMac = !IsWindows && IsRunningOnMac();
			PathComparer = (IgnoreFilenameCase)? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
			ThreadMirror.NativeTransitions = true;
		}
		
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
				return false;
			} finally {
				if (buf != IntPtr.Zero)
					Marshal.FreeHGlobal (buf);
			}
			return false;
		}
		
		[System.Runtime.InteropServices.DllImport ("libc")]
		static extern int uname (IntPtr buf);
	}

	class LocationComparer : IComparer<Location>
	{
		public int Compare (Location loc0, Location loc1)
		{
			if (loc0.LineNumber < loc1.LineNumber)
				return -1;
			if (loc0.LineNumber > loc1.LineNumber)
				return 1;

			if (loc0.ColumnNumber < loc1.ColumnNumber)
				return -1;
			if (loc0.ColumnNumber > loc1.ColumnNumber)
				return 1;

			return loc0.ILOffset - loc1.ILOffset;
		}
	}
	
	class BreakInfo: BreakEventInfo
	{
		public Location Location;
		public List<EventRequest> Requests = new List<EventRequest> ();
		public string LastConditionValue;
		public string FileName;
		public string TypeName;
	}
	
	class DisconnectedException: DebuggerException
	{
		public DisconnectedException (Exception ex):
			base ("The connection with the debugger has been lost. The target application may have exited.", ex)
		{
		}
	}
	
	class DebugSocketException: DebuggerException
	{
		public DebugSocketException (Exception ex):
			base ("Could not open port for debugger. Another process may be using the port.", ex)
		{
		}
	}
	
	class ConnectionException : DebuggerException
	{
		public ConnectionException (Exception ex):
			base ("Could not connect to the debugger.", ex)
		{
		}
	}
}
