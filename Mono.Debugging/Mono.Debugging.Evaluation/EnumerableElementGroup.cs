//
// IEnumerableSource.cs
//
// Author:
//       David Karlaš <david.karlas@xamarin.com>
//
// Copyright (c) 2014 Xamarin, Inc (http://www.xamarin.com)
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
using System.Linq;
using Mono.Debugging.Backend;
using Mono.Debugging.Client;
using System.Collections.Generic;

namespace Mono.Debugging.Evaluation
{
	class EnumerableSource : IObjectValueSource
	{
		object obj;
		EvaluationContext ctx;
		List<ObjectValue> elements;
		List<object> values;

		public EnumerableSource (object source, EvaluationContext ctx)
		{
			this.obj = source;
			this.ctx = ctx;
		}

		bool MoveNext (object type, object enumerator)
		{
			try {
				return (bool)ctx.Adapter.TargetObjectToObject (ctx, ctx.Adapter.RuntimeInvoke (ctx, type, enumerator, "MoveNext", new object[0], new object[0]));
			} catch (EvaluatorException e) {
				if (e.Message.StartsWith ("Method `MoveNext' not found in type")) {
					return (bool)ctx.Adapter.TargetObjectToObject (ctx, ctx.Adapter.RuntimeInvoke (ctx, type, enumerator, "System.Collections.IEnumerator.MoveNext", new object[0], new object[0]));
				} else {
					throw;
				}
			}
		}

		void FetchElements ()
		{
			if (elements == null) {
				elements = new List<ObjectValue> ();
				values = new List<object> ();
				object enumerator = null;
				try {
					enumerator = ctx.Adapter.RuntimeInvoke (ctx, ctx.Adapter.GetValueType (ctx, obj), obj, "GetEnumerator", new object[0], new object[0]);
				} catch (EvaluatorException e) {
					if (e.Message.StartsWith ("Method `GetEnumerator' not found in type")) {
						enumerator = ctx.Adapter.RuntimeInvoke (ctx, ctx.Adapter.GetValueType (ctx, obj), obj, "System.Collections.IEnumerable.GetEnumerator", new object[0], new object[0]);
					} else {
						throw;
					}
				}
				var type = ctx.Adapter.GetValueType (ctx, enumerator);
				int i = 0;
				while (MoveNext (type, enumerator)) {
					var valCurrent = ctx.Adapter.GetMember (ctx, null, type, enumerator, "Current");
					if (valCurrent == null) {
						valCurrent = ctx.Adapter.GetMember (ctx, null, type, enumerator, "System.Collections.IEnumerator.Current");
					}
					var val = valCurrent.Value;
					values.Add (val);
					if (val != null) {
						elements.Add (ctx.Adapter.CreateObjectValue (ctx, this, new ObjectPath ("[" + i + "]"), val, ObjectValueFlags.ReadOnly));
					} else {
						elements.Add (Mono.Debugging.Client.ObjectValue.CreateNullObject (this, "[" + i + "]", ctx.Adapter.GetDisplayTypeName (ctx.Adapter.GetTypeName (ctx, valCurrent.Type)), ObjectValueFlags.ReadOnly));
					}
					i++;
				}
			}
		}


		public object GetElement (int idx)
		{
			FetchElements ();
			return values [idx];
		}

		public ObjectValue[] GetChildren (ObjectPath path, int index, int count, EvaluationOptions options)
		{
			FetchElements ();
			int idx;
			if (int.TryParse (path.LastName.Replace ("[", "").Replace ("]", ""), out idx)) {
				return ctx.Adapter.GetObjectValueChildren (ctx, null, values [idx], -1, -1);
			}
			if (index < 0)
				index = 0;
			if (count == 0)
				return new ObjectValue[0];
			if (count < 0 || index + count > elements.Count) {
				return elements.Skip (index).ToArray ();
			} else {
				return elements.Skip (index).Take (count).ToArray ();
			}
		}

		public EvaluationResult SetValue (ObjectPath path, string value, EvaluationOptions options)
		{
			throw new InvalidOperationException ("Elements of IEnumerable can not be set");
		}

		public ObjectValue GetValue (ObjectPath path, EvaluationOptions options)
		{
			FetchElements ();
			int idx;
			if (int.TryParse (path.LastName.Replace ("[", "").Replace ("]", ""), out idx)) {
				return elements [idx];
			}
			return null;
		}

		public object GetRawValue (ObjectPath path, EvaluationOptions options)
		{
			int idx = int.Parse (path.LastName.Replace ("[", "").Replace ("]", ""));
			EvaluationContext cctx = ctx.WithOptions (options);
			return cctx.Adapter.ToRawValue (cctx, new EnumerableObjectSource (this, idx), GetElement (idx));
		}

		public void SetRawValue (ObjectPath path, object value, EvaluationOptions options)
		{
			throw new InvalidOperationException ("Elements of IEnumerable can not be set");
		}
	}

	class EnumerableObjectSource : IObjectSource
	{
		EnumerableSource enumerableSource;
		int idx;

		public EnumerableObjectSource (EnumerableSource enumerableSource, int idx)
		{
			this.enumerableSource = enumerableSource;
			this.idx = idx;

		}

		#region IObjectSource implementation

		public object Value {
			get {
				return enumerableSource.GetElement (idx);
			}
			set {
				throw new InvalidOperationException ("Elements of IEnumerable can not be set");
			}
		}

		#endregion
	}
}

