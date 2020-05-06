// 
// EvaluationTests.cs
//  
// Author:
//       Lluis Sanchez Gual <lluis@novell.com>
// 
// Copyright (c) 2009 Novell, Inc (http://www.novell.com)
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

using Mono.Debugging.Client;
using NUnit.Framework;
using Mono.Debugging.Soft;
using System.Linq;

namespace Mono.Debugging.Tests
{
	[TestFixture]
	public abstract class EvaluationTests : DebugTests
	{
		protected EvaluationTests (string de, bool allowTargetInvokes) : base (de)
		{
			AllowTargetInvokes = allowTargetInvokes;
		}

		[TestFixtureSetUp]
		public override void SetUp ()
		{
			base.SetUp ();

			Start ("TestEvaluation");
			Session.TypeResolverHandler = ResolveType;
		}

		static string ResolveType (string identifier, SourceLocation location)
		{
			switch (identifier) {
			case "SomeClassInNamespace":
				return "MonoDevelop.Debugger.Tests.TestApp.SomeClassInNamespace";
			case "ParentNestedClass":
				return "MonoDevelop.Debugger.Tests.TestApp.TestEvaluationParent.ParentNestedClass";
			case "NestedClass":
				return "MonoDevelop.Debugger.Tests.TestApp.TestEvaluation.NestedClass";
			case "TestEvaluation":
				return "MonoDevelop.Debugger.Tests.TestApp.TestEvaluation";
			case "NestedGenericClass`2":
				return "MonoDevelop.Debugger.Tests.TestApp.TestEvaluation.NestedGenericClass";
			case "Dictionary`2":
				return "System.Collections.Generic.Dictionary";
			case "Thing`1":
				return "Thing";
			case "A":
				return "A";
			case "B":
				return "B";
			case "C":
				return "C";
			case "System":
				return "System";
			case "MonoDevelop":
				return "MonoDevelop";
			case "SomeEnum":
				return "SomeEnum";
			case "IFoo":
				return "MonoDevelop.Debugger.Tests.TestApp.IFoo";
			case "IBar":
				return "MonoDevelop.Debugger.Tests.TestApp.IBar";
			case "Bo":
				return "MonoDevelop.Debugger.Tests.TestApp.Bo";
			}
			return null;
		}

		[Test]
		public void This ()
		{
			var soft = Session as SoftDebuggerSession;
			if (soft != null && soft.ProtocolVersion < new Version (2, 31))
				Assert.Ignore ("A newer version of the Mono runtime is required.");

			ObjectValue val = Eval ("this");
			Assert.AreEqual ("{MonoDevelop.Debugger.Tests.TestApp.TestEvaluationChild}", val.Value);
			Assert.AreEqual ("MonoDevelop.Debugger.Tests.TestApp.TestEvaluationChild", val.TypeName);
		}

