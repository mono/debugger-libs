using System;
using Mono.Debugger;

namespace Mono.Debugger.Soft
{
	public class ModuleMirror : Mirror
	{
		ModuleInfo info;
		Guid guid;
		AssemblyMirror assembly;

		internal ModuleMirror (VirtualMachine vm, long id) : base (vm, id) {
		}

		void ReadInfo () {
			if (info == null)
				info = vm.conn.Module_GetInfo (id);
		}

		public string Name {
			get {
				ReadInfo ();
				return info.Name;
			}
		}

		public string ScopeName {
			get {
				ReadInfo ();
				return info.ScopeName;
			}
		}

		public string FullyQualifiedName {
			get {
				ReadInfo ();
				return info.FQName;
			}
		}

		public Guid ModuleVersionId {
			get {
				if (guid == Guid.Empty) {
					ReadInfo ();
					if (string.IsNullOrEmpty(info.Guid))
						return Guid.Empty;
					guid = new Guid (info.Guid);
				}
				return guid;
			}
		}

		public AssemblyMirror Assembly {
			get {
				if (assembly == null) {
					ReadInfo ();
					if (info.Assembly == 0)
						return null;
					assembly = vm.GetAssembly (info.Assembly);
				}
				return assembly;
			}
		}

		// FIXME: Add function to query the guid, check in Metadata

		// Since protocol version 2.48
		public string SourceLink {
			get {
				vm.CheckProtocolVersion (2, 48);
				ReadInfo ();
				return info.SourceLink;
			}
		}

		// Apply a hot reload delta to the current module
		// Since protocol version 2.60
		public void ApplyChanges (ArrayMirror dmeta, ArrayMirror dIL, Value dPDB) {
		    /* dPDB is Value because it can be ArrayMirror or PrimitiveValue (vm, null) */
		    vm.CheckProtocolVersion (2, 60, checkOnlyRuntime: true);
		    vm.conn.Module_ApplyChanges (id, dmeta.Id, dIL.Id, dPDB.Id);
		}
	}
}
