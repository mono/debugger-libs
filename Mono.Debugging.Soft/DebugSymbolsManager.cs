using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.FileFormats.PE;
using Microsoft.SymbolStore;
using Microsoft.SymbolStore.SymbolStores;
using Mono.Debugger.Soft;
using Mono.Debugging.Client;

namespace Mono.Debugging.Soft
{
	internal enum SymbolStatus{
		NotTriedToLoad,
		NotFound,
		LoadedOnDebuggerSide,
		NotLoadedFromSymbolServer
	}
	internal class DebugSymbolsInfo
	{
		internal SymbolStatus Status {
			get; set;
		}
		
		internal PortablePdbData PdbData {
			get; set;
		}
		internal DebugSymbolsInfo(SymbolStatus status, PortablePdbData pdbData)
		{
			Status = status;
			PdbData = pdbData;
		}
	}
	public class DebugSymbolsManager
	{
		private readonly ITracer _tracer;
		Dictionary<AssemblyMirror, DebugSymbolsInfo> symbolsByAssembly = new Dictionary<AssemblyMirror, DebugSymbolsInfo> ();
		private string symbolCachePath;
		private string[] symbolServerUrls;
		private SymbolStore symbolStore;
		private SoftDebuggerSession session;
		private readonly object _lock = new object();

		internal DebugSymbolsManager (SoftDebuggerSession session)
		{
			this.session = session;
			_tracer = new TracerSymbolServer (session.LogWriter);
		}
		public string HasSymbolLoaded (AssemblyMirror assembly)
		{
			var symbolPath = GetSymbolPath (assembly.Location);
			if (!string.IsNullOrEmpty (symbolPath)) {
				return symbolPath;
			}

			var symbolsInfo = GetDebugSymbols (assembly);
			if (symbolsInfo != null) {
				return symbolsInfo.Status == SymbolStatus.NotLoadedFromSymbolServer ? "Dynamically loaded" : null;
			}

			return null;
		}
		public bool ForceLoadSymbolFromAssembly (AssemblyMirror assembly)
		{
			if (HasSymbolLoaded (assembly) != null)
				return true;
			return TryLoadSymbolFromSymbolServerIfNeeded (assembly).Result;
		}

		public void SetSymbolCache (string symbolCachePath)
		{
			this.symbolCachePath = symbolCachePath;
			symbolStore = null;
		}

		public async Task SetSymbolServerUrl (string[] symbolServerUrls)
		{
			this.symbolServerUrls = symbolServerUrls;
			symbolStore = null;
			await TryLoadSymbolFromSymbolServerIfNeeded ();
		}

		public async Task TryLoadSymbolFromSymbolServerIfNeeded ()
		{
			var assembliesToTryToLoadPPDB = GetDebugSymbols(asm => asm.Value.Status == SymbolStatus.NotFound || asm.Value.Status == SymbolStatus.NotTriedToLoad);
			foreach (var asm in assembliesToTryToLoadPPDB) {
				await TryLoadSymbolFromSymbolServerIfNeeded (asm.Key);
			}
		}

		internal PortablePdbData GetPdbData (AssemblyMirror asm, bool force = false)
		{
			PortablePdbData portablePdb = null;

			var symbolsInfo = GetDebugSymbols (asm);
			if (symbolsInfo != null && (symbolsInfo.PdbData != null || (!force && symbolsInfo.Status == SymbolStatus.NotFound))) {
				return symbolsInfo.PdbData;
			}

			var asmName = asm.GetName ().FullName;
			var pdbFileName = GetSymbolPath (asmName);
			if (string.IsNullOrEmpty(pdbFileName) || Path.GetExtension (pdbFileName) != ".pdb") {
				string assemblyFileName = GetAssemblyFileName(asmName);
				if (string.IsNullOrEmpty (assemblyFileName)) {
					assemblyFileName = asm.Location;
				}

				pdbFileName = Path.ChangeExtension (assemblyFileName, ".pdb");
			}
			if (PortablePdbData.IsPortablePdb (pdbFileName)) {
				portablePdb = new PortablePdbData (pdbFileName);
			} else {
				// Attempt to fetch pdb from the debuggee over the wire
				var pdbBlob = asm.GetPdbBlob ();
				portablePdb = pdbBlob != null ? new PortablePdbData (pdbBlob) : null;
			}

			if (portablePdb == null)
				AddDebugSymbols(asm, new DebugSymbolsInfo (SymbolStatus.NotFound, null));
			else
				AddDebugSymbols(asm, new DebugSymbolsInfo(SymbolStatus.NotLoadedFromSymbolServer, portablePdb));
			return portablePdb;
		}

		internal Location FindLocationsByFileInPdbLoadedOnDebuggerSide (string fileName, int line, int column)
		{
			List<KeyValuePair<AssemblyMirror, DebugSymbolsInfo>> symbolsByAssemblyList = GetDebugSymbols(item => item.Value.Status == SymbolStatus.LoadedOnDebuggerSide);

			foreach (var symbolServerPPDB in symbolsByAssemblyList) {
				var location = symbolServerPPDB.Value.PdbData.GetLocationByFileName (symbolServerPPDB.Key, fileName, line, column);
				if (location != null)
					return location;
			}

			return null;
		}