		[Test]
		public void UnaryOperators ()
		{
			ObjectValue val = Eval ("~1234");
			Assert.AreEqual ((~1234).ToString (), val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("!true");
			Assert.AreEqual ("false", val.Value);
			Assert.AreEqual ("bool", val.TypeName);

			val = Eval ("!false");
			Assert.AreEqual ("true", val.Value);
			Assert.AreEqual ("bool", val.TypeName);

			val = Eval ("-1234");
			Assert.AreEqual ("-1234", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("+1234");
			Assert.AreEqual ("1234", val.Value);
			Assert.AreEqual ("int", val.TypeName);
		}

		[Test]
		public void TypeReference ()
		{
			ObjectValue val;
			val = Eval ("System.String");
			Assert.AreEqual ("string", val.Value);
			Assert.AreEqual ("<type>", val.TypeName);
			Assert.AreEqual (ObjectValueFlags.Type, val.Flags & ObjectValueFlags.OriginMask);

			val = Eval ("TestEvaluation");
			Assert.AreEqual ("MonoDevelop.Debugger.Tests.TestApp.TestEvaluation", val.Value);
			Assert.AreEqual ("<type>", val.TypeName);
			Assert.AreEqual (ObjectValueFlags.Type, val.Flags & ObjectValueFlags.OriginMask);

			val = Eval ("A");
			Assert.AreEqual ("A", val.Value);
			Assert.AreEqual ("<type>", val.TypeName);
			Assert.AreEqual (ObjectValueFlags.Type, val.Flags & ObjectValueFlags.OriginMask);

			val = Eval ("NestedClass");
			Assert.AreEqual ("MonoDevelop.Debugger.Tests.TestApp.TestEvaluation.NestedClass", val.Value);
			Assert.AreEqual ("<type>", val.TypeName);
			Assert.AreEqual (ObjectValueFlags.Type, val.Flags & ObjectValueFlags.OriginMask);

			val = Eval ("MonoDevelop.Debugger.Tests.TestApp.TestEvaluation");
			Assert.AreEqual ("MonoDevelop.Debugger.Tests.TestApp.TestEvaluation", val.Value);
			Assert.AreEqual ("<type>", val.TypeName);
			Assert.AreEqual (ObjectValueFlags.Type, val.Flags & ObjectValueFlags.OriginMask);

			val = Eval ("NestedClass.DoubleNestedClass");
			Assert.AreEqual ("MonoDevelop.Debugger.Tests.TestApp.TestEvaluation.NestedClass.DoubleNestedClass", val.Value);
			Assert.AreEqual ("<type>", val.TypeName);
			Assert.AreEqual (ObjectValueFlags.Type, val.Flags & ObjectValueFlags.OriginMask);

			val = Eval ("ParentNestedClass");
			Assert.AreEqual ("MonoDevelop.Debugger.Tests.TestApp.TestEvaluationParent.ParentNestedClass", val.Value);
			Assert.AreEqual ("<type>", val.TypeName);
			Assert.AreEqual (ObjectValueFlags.Type, val.Flags & ObjectValueFlags.OriginMask);

			val = Eval ("SomeClassInNamespace");
			Assert.AreEqual ("MonoDevelop.Debugger.Tests.TestApp.SomeClassInNamespace", val.Value);
			Assert.AreEqual ("<type>", val.TypeName);
			Assert.AreEqual (ObjectValueFlags.Type, val.Flags & ObjectValueFlags.OriginMask);

			val = Eval ("ClassWithCompilerGeneratedNestedClass");
			Assert.AreEqual ("ClassWithCompilerGeneratedNestedClass", val.Value);
			Assert.AreEqual ("<type>", val.TypeName);
			Assert.AreEqual (ObjectValueFlags.Type, val.Flags & ObjectValueFlags.OriginMask);

			if (!IsVsDebugger) {
				// Note: not currently supported by the NetCoreDebugger
				var children = val.GetAllChildrenSync ();
				Assert.AreEqual (2, children.Length);
				Assert.AreEqual ("ClassWithCompilerGeneratedNestedClass.NestedClass", children [0].Value);
				Assert.AreEqual ("<type>", children [0].TypeName);
				Assert.AreEqual (ObjectValueFlags.Type, children [0].Flags & ObjectValueFlags.OriginMask);
			}
		}

		[Test]
		public virtual void HiddenMembers ()
		{
			IgnoreCorDebugger ("TODO");
			if (Session is SoftDebuggerSession) {
				if (!((SoftDebuggerSession)Session).ProtocolVersion.AtLeast (2, 40)) {
					Assert.Ignore ("Need newer Mono with SDB protocol 2.40+");
				}
			}
			ObjectValue val;
			val = Eval ("HiddenField");
			Assert.AreEqual ("5", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("HiddenProperty");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("5", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("HiddenMethod()");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("5", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("OverridenPropertyInt");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("6", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("OverridenMethodInt()");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("6", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("OverridenPropertyString");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("\"6\"", val.Value);
			Assert.AreEqual ("string", val.TypeName);

			val = Eval ("OverridenMethodString()");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("\"6\"", val.Value);
			Assert.AreEqual ("string", val.TypeName);



			val = Eval ("testEvaluationChild.HiddenField");
			Assert.AreEqual ("6", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("testEvaluationChild.HiddenProperty");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("6", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("testEvaluationChild.HiddenMethod()");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("6", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("testEvaluationChild.OverridenPropertyInt");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("6", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("testEvaluationChild.OverridenMethodInt()");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("6", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("testEvaluationChild.OverridenPropertyString");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("\"6\"", val.Value);
			Assert.AreEqual ("string", val.TypeName);

			val = Eval ("testEvaluationChild.OverridenMethodString()");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("\"6\"", val.Value);
			Assert.AreEqual ("string", val.TypeName);
		}

		[Test]
		public virtual void TypeReferenceGeneric ()
		{
			ObjectValue val;
			val = Eval ("System.Collections.Generic.Dictionary<string,int>");
			Assert.AreEqual ("System.Collections.Generic.Dictionary<string,int>", val.Value);
			Assert.AreEqual ("<type>", val.TypeName);
			Assert.AreEqual (ObjectValueFlags.Type, val.Flags & ObjectValueFlags.OriginMask);

			val = Eval ("Thing<string>.Done<int>");
			Assert.AreEqual ("Thing<string>.Done<int>", val.Value);
			Assert.AreEqual ("<type>", val.TypeName);
			Assert.AreEqual (ObjectValueFlags.Type, val.Flags & ObjectValueFlags.OriginMask);
		}

		[Test]
		public virtual void Typeof ()
		{
			var soft = Session as SoftDebuggerSession;
			if (soft != null && soft.ProtocolVersion < new Version (2, 31))
				Assert.Ignore ("A newer version of the Mono runtime is required.");

			var val = Eval ("typeof(System.Console)");
			Assert.IsTrue (val.TypeName == "System.MonoType" || val.TypeName == "System.RuntimeType", "Incorrect type name: " + val.TypeName);
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.Value == "{System.MonoType}" || val.Value == "{System.RuntimeType}");

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("{System.Console}", val.Value);

			if (!AllowTargetInvokes)
				return;

			val = Eval ("typeof(System.Console).GetMembers()");
			Assert.AreEqual ("System.Reflection.MemberInfo[]", val.TypeName);
		}

		[Test]
		public void MethodInvoke ()
		{
			ObjectValue val;

			var soft = Session as SoftDebuggerSession;
			if (soft != null && soft.ProtocolVersion < new Version (2, 31))
				Assert.Ignore ("A newer version of the Mono runtime is required.");

			val = Eval ("TestMethod ()");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("1", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("TestMethod (\"23\")");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("24", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("TestMethod (42)");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("43", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("TestMethod (false)");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("2", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("this.TestMethod ()");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("1", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("this.TestMethod (\"23\")");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("24", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("this.TestMethod (42)");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("43", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("System.Int32.Parse (\"67\")");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("67", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("this.BoxingTestMethod (43)");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("\"43\"", val.Value);
			Assert.AreEqual ("string", val.TypeName);

			val = Eval ("objWithMethodA.MethodA()");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("\"AbstractImplementation\"", val.Value);
			Assert.AreEqual ("string", val.TypeName);

			IgnoreCorDebugger ("TODO: CorDebugger support explicit interfaces");

			val = Eval ("((IInterfaceWithMethodA)objWithMethodA).MethodA()");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("\"InterfaceImplementation\"", val.Value);
			Assert.AreEqual ("string", val.TypeName);

			val = Eval ("true.ToString()");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}

			Assert.AreEqual ("\"True\"", val.Value);
			Assert.AreEqual ("string", val.TypeName);

			val = Eval ("IsNull(null)");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("true", val.Value);
			Assert.AreEqual ("bool", val.TypeName);

			val = Eval ("IsNull(new RichClass(5))");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("false", val.Value);
			Assert.AreEqual ("bool", val.TypeName);
		}

		[Test]
		public void FuncInvoke ()
		{
			ObjectValue val;

			var soft = Session as SoftDebuggerSession;
			if (soft != null && soft.ProtocolVersion < new Version (2, 31))
				Assert.Ignore ("A newer version of the Mono runtime is required.");

			val = Eval ("((System.Action<int>)(x => System.Console.WriteLine(x))).Invoke(5)");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("No return value.", val.Value);

			val = Eval ("((System.Func<string,int>)(x => x.Length + 10)).Invoke(\"abcd\")");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("14", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("((System.Predicate<string>)(x => x == \"abcd\")).Invoke(\"abcd\")");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("true", val.Value);
			Assert.AreEqual ("bool", val.TypeName);

			val = Eval ("((MonoDevelop.Debugger.Tests.TestApp.del)((x, y) => x + y * 100 - 1)).Invoke(-9, 5)");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("490", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			// Note: NetCoreDebugger does not support LINQ extensions
			if (!IsVsDebugger) {
				val = Eval ("stringList.Sum(x => x.Length)");
				if (!AllowTargetInvokes) {
					var options = Session.Options.EvaluationOptions.Clone ();
					options.AllowTargetInvoke = true;

					if (!IsVsDebugger)
						Assert.IsTrue (val.IsImplicitNotSupported);

					val.Refresh (options);
					val = val.Sync ();
				}
				Assert.AreEqual ("9", val.Value);
				Assert.AreEqual ("int", val.TypeName);

				val = Eval ("stringList.Any (x => x.Length == 3)");
				if (!AllowTargetInvokes) {
					var options = Session.Options.EvaluationOptions.Clone ();
					options.AllowTargetInvoke = true;

					if (!IsVsDebugger)
						Assert.IsTrue (val.IsImplicitNotSupported);

					val.Refresh (options);
					val = val.Sync ();
				}
				Assert.AreEqual ("true", val.Value);
				Assert.AreEqual ("bool", val.TypeName);

				val = Eval ("stringList.Last(x => x.CompareTo(\"c\") < 0)");
				if (!AllowTargetInvokes) {
					var options = Session.Options.EvaluationOptions.Clone ();
					options.AllowTargetInvoke = true;

					if (!IsVsDebugger)
						Assert.IsTrue (val.IsImplicitNotSupported);

					val.Refresh (options);
					val = val.Sync ();
				}
				Assert.AreEqual ("\"bbb\"", val.Value);
				Assert.AreEqual ("string", val.TypeName);
			}

			val = Eval ("this.InvokeFuncInt (() => 100 + 500 * 2)");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("1100", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("this.InvokeFuncInt (() => this.HiddenField)");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			// Error occurs because `this' type is non public
			if (!IsVsDebugger)
				Assert.IsTrue (val.IsError);

			val = Eval ("this.InvokeFuncString (() => \"test\")");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("\"test\"", val.Value);
			Assert.AreEqual ("string", val.TypeName);

			val = Eval ("testEvaluationChild.OverridenInvokeFuncInt(() => 100)");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("101", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("testEvaluationChild.OverridenInvokeFuncString(() => (stringList.Count < n ? stringList[1] : stringList[0]) + intOne)");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("\"bbb1-in-overriden\"", val.Value);
			Assert.AreEqual ("string", val.TypeName);

			val = Eval ("this.OverloadedInvokeFunc (() => dict.Count + 500)");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("501", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("this.OverloadedInvokeFunc (() => \"\" + n + 5)");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("\"325\"", val.Value);
			Assert.AreEqual ("string", val.TypeName);

			val = Eval ("this.InvokePredicateString (x => x == \"abc\")");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("true", val.Value);
			Assert.AreEqual ("bool", val.TypeName);

			val = Eval ("this.InvokeUserDelegate ((x, y) => x + y)");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("6", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("InvokeGenericFunc (500, x => x * 10)");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("5000", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			if (!IsVsDebugger) {
				val = Eval ("string.Join(\",\", numbers.Where(n=>n.StartsWith(\"t\")).ToArray())");
				if (!AllowTargetInvokes) {
					var options = Session.Options.EvaluationOptions.Clone ();
					options.AllowTargetInvoke = true;

					if (!IsVsDebugger)
						Assert.IsTrue (val.IsImplicitNotSupported);

					val.Refresh (options);
					val = val.Sync ();
				}
				Assert.AreEqual ("\"two,three\"", val.Value);
				Assert.AreEqual ("string", val.TypeName);
			}

			val = Eval ("instList.Find(x => x.n == 5)");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("{MonoDevelop.Debugger.Tests.TestApp.SomeOuterClass.SomeInnerClass}", val.Value);
			Assert.AreEqual ("MonoDevelop.Debugger.Tests.TestApp.SomeOuterClass.SomeInnerClass", val.TypeName);

			val = Eval ("System.Array.Find(instArray, x => x.n == 10)");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("{MonoDevelop.Debugger.Tests.TestApp.SomeOuterClass.SomeInnerClass}", val.Value);
			Assert.AreEqual ("MonoDevelop.Debugger.Tests.TestApp.SomeOuterClass.SomeInnerClass", val.TypeName);

			val = Eval ("System.Array.FindIndex(numbers, x => x == \"one\") == System.Array.IndexOf(numbers, \"one\")");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("true", val.Value);
			Assert.AreEqual ("bool", val.TypeName);
		}

		[Test]
		public void GenericMethodInvoke ()
		{
			var soft = Session as SoftDebuggerSession;
			if (soft != null && soft.ProtocolVersion < new Version (2, 31))
				Assert.Ignore ("A newer version of the Mono runtime is required.");

			IgnoreCorDebugger ("TODO: Win32 support generic invokes");

			if (!AllowTargetInvokes)
				return;

			ObjectValue val;
			val = Eval ("done.ReturnInt5()");
			Assert.AreEqual ("5", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("done.ReturnSame(3)");
			Assert.AreEqual ("3", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("ReturnSame(4)");
			Assert.AreEqual ("4", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("ReturnSame(\"someString\")");
			Assert.AreEqual ("\"someString\"", val.Value);
			Assert.AreEqual ("string", val.TypeName);

			val = Eval ("ReturnNew<WithToString>()");
			Assert.AreEqual ("{SomeString}", val.Value);
			Assert.AreEqual ("WithToString", val.TypeName);

			val = Eval ("intZero");
			Assert.AreEqual ("0", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("intOne");
			Assert.AreEqual ("1", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			// TODO: in, out, and ref params are not currently supported
			//val = Eval ("Swap (ref intZero, ref intOne)");
			//Assert.AreEqual ("", val.Value);

			//val = Eval ("intZero");
			//Assert.AreEqual ("1", val.Value);
			//Assert.AreEqual ("int", val.TypeName);

			//val = Eval ("intOne");
			//Assert.AreEqual ("0", val.Value);
			//Assert.AreEqual ("int", val.TypeName);

			//Lets return in same state as before in case some other test will use
			//val = Eval ("Swap (ref intZero, ref intOne)");
			//Assert.AreEqual ("", val.Value);

			val = Eval ("GenerateList(\"someString\", 5)");
			Assert.AreEqual ("Count=5", val.Value.Replace (" ", ""));//Remove spaces is for references source and old mono compatiblity
			Assert.AreEqual ("System.Collections.Generic.List<string>", val.TypeName);

			val = Eval ("GenerateList(2.0, 6)");
			Assert.AreEqual ("Count=6", val.Value.Replace (" ", ""));//Remove spaces is for references source and old mono compatiblity
			Assert.AreEqual ("System.Collections.Generic.List<double>", val.TypeName);

			val = Eval ("done.GetDefault()");
			Assert.AreEqual ("0", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("done.GetParentDefault()");
			Assert.AreEqual ("(null)", val.Value);
			if (IsVsDebugger)
				Assert.AreEqual ("string", val.TypeName);
			else
				Assert.AreEqual ("object", val.TypeName);

			val = Eval ("new Dictionary<int,string>()");
			Assert.AreEqual ("Count=0", val.Value.Replace (" ", ""));//Remove spaces is for references source and old mono compatiblity
			Assert.AreEqual ("System.Collections.Generic.Dictionary<int,string>", val.TypeName);

			val = Eval ("done.Property");
			Assert.AreEqual ("54", val.Value);
			Assert.AreEqual ("int", val.TypeName);
		}

		[Test]
		public void Indexers ()
		{
			ObjectValue val = Eval ("numbers[0]");
			Assert.AreEqual ("\"one\"", val.Value);
			Assert.AreEqual ("string", val.TypeName);

			val = Eval ("numbers[1]");
			Assert.AreEqual ("\"two\"", val.Value);
			Assert.AreEqual ("string", val.TypeName);

			val = Eval ("numbers[2]");
			Assert.AreEqual ("\"three\"", val.Value);
			Assert.AreEqual ("string", val.TypeName);

			val = Eval ("staticString[2]");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("'m'", val.Value);
			Assert.AreEqual ("char", val.TypeName);

			val = Eval ("alist[0]");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("1", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("alist[1]");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("\"two\"", val.Value);
			Assert.AreEqual ("string", val.TypeName);

			val = Eval ("alist[2]");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("3", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			IgnoreCorDebugger ("TODO: CorDebugger support explicit interfaces");

			if (!IsVsDebugger) {
				// FIXME: figure out when to set the Unknown flag
				val = Eval ("FooBar[2]");
				Assert.IsTrue (val.IsUnknown);
			}

			//We have IFoo and IBar implementation but without typecasting and determening which implementation user wants
			//we have to return error/Unknown instead of random implementation(FooBar[2] is not compilable anyway)
			val = Eval ("((IFoo)FooBar)[2]");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("int", val.TypeName);
			Assert.AreEqual ("2", val.Value);
			val = Eval ("((IBar)FooBar)[2]");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("int", val.TypeName);
			Assert.AreEqual ("-2", val.Value);
			val = Eval ("Bar[3]");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("int", val.TypeName);
			Assert.AreEqual ("-3", val.Value);
			val = Eval ("Foo[3]");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("int", val.TypeName);
			Assert.AreEqual ("3", val.Value);
		}

		void CheckValue (string expected, string actual, bool isSimpleProperty)
		{
			if (AllowTargetInvokes || isSimpleProperty)
				Assert.AreEqual (expected, actual);
			else
				Assert.AreEqual ("Implicit evaluation is disabled", actual);
		}

		[Test]
		public void MemberReference ()
		{
			ObjectValue val;

			var soft = Session as SoftDebuggerSession;
			if (soft != null && soft.ProtocolVersion < new Version (2, 31))
				Assert.Ignore ("A newer version of the Mono runtime is required.");

			val = Eval ("namedTypeSymbol.BaseType");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("\"Type1\"", val.Value);
			Assert.AreEqual ("string", val.TypeName);

			val = Eval ("\"someString\".Length");
			if (!AllowTargetInvokes && soft == null) {
				// Note: this is a simple property which gets evaluated client-side by the SDB backend
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("10", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("numbers.Length");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("3", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("\"someString\".GetHashCode()");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("numbers.GetHashCode()");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("\"someString\".EndsWith (\"ing\")");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("true", val.Value);
			Assert.AreEqual ("bool", val.TypeName);

			val = Eval ("alist.Count");
			if (!AllowTargetInvokes) {
				// Note: this is a simple property which gets evaluated client-side by the SDB backend
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("3", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			Eval ("var tt = this");

			// FIXME: this errors out when target invokes are disabled
			// FIXME: NetCoreDebugger errors out because 'tt' does not exist in the current context
			if (AllowTargetInvokes && !IsVsDebugger) {
				val = Eval ("tt.someString");
				Assert.AreEqual ("\"hi\"", val.Value);
				Assert.AreEqual ("string", val.TypeName);
			}

			val = Eval ("MonoDevelop.Debugger.Tests");
			Assert.AreEqual ("MonoDevelop.Debugger.Tests", val.Value);
			Assert.AreEqual ("<namespace>", val.TypeName);

			val = Eval ("MonoDevelop.Debugger.Tests.TestApp.TestEvaluation");
			Assert.AreEqual ("MonoDevelop.Debugger.Tests.TestApp.TestEvaluation", val.Value);
			Assert.AreEqual ("<type>", val.TypeName);

			val = Eval ("MonoDevelop.Debugger.Tests.TestApp.TestEvaluation.staticString");
			Assert.AreEqual ("\"some static\"", val.Value);
			Assert.AreEqual ("string", val.TypeName);

			val = Eval ("richObject");
			Assert.AreEqual ("{RichClass}", val.Value);
			Assert.AreEqual ("RichClass", val.TypeName);

			ObjectValue [] richChildren;

			if (!IsVsDebugger) {
				// FIXME: filter private members into "Non-public members" child for NetCoreDebugger
				richChildren = val.GetAllChildrenSync ();
				Assert.AreEqual (13, richChildren.Length);
				Assert.AreEqual ("publicInt1", richChildren [0].Name);
				Assert.AreEqual ("int", richChildren [0].TypeName);
				Assert.AreEqual ("1", richChildren [0].Value);
				Assert.AreEqual ("publicInt2", richChildren [1].Name);
				Assert.AreEqual ("int", richChildren [1].TypeName);
				Assert.AreEqual ("2", richChildren [1].Value);
				Assert.AreEqual ("publicInt3", richChildren [2].Name);
				Assert.AreEqual ("int", richChildren [2].TypeName);
				Assert.AreEqual ("3", richChildren [2].Value);
				Assert.AreEqual ("publicPropInt1", richChildren [3].Name);
				Assert.AreEqual ("int", richChildren [3].TypeName);
				CheckValue ("1", richChildren [3].Value, soft != null);
				Assert.AreEqual ("publicPropInt2", richChildren [4].Name);
				Assert.AreEqual ("int", richChildren [4].TypeName);
				CheckValue ("2", richChildren [4].Value, soft != null);
				Assert.AreEqual ("publicPropInt3", richChildren [5].Name);
				Assert.AreEqual ("int", richChildren [5].TypeName);
				CheckValue ("3", richChildren [5].Value, soft != null);
				Assert.AreEqual ("publicPropStringA", richChildren [6].Name);
				Assert.AreEqual ("string", richChildren [6].TypeName);
				CheckValue ("\"stringA\"", richChildren [6].Value, soft != null);
				Assert.AreEqual ("publicPropStringB", richChildren [7].Name);
				Assert.AreEqual ("string", richChildren [7].TypeName);
				CheckValue ("\"stringB\"", richChildren [7].Value, soft != null);
				Assert.AreEqual ("publicPropStringC", richChildren [8].Name);
				Assert.AreEqual ("string", richChildren [8].TypeName);
				CheckValue ("\"stringC\"", richChildren [8].Value, soft != null);
				Assert.AreEqual ("publicStringA", richChildren [9].Name);
				Assert.AreEqual ("string", richChildren [9].TypeName);
				Assert.AreEqual ("\"stringA\"", richChildren [9].Value);
				Assert.AreEqual ("publicStringB", richChildren [10].Name);
				Assert.AreEqual ("string", richChildren [10].TypeName);
				Assert.AreEqual ("\"stringB\"", richChildren [10].Value);
				Assert.AreEqual ("publicStringC", richChildren [11].Name);
				Assert.AreEqual ("string", richChildren [11].TypeName);
				Assert.AreEqual ("\"stringC\"", richChildren [11].Value);
				Assert.AreEqual ("Non-public members", richChildren [12].Name);

				richChildren = richChildren [12].GetAllChildrenSync ();
				Assert.AreEqual (12, richChildren.Length);
				Assert.AreEqual ("privateInt1", richChildren [0].Name);
				Assert.AreEqual ("int", richChildren [0].TypeName);
				Assert.AreEqual ("1", richChildren [0].Value);
				Assert.AreEqual ("privateInt2", richChildren [1].Name);
				Assert.AreEqual ("int", richChildren [1].TypeName);
				Assert.AreEqual ("2", richChildren [1].Value);
				Assert.AreEqual ("privateInt3", richChildren [2].Name);
				Assert.AreEqual ("int", richChildren [2].TypeName);
				Assert.AreEqual ("3", richChildren [2].Value);
				Assert.AreEqual ("privatePropInt1", richChildren [3].Name);
				Assert.AreEqual ("int", richChildren [3].TypeName);
				CheckValue ("1", richChildren [3].Value, soft != null);
				Assert.AreEqual ("privatePropInt2", richChildren [4].Name);
				Assert.AreEqual ("int", richChildren [4].TypeName);
				CheckValue ("2", richChildren [4].Value, soft != null);
				Assert.AreEqual ("privatePropInt3", richChildren [5].Name);
				Assert.AreEqual ("int", richChildren [5].TypeName);
				CheckValue ("3", richChildren [5].Value, soft != null);
				Assert.AreEqual ("privatePropStringA", richChildren [6].Name);
				Assert.AreEqual ("string", richChildren [6].TypeName);
				CheckValue ("\"stringA\"", richChildren [6].Value, soft != null);
				Assert.AreEqual ("privatePropStringB", richChildren [7].Name);
				Assert.AreEqual ("string", richChildren [7].TypeName);
				CheckValue ("\"stringB\"", richChildren [7].Value, soft != null);
				Assert.AreEqual ("privatePropStringC", richChildren [8].Name);
				Assert.AreEqual ("string", richChildren [8].TypeName);
				CheckValue ("\"stringC\"", richChildren [8].Value, soft != null);
				Assert.AreEqual ("privateStringA", richChildren [9].Name);
				Assert.AreEqual ("string", richChildren [9].TypeName);
				Assert.AreEqual ("\"stringA\"", richChildren [9].Value);
				Assert.AreEqual ("privateStringB", richChildren [10].Name);
				Assert.AreEqual ("string", richChildren [10].TypeName);
				Assert.AreEqual ("\"stringB\"", richChildren [10].Value);
				Assert.AreEqual ("privateStringC", richChildren [11].Name);
				Assert.AreEqual ("string", richChildren [11].TypeName);
				Assert.AreEqual ("\"stringC\"", richChildren [11].Value);
			}

			if (AllowTargetInvokes) {
				val = Eval ("richObject.publicStringB=\"changedTextB\"");
				Assert.AreEqual ("string", val.TypeName);
				Assert.AreEqual ("\"changedTextB\"", val.Value);
				val = Eval ("richObject.publicStringB");
				Assert.AreEqual ("string", val.TypeName);
				Assert.AreEqual ("\"changedTextB\"", val.Value);

				val = Eval ("richObject");
				Assert.AreEqual ("{RichClass}", val.Value);
				Assert.AreEqual ("RichClass", val.TypeName);

				if (!IsVsDebugger) {
					// FIXME: filter private members into "Non-public members" child for NetCoreDebugger
					richChildren = val.GetAllChildrenSync ();
					Assert.AreEqual ("publicPropStringB", richChildren [7].Name);
					Assert.AreEqual ("string", richChildren [7].TypeName);
					Assert.AreEqual ("\"stringB\"", richChildren [7].Value);
				}
			}

			if (AllowTargetInvokes) {
				val = Eval ("PropertyA");
				if (IsVsDebugger)
					Assert.AreEqual ("(1, \"2\", 3.3)", val.Value);
				else
					Assert.AreEqual ("{(1, 2, 3.3)}", val.Value);
				if (IsVsDebugger)
					Assert.AreEqual ("(int a,string,double B)", val.TypeName);
				else
					Assert.AreEqual ("System.ValueTuple<int,string,double>", val.TypeName);

				richChildren = val.GetAllChildrenSync ();
				Assert.AreEqual ("1", richChildren.Single (c => c.Name == "a").Value);
				Assert.AreEqual ("\"2\"", richChildren.Single (c => c.Name == "Item2").Value);
				Assert.AreEqual ("3.3", richChildren.Single (c => c.Name == "B").Value);

				val = Eval ("PropertyA.a");
				Assert.AreEqual ("int", val.TypeName);
				Assert.AreEqual ("1", val.Value);
			}

			val = Eval ("FieldB");
			if (IsVsDebugger) {
				Assert.AreEqual ("(true, 2.2, \"3\")", val.Value);
				Assert.AreEqual ("(bool C,float,string d)", val.TypeName);
			} else {
				Assert.AreEqual (AllowTargetInvokes ? "{(True, 2.2, 3)}" : "{System.ValueTuple<bool,float,string>}", val.Value);
				Assert.AreEqual ("System.ValueTuple<bool,float,string>", val.TypeName);
			}

			richChildren = val.GetAllChildrenSync ();
			Assert.AreEqual ("true", richChildren.Single (c => c.Name == "C").Value);
			Assert.AreEqual ("2.2", richChildren.Single (c => c.Name == "Item2").Value);
			Assert.AreEqual ("\"3\"", richChildren.Single (c => c.Name == "d").Value);

			val = Eval ("namedTuple");
			if (IsVsDebugger) {
				Assert.AreEqual ("(null, 3)", val.Value);
				Assert.AreEqual ("(MonoDevelop.Debugger.Tests.TestApp.TestEvaluation e,int f)", val.TypeName);
			} else {
				Assert.AreEqual (AllowTargetInvokes ? "{(, 3)}" : "{System.ValueTuple<MonoDevelop.Debugger.Tests.TestApp.TestEvaluation,int>}", val.Value);
				Assert.AreEqual ("System.ValueTuple<MonoDevelop.Debugger.Tests.TestApp.TestEvaluation,int>", val.TypeName);
			}

			richChildren = val.GetAllChildrenSync ();
			Assert.AreEqual ("(null)", richChildren.Single (c => c.Name == "e").Value);
			Assert.AreEqual ("3", richChildren.Single (c => c.Name == "f").Value);

			val = Eval ("FieldB.C");
			Assert.AreEqual ("bool", val.TypeName);
			Assert.AreEqual ("true", val.Value);

			val = Eval ("namedTuple.e");
			Assert.AreEqual ("MonoDevelop.Debugger.Tests.TestApp.TestEvaluation", val.TypeName);
			Assert.AreEqual ("(null)", val.Value);

			val = Eval ("numbers.GetLength(0)");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("3", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			IgnoreCorDebugger ("TODO: CorDebugger support explicit interfaces");

			val = Eval ("Bar.Prop");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("int", val.TypeName);
			Assert.AreEqual ("1", val.Value);

			val = Eval ("bug57425.Prop");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("string", val.TypeName);
			Assert.AreEqual ("\"3\"", val.Value);

			val = Eval ("Foo.Prop");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("int", val.TypeName);
			Assert.AreEqual ("2", val.Value);
			val = Eval ("FooBar.Prop");
			Assert.AreEqual ("int", val.TypeName);
			Assert.AreEqual ("3", val.Value);
			val = Eval ("((Bo)FooBar).Prop");
			Assert.AreEqual ("int", val.TypeName);
			Assert.AreEqual ("4", val.Value);

			if (!IsVsDebugger) {
				val = Eval ("FooBar");
				richChildren = val.GetAllChildrenSync ();
				Assert.AreEqual ("Prop (MonoDevelop.Debugger.Tests.TestApp.FooBar)", richChildren [0].Name);
				Assert.AreEqual ("int", richChildren [0].TypeName);
				Assert.AreEqual ("3", richChildren [0].Value);
				Assert.AreEqual ("Prop (MonoDevelop.Debugger.Tests.TestApp.Bo)", richChildren [1].Name);
				Assert.AreEqual ("int", richChildren [1].TypeName);
				Assert.AreEqual ("4", richChildren [1].Value);

				if (!AllowTargetInvokes) {
					var options = Session.Options.EvaluationOptions.Clone ();
					options.AllowTargetInvoke = true;

					Assert.IsTrue (richChildren [2].IsImplicitNotSupported);
					richChildren [2].Refresh (options);
					richChildren [2] = richChildren [2].Sync ();
					Assert.IsTrue (richChildren [3].IsImplicitNotSupported);
					richChildren [3].Refresh (options);
					richChildren [3] = richChildren [3].Sync ();
				}

				//TODO: Include interface names into sorting
				if ("Prop (MonoDevelop.Debugger.Tests.TestApp.IFoo)" == richChildren [2].Name) {
					Assert.AreEqual ("Prop (MonoDevelop.Debugger.Tests.TestApp.IFoo)", richChildren [2].Name);
					Assert.AreEqual ("int", richChildren [2].TypeName);
					Assert.AreEqual ("2", richChildren [2].Value);
					Assert.AreEqual ("Prop (MonoDevelop.Debugger.Tests.TestApp.IBar)", richChildren [3].Name);
					Assert.AreEqual ("int", richChildren [3].TypeName);
					Assert.AreEqual ("1", richChildren [3].Value);
				} else {
					Assert.AreEqual ("Prop (MonoDevelop.Debugger.Tests.TestApp.IFoo)", richChildren [3].Name);
					Assert.AreEqual ("int", richChildren [3].TypeName);
					Assert.AreEqual ("2", richChildren [3].Value);
					Assert.AreEqual ("Prop (MonoDevelop.Debugger.Tests.TestApp.IBar)", richChildren [2].Name);
					Assert.AreEqual ("int", richChildren [2].TypeName);
					Assert.AreEqual ("1", richChildren [2].Value);
				}
			}
		}

		[Test]
		public void NullableEquality ()
		{
			var val = Eval ("nullableBool == null");
			Assert.AreEqual ("false", val.Value);

			val = Eval ("nullableBool != null");
			Assert.AreEqual ("true", val.Value);

			val = Eval ("nullableBool != false");
			Assert.AreEqual ("true", val.Value);

			val = Eval ("nullableBool == true");
			Assert.AreEqual ("true", val.Value);
		}

		[Test]
		public void CallMethodWithNullableInt()
		{
			if (!AllowTargetInvokes)
				return;

			var soft = Session as SoftDebuggerSession;
			if (soft != null && soft.ProtocolVersion < new Version (2, 46))
				Assert.Ignore ("A newer version of the Mono runtime is required.");
			
			var val = Eval ("NullableHasValue1 (null)");
			Assert.AreEqual ("false", val.Value);
			val = Eval ("NullableHasValue1 (0)");
			Assert.AreEqual ("true", val.Value);
			val = Eval ("NullableHasValue1 ((byte)0)");
			Assert.AreEqual ("true", val.Value);
			val = Eval ("NullableHasValue1 (new int? (0))");
			Assert.AreEqual ("true", val.Value);
			val = Eval ("NullableHasValue1 ((int?)(0))");
			Assert.AreEqual ("true", val.Value);
		}

		[Test]
		public void ConditionalExpression ()
		{
			ObjectValue val = Eval ("true ? \"yes\" : \"no\"");
			Assert.AreEqual ("\"yes\"", val.Value);
			Assert.AreEqual ("string", val.TypeName);

			val = Eval ("false ? \"yes\" : \"no\"");
			Assert.AreEqual ("\"no\"", val.Value);
			Assert.AreEqual ("string", val.TypeName);
		}

		[Test]
		public void Cast ()
		{
			ObjectValue val;

			var soft = Session as SoftDebuggerSession;
			if (soft != null && soft.ProtocolVersion < new Version (2, 31))
				Assert.Ignore ("A newer version of the Mono runtime is required.");

			val = Eval ("(int)19.7");
			Assert.AreEqual ("19", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("(byte)n");
			Assert.AreEqual ("32", val.Value);
			Assert.AreEqual ("byte", val.TypeName);

			val = Eval ("(int)n");
			Assert.AreEqual ("32", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("(long)n");
			Assert.AreEqual ("32", val.Value);
			Assert.AreEqual ("long", val.TypeName);

			val = Eval ("(float)n");
			Assert.AreEqual ("32", val.Value);
			Assert.AreEqual ("float", val.TypeName);

			val = Eval ("(double)n");
			Assert.AreEqual ("32", val.Value);
			Assert.AreEqual ("double", val.TypeName);

			val = Eval ("(string)staticString");
			Assert.AreEqual ("\"some static\"", val.Value);
			Assert.AreEqual ("string", val.TypeName);

			val = Eval ("(int)numbers");
			Assert.IsTrue (val.IsError, "(int)numbers");

			val = Eval ("(int)this");
			Assert.IsTrue (val.IsError, "(int)this");

			val = Eval ("(C)a");
			Assert.IsTrue (val.IsError, "(C)a");

			val = Eval ("(C)b");
			Assert.IsTrue (val.IsError, "(C)b");

			val = Eval ("(C)c");
			Assert.AreEqual ("{C}", val.Value);
			Assert.AreEqual ("C", val.TypeName);

			val = Eval ("(B)a");
			Assert.IsTrue (val.IsError, "(B)a");

			val = Eval ("(B)b");
			Assert.AreEqual ("{B}", val.Value);
			Assert.AreEqual ("B", val.TypeName);

			val = Eval ("(B)c");
			Assert.AreEqual ("{C}", val.Value);
			Assert.AreEqual ("C", val.TypeName);

			val = Eval ("(A)a");
			Assert.AreEqual ("{A}", val.Value);
			Assert.AreEqual ("A", val.TypeName);

			val = Eval ("(A)b");
			Assert.AreEqual ("{B}", val.Value);
			Assert.AreEqual ("B", val.TypeName);

			val = Eval ("(A)c");
			Assert.AreEqual ("{C}", val.Value);
			Assert.AreEqual ("C", val.TypeName);

			// Try cast

			val = Eval ("c as A");
			Assert.AreEqual ("{C}", val.Value);
			Assert.AreEqual ("C", val.TypeName);

			val = Eval ("c as B");
			Assert.AreEqual ("{C}", val.Value);
			Assert.AreEqual ("C", val.TypeName);

			val = Eval ("c as C");
			Assert.AreEqual ("{C}", val.Value);
			Assert.AreEqual ("C", val.TypeName);

			val = Eval ("b as A");
			Assert.AreEqual ("{B}", val.Value);
			Assert.AreEqual ("B", val.TypeName);

			val = Eval ("b as B");
			Assert.AreEqual ("{B}", val.Value);
			Assert.AreEqual ("B", val.TypeName);

			val = Eval ("b as C");
			if (IsVsDebugger)
				Assert.AreEqual ("(null)", val.Value);
			else
				Assert.AreEqual ("null", val.Value);
			Assert.AreEqual ("C", val.TypeName);

			val = Eval ("a as A");
			Assert.AreEqual ("{A}", val.Value);
			Assert.AreEqual ("A", val.TypeName);

			val = Eval ("a as B");
			if (IsVsDebugger)
				Assert.AreEqual ("(null)", val.Value);
			else
				Assert.AreEqual ("null", val.Value);
			Assert.AreEqual ("B", val.TypeName);

			val = Eval ("a as C");
			if (IsVsDebugger)
				Assert.AreEqual ("(null)", val.Value);
			else
				Assert.AreEqual ("null", val.Value);
			Assert.AreEqual ("C", val.TypeName);

			if (!IsVsDebugger) {
				val = Eval ("a as string");
				Assert.AreEqual ("null", val.Value);
				Assert.AreEqual ("System.String", val.TypeName);
			}


			// Is operator

			val = Eval ("c is A");
			Assert.AreEqual ("true", val.Value);
			Assert.AreEqual ("bool", val.TypeName);

			val = Eval ("c is B");
			Assert.AreEqual ("true", val.Value);
			Assert.AreEqual ("bool", val.TypeName);

			val = Eval ("c is C");
			Assert.AreEqual ("true", val.Value);
			Assert.AreEqual ("bool", val.TypeName);

			val = Eval ("b is A");
			Assert.AreEqual ("true", val.Value);
			Assert.AreEqual ("bool", val.TypeName);

			val = Eval ("b is B");
			Assert.AreEqual ("true", val.Value);
			Assert.AreEqual ("bool", val.TypeName);

			val = Eval ("b is C");
			Assert.AreEqual ("false", val.Value);
			Assert.AreEqual ("bool", val.TypeName);

			val = Eval ("a is A");
			Assert.AreEqual ("true", val.Value);
			Assert.AreEqual ("bool", val.TypeName);

			val = Eval ("a is B");
			Assert.AreEqual ("false", val.Value);
			Assert.AreEqual ("bool", val.TypeName);

			val = Eval ("a is C");
			Assert.AreEqual ("false", val.Value);
			Assert.AreEqual ("bool", val.TypeName);

			val = Eval ("a is string");
			Assert.AreEqual ("false", val.Value);
			Assert.AreEqual ("bool", val.TypeName);

			// Enum cast

			val = Eval ("(int)SomeEnum.two");
			Assert.AreEqual ("2", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("(long)SomeEnum.two");
			Assert.AreEqual ("2", val.Value);
			Assert.AreEqual ("long", val.TypeName);

			val = Eval ("(SomeEnum)2");
			Assert.AreEqual ("SomeEnum.two", val.Value);
			Assert.AreEqual ("two", val.DisplayValue);
			Assert.AreEqual ("SomeEnum", val.TypeName);

			val = Eval ("(SomeEnum)3");
			Assert.AreEqual ("SomeEnum.one | SomeEnum.two", val.Value);
			Assert.AreEqual ("one | two", val.DisplayValue);
			Assert.AreEqual ("SomeEnum", val.TypeName);

			IgnoreCorDebugger ("CorDebugger: Implicit casting");

			// Casting primitive <-> custom class via implicit operator
			val = Eval ("(myNint)3");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("{3}", val.Value);
			Assert.AreEqual ("myNint", val.TypeName);

			val = Eval ("(int)(myNint)4");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("4", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("TestCastingArgument(4)");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("\"4\"", val.Value);
			Assert.AreEqual ("string", val.TypeName);

			val = Eval ("new RichClass(5).publicPropInt1");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("5", val.Value);
			Assert.AreEqual ("int", val.TypeName);
		}

		[Test]
		public void BinaryOperators ()
		{
			ObjectValue val;

			var soft = Session as SoftDebuggerSession;
			if (soft != null && soft.ProtocolVersion < new Version (2, 31))
				Assert.Ignore ("A newer version of the Mono runtime is required.");

			// Boolean

			val = Eval ("true && true");
			Assert.AreEqual ("true", val.Value);
			Assert.AreEqual ("bool", val.TypeName);

			val = Eval ("true && false");
			Assert.AreEqual ("false", val.Value);
			Assert.AreEqual ("bool", val.TypeName);

			val = Eval ("false && true");
			Assert.AreEqual ("false", val.Value);
			Assert.AreEqual ("bool", val.TypeName);

			val = Eval ("false && false");
			Assert.AreEqual ("false", val.Value);
			Assert.AreEqual ("bool", val.TypeName);

			val = Eval ("false || false");
			Assert.AreEqual ("false", val.Value);
			Assert.AreEqual ("bool", val.TypeName);

			val = Eval ("false || true");
			Assert.AreEqual ("true", val.Value);
			Assert.AreEqual ("bool", val.TypeName);

			val = Eval ("true || false");
			Assert.AreEqual ("true", val.Value);
			Assert.AreEqual ("bool", val.TypeName);

			val = Eval ("true || true");
			Assert.AreEqual ("true", val.Value);
			Assert.AreEqual ("bool", val.TypeName);

			val = Eval ("false || 1");
			Assert.IsTrue (val.IsError, "false || 1");

			val = Eval ("1 || true");
			Assert.IsTrue (val.IsError, "1 || true");

			val = Eval ("true && 1");
			Assert.IsTrue (val.IsError, "true && 1");

			val = Eval ("1 && true");
			Assert.IsTrue (val.IsError, "1 && true");

			// Concat string

			val = Eval ("\"a\" + \"b\"");
			Assert.AreEqual ("\"ab\"", val.Value);
			Assert.AreEqual ("string", val.TypeName);

			val = Eval ("\"a\" + 2");
			Assert.AreEqual ("\"a2\"", val.Value);
			Assert.AreEqual ("string", val.TypeName);

			val = Eval ("2 + \"a\"");
			Assert.AreEqual ("\"2a\"", val.Value);
			Assert.AreEqual ("string", val.TypeName);

			val = Eval ("this + \"a\"");
			Assert.AreEqual ("\"MonoDevelop.Debugger.Tests.TestApp.TestEvaluationChilda\"", val.Value);
			Assert.AreEqual ("string", val.TypeName);

			// Equality

			val = Eval ("2 == 2");
			Assert.AreEqual ("true", val.Value);
			Assert.AreEqual ("bool", val.TypeName);

			val = Eval ("2 == 3");
			Assert.AreEqual ("false", val.Value);
			Assert.AreEqual ("bool", val.TypeName);

			val = Eval ("(long)2 == (int)2");
			Assert.AreEqual ("true", val.Value);
			Assert.AreEqual ("bool", val.TypeName);

			val = Eval ("SomeEnum.two == SomeEnum.one");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("false", val.Value);
			Assert.AreEqual ("bool", val.TypeName);

			val = Eval ("SomeEnum.one != SomeEnum.one");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("false", val.Value);
			Assert.AreEqual ("bool", val.TypeName);

			// Arithmetic

			val = Eval ("2 + 3");
			Assert.AreEqual ("5", val.Value);

			val = Eval ("2 + 2 == 4");
			Assert.AreEqual ("true", val.Value);
			Assert.AreEqual ("bool", val.TypeName);

			val = Eval ("(2 * 1.5f) / 2");
			Assert.AreEqual ("1.5", val.Value);
			Assert.AreEqual ("float", val.TypeName);

			val = Eval ("(uint)2 + 1");
			Assert.AreEqual ("3", val.Value);
			Assert.AreEqual ("uint", val.TypeName);

			val = Eval ("2 + (uint)1");
			Assert.AreEqual ("3", val.Value);
			Assert.AreEqual ("uint", val.TypeName);

			val = Eval ("(sbyte)1 + (byte)2");
			Assert.AreEqual ("3", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("(short)1 + (ushort)2");
			Assert.AreEqual ("3", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("(ulong)1 + (long)2");
			Assert.AreEqual ("3", val.Value);
			Assert.AreEqual ("ulong", val.TypeName);

			val = Eval ("-(2.3)");
			Assert.AreEqual ("-2.3", val.Value);
		}

		[Test]
		public void BinaryOperatorOverrides ()
		{
			ObjectValue val;

			if (!AllowTargetInvokes)
				return;

			var soft = Session as SoftDebuggerSession;
			if (soft != null && soft.ProtocolVersion < new Version (2, 31))
				Assert.Ignore ("Requires a newer version of the Mono runtime.");

			val = Eval ("ops1 == ops3");
			Assert.AreEqual ("false", val.Value);
			Assert.AreEqual ("bool", val.TypeName);

			val = Eval ("ops1 != ops3");
			Assert.AreEqual ("true", val.Value);
			Assert.AreEqual ("bool", val.TypeName);

			val = Eval ("ops2 == ops3");
			Assert.AreEqual ("true", val.Value);
			Assert.AreEqual ("bool", val.TypeName);

			val = Eval ("ops2 != ops3");
			Assert.AreEqual ("false", val.Value);
			Assert.AreEqual ("bool", val.TypeName);

			val = Eval ("ops1 <= ops2");
			Assert.AreEqual ("true", val.Value);
			Assert.AreEqual ("bool", val.TypeName);

			val = Eval ("ops1 < ops2");
			Assert.AreEqual ("true", val.Value);
			Assert.AreEqual ("bool", val.TypeName);

			val = Eval ("ops1 >= ops2");
			Assert.AreEqual ("false", val.Value);
			Assert.AreEqual ("bool", val.TypeName);

			val = Eval ("ops1 > ops2");
			Assert.AreEqual ("false", val.Value);
			Assert.AreEqual ("bool", val.TypeName);

			val = Eval ("ops1 + ops2");
			Assert.AreEqual ("{[BinaryOperatorOverrides 3]}", val.Value);
			Assert.AreEqual ("BinaryOperatorOverrides", val.TypeName);

			val = Eval ("ops1 - ops2");
			Assert.AreEqual ("{[BinaryOperatorOverrides -1]}", val.Value);
			Assert.AreEqual ("BinaryOperatorOverrides", val.TypeName);

			val = Eval ("ops1 * ops2");
			Assert.AreEqual ("{[BinaryOperatorOverrides 2]}", val.Value);
			Assert.AreEqual ("BinaryOperatorOverrides", val.TypeName);

			val = Eval ("ops2 / ops1");
			Assert.AreEqual ("{[BinaryOperatorOverrides 2]}", val.Value);
			Assert.AreEqual ("BinaryOperatorOverrides", val.TypeName);

			val = Eval ("ops1 % ops2");
			Assert.AreEqual ("{[BinaryOperatorOverrides 1]}", val.Value);
			Assert.AreEqual ("BinaryOperatorOverrides", val.TypeName);

			val = Eval ("ops1 & ops2");
			Assert.AreEqual ("{[BinaryOperatorOverrides 0]}", val.Value);
			Assert.AreEqual ("BinaryOperatorOverrides", val.TypeName);

			val = Eval ("ops1 | ops2");
			Assert.AreEqual ("{[BinaryOperatorOverrides 3]}", val.Value);
			Assert.AreEqual ("BinaryOperatorOverrides", val.TypeName);

			val = Eval ("ops1 ^ ops2");
			Assert.AreEqual ("{[BinaryOperatorOverrides 3]}", val.Value);
			Assert.AreEqual ("BinaryOperatorOverrides", val.TypeName);

			val = Eval ("ops1 << 1");
			Assert.AreEqual ("{[BinaryOperatorOverrides 2]}", val.Value);
			Assert.AreEqual ("BinaryOperatorOverrides", val.TypeName);

			val = Eval ("ops2 >> 1");
			Assert.AreEqual ("{[BinaryOperatorOverrides 1]}", val.Value);
			Assert.AreEqual ("BinaryOperatorOverrides", val.TypeName);
		}

		void AssertAssignment (string assignment, string variable, string value, string type)
		{
			ObjectValue val;

			val = Eval (assignment);
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}

			Assert.AreEqual (value, val.Value, $"Eval(\"{assignment}\")");
			Assert.AreEqual (type, val.TypeName, $"Eval(\"{assignment}\")");

			val = Eval (variable);
			if (!AllowTargetInvokes && val.IsImplicitNotSupported) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				val.Refresh (options);
				val = val.Sync ();
			}

			Assert.AreEqual (value, val.Value, $"Eval(\"{variable}\")");
			Assert.AreEqual (type, val.TypeName, $"Eval(\"{variable}\")");
		}

		[Test]
		public void MethodParameters ()
		{
			ObjectValue val;
			val = Eval ("stringParam");
			Assert.AreEqual ("\"testString\"", val.Value);
			Assert.AreEqual ("string", val.TypeName);

			val = Eval ("intParam");
			Assert.AreEqual ("55", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("intParam2");
			Assert.AreEqual ("66", val.Value);
			Assert.AreEqual ("int", val.TypeName);
		}

		[Test]
		public virtual void Assignment ()
		{
			AssertAssignment ("n = 6", "n", "6", "int");
			AssertAssignment ("n = 32", "n", "32", "int");

			AssertAssignment ("d = 33", "d", "33", "double");
			AssertAssignment ("d = 34.0", "d", "34", "double");
			AssertAssignment ("d = 35.0f", "d", "35", "double");

			AssertAssignment ("someString = \"test\"", "someString", "\"test\"", "string");
			AssertAssignment ("someString = \"hi\"", "someString", "\"hi\"", "string");

			AssertAssignment ("numbers[0] = \"test\"", "numbers[0]", "\"test\"", "string");
			AssertAssignment ("numbers[0] = \"one\"", "numbers[0]", "\"one\"", "string");

			AssertAssignment ("alist[0] = 6", "alist[0]", "6", "int");
			AssertAssignment ("alist[0] = 1", "alist[0]", "1", "int");

			AssertAssignment ("ProtectedStringProperty = \"test\"", "ProtectedStringProperty", "\"test\"", "string");
			AssertAssignment ("ProtectedStringProperty = \"hi\"", "ProtectedStringProperty", "\"hi\"", "string");
		}

		[Test]
		public virtual void AssignmentStatic ()
		{
			AssertAssignment ("staticString = \"test\"", "staticString", "\"test\"", "string");
			AssertAssignment ("staticString = \"some static\"", "staticString", "\"some static\"", "string");
		}

		[Test]
		public void FormatBool ()
		{
			ObjectValue val;

			val = Eval ("true");
			Assert.AreEqual ("true", val.Value);

			val = Eval ("false");
			Assert.AreEqual ("false", val.Value);
		}

		[Test]
		public void FormatNumber ()
		{
			ObjectValue val;
			val = Eval ("(int)123");
			Assert.AreEqual ("123", val.Value);
			val = Eval ("(int)-123");
			Assert.AreEqual ("-123", val.Value);

			val = Eval ("(long)123");
			Assert.AreEqual ("123", val.Value);
			val = Eval ("(long)-123");
			Assert.AreEqual ("-123", val.Value);

			val = Eval ("(byte)123");
			Assert.AreEqual ("123", val.Value);

			val = Eval ("(uint)123");
			Assert.AreEqual ("123", val.Value);

			val = Eval ("(ulong)123");
			Assert.AreEqual ("123", val.Value);

			var soft = Session as SoftDebuggerSession;
			if (soft != null && soft.ProtocolVersion < new Version (2, 31))
				return;

			val = Eval ("dec");
			Assert.AreEqual ("123.456", val.Value);
		}

		[Test]
		public void FormatString ()
		{
			ObjectValue val;
			val = Eval ("\"hi\"");
			Assert.AreEqual ("\"hi\"", val.Value);

			val = Eval ("EscapedStrings");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("\" \\\" \\\\ \\a \\b \\f \\v \\n \\r \\t\"", val.Value);

			val = Eval ("\" \\\" \\\\ \\a \\b \\f \\v \\n \\r \\t\"");
			Assert.AreEqual ("\" \\\" \\\\ \\a \\b \\f \\v \\n \\r \\t\"", val.Value);
		}

		[Test]
		public void FormatChar ()
		{
			ObjectValue val;
			val = Eval ("'A'");
			Assert.AreEqual ("'A'", val.Value);
			Assert.AreEqual ("65 'A'", val.DisplayValue);

			val = Eval ("'\\0'");
			Assert.AreEqual ("'\\0'", val.Value);
			Assert.AreEqual ("0 '\\0'", val.DisplayValue);

			val = Eval ("'\"'");
			Assert.AreEqual ("'\"'", val.Value);
			Assert.AreEqual ("34 '\"'", val.DisplayValue);

			val = Eval ("'\\''");
			Assert.AreEqual ("'\\''", val.Value);
			Assert.AreEqual ("39 '\\''", val.DisplayValue);

			val = Eval ("'\\\\'");
			Assert.AreEqual ("'\\\\'", val.Value);
			Assert.AreEqual ("92 '\\\\'", val.DisplayValue);

			val = Eval ("'\\a'");
			Assert.AreEqual ("'\\a'", val.Value);
			Assert.AreEqual ("7 '\\a'", val.DisplayValue);

			val = Eval ("'\\b'");
			Assert.AreEqual ("'\\b'", val.Value);
			Assert.AreEqual ("8 '\\b'", val.DisplayValue);

			val = Eval ("'\\f'");
			Assert.AreEqual ("'\\f'", val.Value);
			Assert.AreEqual ("12 '\\f'", val.DisplayValue);

			val = Eval ("'\\v'");
			Assert.AreEqual ("'\\v'", val.Value);
			Assert.AreEqual ("11 '\\v'", val.DisplayValue);

			val = Eval ("'\\n'");
			Assert.AreEqual ("'\\n'", val.Value);
			Assert.AreEqual ("10 '\\n'", val.DisplayValue);

			val = Eval ("'\\r'");
			Assert.AreEqual ("'\\r'", val.Value);
			Assert.AreEqual ("13 '\\r'", val.DisplayValue);

			val = Eval ("'\\t'");
			Assert.AreEqual ("'\\t'", val.Value);
			Assert.AreEqual ("9 '\\t'", val.DisplayValue);
		}

		[Test]
		public void FormatObject ()
		{
			ObjectValue val;

			var soft = Session as SoftDebuggerSession;
			if (soft != null && soft.ProtocolVersion < new Version (2, 31))
				Assert.Ignore ("A newer version of the Mono runtime is required.");

			val = Eval ("c");
			Assert.AreEqual ("{C}", val.Value);
			Assert.AreEqual ("C", val.TypeName);

			val = Eval ("withDisplayString");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.AreEqual ("{WithDisplayString}", val.Value);

				val.Refresh (options);
				val = val.Sync ();
			}
			if (IsVsDebugger)
				Assert.AreEqual ("Some \"one\" Value 2 End", val.Value);
			else
				Assert.AreEqual ("Some one Value 2 End", val.Value);
			Assert.AreEqual ("WithDisplayString", val.TypeName);

			val = Eval ("withProxy");
			Assert.AreEqual ("{WithProxy}", val.Value);
			Assert.AreEqual ("WithProxy", val.TypeName);

			/*			val = Eval ("withToString");
						Assert.AreEqual ("{SomeString}", val.Value);
						Assert.AreEqual ("WithToString", val.TypeName);*/
		}

		[Test]
		public void FormatArray ()
		{
			ObjectValue val;

			val = Eval ("numbers");
			Assert.AreEqual ("{string[3]}", val.Value);
			Assert.AreEqual ("string[]", val.TypeName);

			val = Eval ("numbersArrays");
			Assert.AreEqual ("{int[2][]}", val.Value);
			Assert.AreEqual ("int[][]", val.TypeName);

			IgnoreCorDebugger ("Randomly fails");

			val = Eval ("arrayWithLowerBounds");
			Assert.AreEqual ("int[,,]", val.TypeName);
			Assert.AreEqual ("{int[3,4,5]}", val.Value);

			val = Eval ("numbersMulti");
			Assert.AreEqual ("{int[3,4,5]}", val.Value);
			Assert.AreEqual ("int[,,]", val.TypeName);
			Assert.IsFalse (val.IsNull);

			val = Eval ("nulledByteArray");
			Assert.AreEqual ("(null)", val.Value);
			Assert.AreEqual ("byte[]", val.TypeName);
			Assert.IsTrue (val.IsNull);
		}

		[Test]
		public void FormatGeneric ()
		{
			ObjectValue val;

			try {
				Session.Options.EvaluationOptions.AllowTargetInvoke = false;
				val = Eval ("dict");
			} finally {
				Session.Options.EvaluationOptions.AllowTargetInvoke = AllowTargetInvokes;
			}
			//Assert.AreEqual ("{System.Collections.Generic.Dictionary<int,string[]>}", val.Value);
			Assert.AreEqual ("System.Collections.Generic.Dictionary<int,string[]>", val.TypeName);

			try {
				Session.Options.EvaluationOptions.AllowTargetInvoke = false;
				val = Eval ("dictArray");
			} finally {
				Session.Options.EvaluationOptions.AllowTargetInvoke = AllowTargetInvokes;
			}
			Assert.AreEqual ("{System.Collections.Generic.Dictionary<int,string[]>[2,3]}", val.Value);
			Assert.AreEqual ("System.Collections.Generic.Dictionary<int,string[]>[,]", val.TypeName);


			var soft = Session as SoftDebuggerSession;
			if (soft != null && soft.ProtocolVersion < new Version (2, 31))
				return;

			val = Eval ("thing.done");
			Assert.AreEqual ("{Thing<string>.Done<int>[1]}", val.Value);
			Assert.AreEqual ("Thing<string>.Done<int>[]", val.TypeName);

			val = Eval ("done");
			Assert.AreEqual ("{Thing<string>.Done<int>}", val.Value);
			Assert.AreEqual ("Thing<string>.Done<int>", val.TypeName);
		}

		[Test]
		public void FormatEnum ()
		{
			ObjectValue val;

			var soft = Session as SoftDebuggerSession;
			if (soft != null && soft.ProtocolVersion < new Version (2, 31))
				Assert.Ignore ("A newer version of the Mono runtime is required.");

			val = Eval ("SomeEnum.one");
			Assert.AreEqual ("SomeEnum.one", val.Value);
			Assert.AreEqual ("one", val.DisplayValue);

			val = Eval ("SomeEnum.two");
			Assert.AreEqual ("SomeEnum.two", val.Value);
			Assert.AreEqual ("two", val.DisplayValue);

			val = Eval ("SomeEnum.one | SomeEnum.two");
			Assert.AreEqual ("SomeEnum.one | SomeEnum.two", val.Value);
			Assert.AreEqual ("one | two", val.DisplayValue);
		}

		[Test]
		public void LambdaInvoke ()
		{
			ObjectValue val;

			var soft = Session as SoftDebuggerSession;
			if (soft != null && soft.ProtocolVersion < new Version (2, 31))
				Assert.Ignore ("A newer version of the Mono runtime is required.");

			val = Eval ("action");
			Assert.AreEqual ("{System.Action}", val.Value);
			Assert.AreEqual ("System.Action", val.TypeName);

			val = Eval ("modifyInLamda");
			Assert.AreEqual ("\"modified\"", val.Value);
			Assert.AreEqual ("string", val.TypeName);

		}

		[Test]
		public void Structures ()
		{
			ObjectValue val;

			var soft = Session as SoftDebuggerSession;
			if (soft != null && soft.ProtocolVersion < new Version (2, 31))
				Assert.Ignore ("A newer version of the Mono runtime is required.");

			val = Eval ("simpleStruct");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.AreEqual ("{SimpleStruct}", val.Value);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("{str 45 }", val.Value);
			Assert.AreEqual ("SimpleStruct", val.TypeName);

			val = Eval ("nulledSimpleStruct");
			if (IsVsDebugger)
				Assert.AreEqual ("(null)", val.Value);
			else
				Assert.AreEqual ("null", val.Value);
			Assert.AreEqual ("SimpleStruct?", val.TypeName);
		}

		[Test]
		[Ignore ("TODO")]
		public void SdbFailingTests ()
		{
			ObjectValue val;

			val = Eval ("base.TestMethodBase (\"23\")");
			Assert.AreEqual ("25", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("base.TestMethodBase (42)");
			Assert.AreEqual ("44", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			//When fixed put into TypeReferenceGeneric
			val = Eval ("Dictionary<string,NestedClass>");
			Assert.AreEqual ("System.Collections.Generic.Dictionary<string,MonoDevelop.Debugger.Tests.TestApp.TestEvaluation.NestedClass>", val.Value);
			Assert.AreEqual ("<type>", val.TypeName);
			Assert.AreEqual (ObjectValueFlags.Type, val.Flags & ObjectValueFlags.OriginMask);

			//When fixed put into TypeReferenceGeneric
			val = Eval ("NestedGenericClass<int,string>");
			Assert.AreEqual ("MonoDevelop.Debugger.Tests.TestApp.TestEvaluation.NestedGenericClass<int,string>", val.Value);
			Assert.AreEqual ("<type>", val.TypeName);
			Assert.AreEqual (ObjectValueFlags.Type, val.Flags & ObjectValueFlags.OriginMask);

		}

		[Test]
		public void ObjectCreation ()
		{
			ObjectValue val;

			var soft = Session as SoftDebuggerSession;
			if (soft != null && soft.ProtocolVersion < new Version (2, 31))
				Assert.Ignore ("A newer version of the Mono runtime is required.");

			val = Eval ("new A().ConstructedBy");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("\"NoArg\"", val.Value);

			val = Eval ("new A(7).ConstructedBy");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("\"IntArg\"", val.Value);

			val = Eval ("new A(\"someString\").ConstructedBy");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("\"StringArg\"", val.Value);
		}

		[Test]
		public void StructCreation ()
		{
			if (!AllowTargetInvokes)
				return;

			var soft = Session as SoftDebuggerSession;

			if (soft == null)
				Assert.Ignore ("TODO: Win32 support generic invokes");

			if (soft != null && soft.ProtocolVersion < new Version (2, 31))
				Assert.Inconclusive ("A newer version of the Mono runtime is required.");

			ObjectValue val;

			val = Eval ("new SimpleStruct()");
			Assert.AreEqual ("SimpleStruct", val.TypeName);
		}

		[Test]
		public void Inheriting ()
		{
			ObjectValue val;

			var soft = Session as SoftDebuggerSession;
			if (soft != null && soft.ProtocolVersion < new Version (2, 31))
				Assert.Ignore ("A newer version of the Mono runtime is required.");

			val = Eval ("a.Prop");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("1", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("a.PropNoVirt1");
			if (!AllowTargetInvokes && soft == null) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("1", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("a.PropNoVirt2");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("1", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("a.IntField");
			Assert.AreEqual ("1", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("a.TestMethod ()");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("1", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("a.TestMethod (\"23\")");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("24", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("a.TestMethod (42)");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("43", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			if (soft != null && soft.ProtocolVersion < new Version (2, 40))
				Assert.Ignore ("A newer version of the Mono runtime is required.");

			val = Eval ("b.Prop");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("2", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("((B)b).PropNoVirt1");
			if (!AllowTargetInvokes && soft == null) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("2", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("((B)b).PropNoVirt2");
			if (!AllowTargetInvokes && soft == null) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("2", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("((B)b).IntField");
			Assert.AreEqual ("2", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("this.TestMethodBase ()");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("1", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("this.TestMethodBase (\"23\")");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("24", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("this.TestMethodBase (42)");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("43", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("this.TestMethodBaseNotOverrided ()");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("1", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("TestMethodBase ()");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("1", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("TestMethodBase (\"23\")");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("24", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("TestMethodBase (42)");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("43", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("TestMethodBaseNotOverrided ()");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("1", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("b.TestMethod (\"23\")");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("25", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("base.TestMethodBaseNotOverrided ()");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("1", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("base.TestMethodBase ()");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("2", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			IgnoreCorDebugger ("Not working on CorDebugger");

			val = Eval ("b.PropNoVirt2");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("1", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("b.IntField");
			Assert.AreEqual ("1", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("b.PropNoVirt1");
			if (!AllowTargetInvokes && soft == null) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("1", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("System.Text.Encoding.UTF8.GetPreamble ()");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("byte[]", val.TypeName);
			Assert.AreEqual ("{byte[3]}", val.Value);

			var bytes = ((RawValueArray)val.GetRawValue ()).ToArray ();
			Assert.AreEqual (239, bytes.GetValue (0));
			Assert.AreEqual (187, bytes.GetValue (1));
			Assert.AreEqual (191, bytes.GetValue (2));

			val = Eval ("b.TestMethod ()");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("1", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			val = Eval ("b.TestMethod (42)");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.AreEqual ("43", val.Value);
			Assert.AreEqual ("int", val.TypeName);

			if (AllowTargetInvokes) {
				val = Eval ("bar.Foo");

				if (IsVsDebugger)
					Assert.AreEqual ("MonoDevelop.Debugger.Tests.TestApp.BaseClass.MyEnum.Black", val.Value);
				else
					Assert.AreEqual ("MonoDevelop.Debugger.Tests.TestApp.BaseClass+MyEnum.Black", val.Value);
				Assert.AreEqual ("MonoDevelop.Debugger.Tests.TestApp.BaseClass.MyEnum", val.TypeName);
			}
		}

		[Test]
		public void Lists ()
		{
			ObjectValue [] children;
			ObjectValue val;

			var soft = Session as SoftDebuggerSession;
			if (soft != null && soft.ProtocolVersion < new Version (2, 31))
				Assert.Ignore ("A newer version of the Mono runtime is required.");

			val = Eval ("dict");
			children = val.GetAllChildrenSync ();

			if (AllowTargetInvokes || IsVsDebugger) {
				// AllowTargetInvokes also allows debugger proxies
				Assert.AreEqual (2, children.Length);
				Assert.AreEqual ("[0]", children [0].Name);
				Assert.AreEqual ("{[5, System.String[]]}", children [0].Value);
				Assert.AreEqual ("Raw View", children [1].Name);

				children = children [0].GetAllChildrenSync ();
				Assert.AreEqual ("Key", children [0].Name);
				Assert.AreEqual ("5", children [0].Value);
				Assert.AreEqual ("int", children [0].TypeName);
				Assert.AreEqual ("Value", children [1].Name);
				Assert.AreEqual ("{string[2]}", children [1].Value);

				children = children [1].GetAllChildrenSync ();
				Assert.AreEqual ("\"a\"", children [0].Value);
				Assert.AreEqual ("string", children [0].TypeName);
				Assert.AreEqual ("\"b\"", children [1].Value);
			} else {
				// when AllowTargetInvokes is disabled, it also disables debugger proxies
				Assert.AreEqual (18, children.Length);
				Assert.AreEqual ("Comparer", children [0].Name);
				Assert.AreEqual ("Static members", children [15].Name);
				Assert.AreEqual ("Non-public members", children [16].Name);
				Assert.AreEqual ("IEnumerator", children [17].Name);
			}

			val = Eval ("stringList");
			children = val.GetAllChildrenSync ();

			if (AllowTargetInvokes || IsVsDebugger) {
				// AllowTargetInvokes also allows debugger proxies
				Assert.AreEqual (4, children.Length);
				Assert.AreEqual ("[0]", children [0].Name);
				Assert.AreEqual ("[1]", children [1].Name);
				Assert.AreEqual ("[2]", children [2].Name);
				Assert.AreEqual ("Raw View", children [3].Name);
				Assert.AreEqual ("\"aaa\"", children [0].Value);
				Assert.AreEqual ("\"bbb\"", children [1].Value);
				Assert.AreEqual ("\"ccc\"", children [2].Value);
			} else {
				// when AllowTargetInvokes is disabled, it also disables debugger proxies
				Assert.AreEqual (10, children.Length);
				Assert.AreEqual ("Capacity", children [0].Name);
				Assert.AreEqual ("Static members", children [7].Name);
				Assert.AreEqual ("Non-public members", children [8].Name);
				Assert.AreEqual ("IEnumerator", children [9].Name);
			}

			val = Eval ("alist");
			children = val.GetAllChildrenSync ();

			if (AllowTargetInvokes || IsVsDebugger) {
				// AllowTargetInvokes also allows debugger proxies
				Assert.AreEqual (4, children.Length);
				Assert.AreEqual ("[0]", children [0].Name);
				Assert.AreEqual ("[1]", children [1].Name);
				Assert.AreEqual ("[2]", children [2].Name);
				Assert.AreEqual ("Raw View", children [3].Name);
				Assert.AreEqual ("1", children [0].Value);
				Assert.AreEqual ("\"two\"", children [1].Value);
				Assert.AreEqual ("3", children [2].Value);
			} else {
				// when AllowTargetInvokes is disabled, it also disables debugger proxies
				Assert.AreEqual (9, children.Length);
				Assert.AreEqual ("Capacity", children [0].Name);
				Assert.AreEqual ("Count", children [1].Name);
				Assert.AreEqual ("IsFixedSize", children [2].Name);
				Assert.AreEqual ("IsReadOnly", children [3].Name);
				Assert.AreEqual ("IsSynchronized", children [4].Name);
				Assert.AreEqual ("SyncRoot", children [5].Name);
				Assert.AreEqual ("Static members", children [6].Name);
				Assert.AreEqual ("Non-public members", children [7].Name);
				Assert.AreEqual ("IEnumerator", children [8].Name);
			}
		}

		[Test]
		[Ignore ("TODO: Evaluating dynamic objects")]
		public void DynamicObjects ()
		{
			ObjectValue val;
			val = Eval ("dynObj.someInt");
			Assert.AreEqual ("dynamic {int}", val.TypeName);
			Assert.AreEqual ("53", val.Value);

			val = Eval ("dynObj.someString");
			Assert.AreEqual ("dynamic {string}", val.TypeName);
			Assert.AreEqual ("\"Hello dynamic objects!\"", val.Value);
		}

		[Test]
		public void GetTypeTest ()
		{
			if (Session.GetType ().Name == "CorDebuggerSession") {
				Assert.Ignore ("TODO: GetType() is not implemented in CorDebugger");
			}
			ObjectValue val;
			val = Eval ("a.GetType()");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.IsTrue (val.TypeName == "System.MonoType" || val.TypeName == "System.RuntimeType", "Incorrect type name: " + val.TypeName);
			Assert.AreEqual ("{A}", val.Value);

			val = Eval ("this.GetType()");
			if (!AllowTargetInvokes) {
				var options = Session.Options.EvaluationOptions.Clone ();
				options.AllowTargetInvoke = true;

				if (!IsVsDebugger)
					Assert.IsTrue (val.IsImplicitNotSupported);

				val.Refresh (options);
				val = val.Sync ();
			}
			Assert.IsTrue (val.TypeName == "System.MonoType" || val.TypeName == "System.RuntimeType", "Incorrect type name: " + val.TypeName);
			Assert.AreEqual ("{MonoDevelop.Debugger.Tests.TestApp.TestEvaluationChild}", val.Value);
		}

		[Test]
		public void IsOperatorTest ()
		{
			ObjectValue val;
			val = Eval ("b is A");
			Assert.AreEqual ("bool", val.TypeName);
			Assert.AreEqual ("true", val.Value);

			val = Eval ("b is B");
			Assert.AreEqual ("bool", val.TypeName);
			Assert.AreEqual ("true", val.Value);

			val = Eval ("b is C");
			Assert.AreEqual ("bool", val.TypeName);
			Assert.AreEqual ("false", val.Value);

			val = Eval ("5 is byte?");
			Assert.AreEqual ("bool", val.TypeName);
#pragma warning disable 184 //expression never of provided type
			var expected = (5 is byte?).ToString ().ToLower ();
#pragma warning restore 184
			Assert.AreEqual (expected, val.Value);


			val = Eval ("((byte)5) is byte?");
			Assert.AreEqual ("bool", val.TypeName);
#pragma warning disable 183 //expression always of provided type
			expected = (((byte)5) is byte?).ToString ().ToLower ();
#pragma warning restore 183
			Assert.AreEqual (expected, val.Value);


			val = Eval ("5.0 is double");
			Assert.AreEqual ("bool", val.TypeName);
#pragma warning disable 183 //expression always of provided type
			expected = (5.0 is double).ToString ().ToLower ();
#pragma warning restore 183
			Assert.AreEqual (expected, val.Value);


			val = Eval ("5.0 is float");
			Assert.AreEqual ("bool", val.TypeName);
#pragma warning disable 184 //expression never of provided type
			expected = (5.0 is float).ToString ().ToLower ();
#pragma warning restore 184
			Assert.AreEqual (expected, val.Value);

			val = Eval ("null is object");
			Assert.AreEqual ("bool", val.TypeName);
#pragma warning disable 184 //expression never of provided type
			expected = (null is object).ToString ().ToLower ();
#pragma warning restore 184
			Assert.AreEqual (expected, val.Value);
		}

		[Test]
		public void DebugDisplayTest ()
		{
			ObjectValue val;
			if (AllowTargetInvokes) {
				val = Eval ("debugDisplayMethodTest");
				Assert.AreEqual ("DebuggerDisplayMethodTest", val.TypeName);
				Assert.AreEqual ("First Int:32 Second Int:43", val.Value);
			}
		}

		[Test]
		public void ArrayTests ()
		{
			ObjectValue val;

			val = Eval ("numbersMulti[1,2,3]");
			Assert.AreEqual ("int", val.TypeName);
			Assert.AreEqual ("133", val.Value);

			val = Eval ("numbersArrays[0][7]");
			Assert.AreEqual ("int", val.TypeName);
			Assert.AreEqual ("24", val.Value);

			if (!IsVsDebugger) {
				val = Eval ("arrayWithLowerBounds[5,6,7]");
				Assert.AreEqual ("int", val.TypeName);
				Assert.AreEqual ("114", val.Value);
			}

			if (AllowTargetInvokes) {
				val = Eval ("arrayWithLowerBounds.GetValue(5,6,7)");
				Assert.AreEqual ("int", val.TypeName);
				Assert.AreEqual ("114", val.Value);
			}

			var children = Eval ("arrayWithLowerBounds").GetAllChildrenSync ();
			if (IsVsDebugger)
				Assert.AreEqual ("[5, 4, 3]", children [0].Name);
			else
				Assert.AreEqual ("[5, ...]", children [0].Name);

			if (!IsVsDebugger) {
				// Note: NetCoreDeugger gets an error trying to evaluate this
				children = Eval ("myExtremeArray").GetAllChildrenSync ();
				Assert.AreEqual (3, children.Length);
				Assert.AreEqual ("object", children [0].TypeName);
				Assert.AreEqual ("[-2147483648]", children [0].Name);
				Assert.IsTrue (children [0].IsNull);

				Assert.AreEqual ("string", children [1].TypeName);
				Assert.AreEqual ("[-2147483647]", children [1].Name);
				Assert.IsFalse (children [1].IsNull);
				Assert.AreEqual ("\"b38c0da4-a009-409d-bc78-2a051267d05a\"", children [1].Value);

				Assert.AreEqual ("object", children [2].TypeName);
				Assert.AreEqual ("[-2147483646]", children [2].Name);
				Assert.IsTrue (children [2].IsNull);
			}

			if (!AllowTargetInvokes)
				return;
			
			children = Eval ("new int [2]").GetAllChildrenSync ();
			Assert.AreEqual (2, children.Length);

			Assert.AreEqual ("int", children [0].TypeName);
			Assert.AreEqual ("[0]", children [0].Name);
			Assert.AreEqual ("0", children [0].Value);

			Assert.AreEqual ("int", children [1].TypeName);
			Assert.AreEqual ("[1]", children [1].Name);
			Assert.AreEqual ("0", children [1].Value);

			children = Eval ("new int [2] { 1, 2 }").GetAllChildrenSync ();
			Assert.AreEqual (2, children.Length);

			Assert.AreEqual ("int", children [0].TypeName);
			Assert.AreEqual ("[0]", children [0].Name);
			Assert.AreEqual ("1", children [0].Value);

			Assert.AreEqual ("int", children [1].TypeName);
			Assert.AreEqual ("[1]", children [1].Name);
			Assert.AreEqual ("2", children [1].Value);

			val = Eval ("new int [2,3] { { 1, 2, 3 }, { 6, 5, 4 } }.GetValue(1,1)");
			Assert.AreEqual ("int", val.TypeName);
			Assert.AreEqual ("5", val.Value);
		}

		[Test]
		public void DefaultExpression ()
		{
			var val = Eval ("default (string)");
			Assert.AreEqual ("(null)", val.Value);
			Assert.AreEqual ("string", val.TypeName);

			val = Eval ("default (byte)");
			Assert.AreEqual ("byte", val.TypeName);
			Assert.AreEqual ("0", val.Value);

			val = Eval ("default (bool)");
			Assert.AreEqual ("bool", val.TypeName);
			Assert.AreEqual ("false", val.Value);

			val = Eval ("default (int?)");
			Assert.AreEqual ("int?", val.TypeName);
			Assert.AreEqual ("(null)", val.Value);
		}

		[Test]
		public void RawByteArrayToArray ()
		{
			var val = Eval ("rawByteArray");
			Assert.AreEqual ("byte[]", val.TypeName, "rawByteArray");
			Assert.AreEqual ("{byte[256]}", val.Value);

			var rawArray = (RawValueArray)val.GetRawValue ();
			var bytes = rawArray.ToArray ();
			for (int i = 0; i < 256; i++)
				Assert.AreEqual ((byte) i, bytes.GetValue (i));
		}

		[Test]
		public void RawByteArrayChunking ()
		{
			var val = Eval ("rawByteArray");
			Assert.AreEqual ("byte[]", val.TypeName, "rawByteArray");
			Assert.AreEqual ("{byte[256]}", val.Value);

			var rawArray = (RawValueArray)val.GetRawValue ();
			int index = 0;

			while (index < 256) {
				var bytes = rawArray.GetValues (index, 32);
				Assert.AreEqual (32, bytes.Length);
				for (int i = 0; i < bytes.Length; i++)
					Assert.AreEqual ((byte) (index + i), bytes.GetValue (i));
				index += bytes.Length;
			}
		}

		[Test]
		public void RawByteArrayRandomAccess ()
		{
			var val = Eval ("rawByteArray");
			Assert.AreEqual ("byte[]", val.TypeName, "rawByteArray");
			Assert.AreEqual ("{byte[256]}", val.Value);

			var rawArray = (RawValueArray)val.GetRawValue ();
			var indexes = new int[] { 57, 179, 97, 229 };

			foreach (var startIndex in indexes) {
				var expected = Math.Min(32, 256 - startIndex);
				var bytes = rawArray.GetValues (startIndex, expected);
				Assert.AreEqual (expected, bytes.Length, "expected {0} bytes for chunk starting at {1}", expected, startIndex);
				for (int i = 0; i < bytes.Length; i++)
					Assert.AreEqual ((byte) (startIndex + i), bytes.GetValue (i), "incorrect byte at index {0}", startIndex + i);
			}
		}
	}
}