		internal void CreateSymbolStore ()
		{
			foreach (var urlServer in symbolServerUrls) {
				if (string.IsNullOrEmpty (urlServer))
					continue;
				try {
					symbolStore = new HttpSymbolStore (_tracer, symbolStore, new Uri ($"{urlServer}/"), null);
				} catch (Exception ex) {
					_tracer.Warning ($"Failed to create HttpSymbolStore for this URL - {urlServer} - {ex.Message}");
				}
			}
			if (!string.IsNullOrEmpty (symbolCachePath)) {
				try {
					symbolStore = new CacheSymbolStore (_tracer, symbolStore, symbolCachePath);
				} catch (Exception ex) {
					_tracer.Warning ( $"Failed to create CacheSymbolStore for this path - {symbolCachePath} - {ex.Message}");
				}
			}
		}

		public async Task<bool> TryLoadSymbolFromSymbolServerIfNeeded (AssemblyMirror asm)
		{
			var asmName = asm.GetName ().FullName;
			if (asm.HasDebugInfoLoaded ())
				return true;
			if (HasSymbolPath(asmName)) {
				var portablePdb = GetPdbData (asm, true);
				if (portablePdb != null) {
					AddDebugSymbols(asm, new DebugSymbolsInfo(SymbolStatus.LoadedOnDebuggerSide, portablePdb));
					session.TryResolvePendingBreakpoints ();
				}
				return true;
			}

			var symbolsInfo = GetDebugSymbols (asm);
			if (symbolsInfo != null && symbolsInfo.PdbData != null) {
				return true;
			}

			if (session.JustMyCode) {
				AddDebugSymbols(asm, new DebugSymbolsInfo(SymbolStatus.NotTriedToLoad, null));
				return false;
			}

			if (symbolStore == null)
				CreateSymbolStore ();
			if (symbolStore == null)
				return false;
			if (asm.GetPdbInfo (out int age, out Guid guid, out string pdbPath, out bool isPortableCodeView, out PdbChecksum[] pdbChecksums) == false)
				return false;
			var pdbName = Path.GetFileName (pdbPath);
			var pdbGuid = guid.ToString ("N").ToUpperInvariant () + (isPortableCodeView ? "FFFFFFFF" : age.ToString ());
			var key = $"{pdbName}/{pdbGuid}/{pdbName}";

			SymbolStoreFile file = await symbolStore.GetFile (new SymbolStoreKey (key, pdbPath, false, pdbChecksums), new CancellationTokenSource ().Token);
			if (file != null) {
				AddSymbolPath(asmName, Path.Combine (symbolCachePath, key));

				var portablePdb = GetPdbData (asm, true);
				if (portablePdb != null) {
					AddDebugSymbols (asm, new DebugSymbolsInfo (SymbolStatus.LoadedOnDebuggerSide, portablePdb));
					session.TryResolvePendingBreakpoints ();
				} else {
					AddDebugSymbols (asm, new DebugSymbolsInfo (SymbolStatus.NotFound, null));
					return false;
				}
			}
			else {
				AddDebugSymbols(asm, new DebugSymbolsInfo (SymbolStatus.NotFound, null));
				return false;
			}
			return true;
		}

		private void AddDebugSymbols (AssemblyMirror asm, DebugSymbolsInfo debugSymbolsInfo)
		{
			lock (_lock) {
				symbolsByAssembly[asm] = debugSymbolsInfo;
			}
		}

		private DebugSymbolsInfo GetDebugSymbols (AssemblyMirror asm)
		{
			lock (_lock) {
				if (symbolsByAssembly.TryGetValue (asm, out var symbolsInfo)) {
					return symbolsInfo;
				}
			}

			return null;
		}

		private List<KeyValuePair<AssemblyMirror, DebugSymbolsInfo>> GetDebugSymbols (Func<KeyValuePair<AssemblyMirror, DebugSymbolsInfo>, bool> filterBy)
		{
			lock (_lock) {
				return symbolsByAssembly.Where (item => filterBy (item)).ToList ();
			}
		}

		private string GetSymbolPath (string asmName)
		{
			lock (_lock) {
				if (session.SymbolPathMap.TryGetValue (asmName, out string symbolPath))
					return symbolPath;
			}
			return null;
		}

		private void AddSymbolPath (string asmName, string asmPath)
		{
			lock (_lock) {
				session.SymbolPathMap[asmName] = asmPath;
			}
		}

		private string GetAssemblyFileName (string asmName)
		{
			lock (_lock) {
				if (session.AssemblyPathMap.TryGetValue (asmName, out string assemblyFileName))
					return assemblyFileName;
			}
			return null;
		}

		private bool HasSymbolPath (string asmName)
		{
			lock (_lock) {
				return session.SymbolPathMap.ContainsKey (asmName);
			}
		}
	}
	public sealed class TracerSymbolServer : ITracer
	{
		private readonly OutputWriterDelegate writer;

		public TracerSymbolServer (OutputWriterDelegate writer)
		{
			this.writer = writer;
		}

		public void WriteLine (string message) => writer?.Invoke (false, message);

		public void WriteLine (string format, params object[] arguments) => writer?.Invoke (false, string.Format (format, arguments));

		public void Information (string message) => writer?.Invoke (false, message);

		public void Information (string format, params object[] arguments) => writer?.Invoke (false, string.Format (format, arguments));

		public void Warning (string message) => writer?.Invoke (false, message);
		public void Warning (string format, params object[] arguments) => writer?.Invoke (false, string.Format (format, arguments));

		public void Error (string message) => writer?.Invoke (false, message);

		public void Error (string format, params object[] arguments) => writer?.Invoke (false, string.Format (format, arguments));

		public void Verbose (string message) => writer?.Invoke (false, message);

		public void Verbose (string format, params object[] arguments) => writer?.Invoke (false, string.Format (format, arguments));
	}
}
