//
// NRefactoryExpressionEvaluatorVisitor.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2013 Xamarin Inc.
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
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Reflection;

using Mono.Debugging.Client;

using ICSharpCode.NRefactory.CSharp;

namespace Mono.Debugging.Evaluation
{
	public class NRefactoryExpressionEvaluatorVisitor : IAstVisitor<ValueReference>
	{
		readonly Dictionary<string, ValueReference> userVariables;
		readonly EvaluationOptions options;
		readonly EvaluationContext ctx;
		readonly object expectedType;
		readonly string expression;

		public NRefactoryExpressionEvaluatorVisitor (EvaluationContext ctx, string expression, object expectedType, Dictionary<string,ValueReference> userVariables)
		{
			this.ctx = ctx;
			this.expression = expression;
			this.expectedType = expectedType;
			this.userVariables = userVariables;
			this.options = ctx.Options;
		}

		static Exception ParseError (string message, params object[] args)
		{
			return new EvaluatorException (message, args);
		}

		static Exception NotSupported ()
		{
			return new NotSupportedExpressionException ();
		}

		static string ResolveTypeName (AstType type)
		{
			string name = type.ToString ();
			if (name.StartsWith ("global::", StringComparison.Ordinal))
				name = name.Substring ("global::".Length);
			return name;
		}

		static long GetInteger (object val)
		{
			try {
				return Convert.ToInt64 (val);
			} catch {
				throw ParseError ("Expected integer value.");
			}
		}

		long ConvertToInt64 (object val)
		{
			if (val is IntPtr)
				return ((IntPtr) val).ToInt64 ();

			if (ctx.Adapter.IsEnum (ctx, val)) {
				var type = ctx.Adapter.GetType (ctx, "System.Int64");
				var result = ctx.Adapter.Cast (ctx, val, type);

				return (long) ctx.Adapter.TargetObjectToObject (ctx, result);
			}

			return Convert.ToInt64 (val);
		}

		static Type GetCommonOperationType (object v1, object v2)
		{
			if (v1 is double || v2 is double)
				return typeof (double);

			if (v1 is float || v2 is float)
				return typeof (double);

			return typeof (long);
		}

		static Type GetCommonType (object v1, object v2)
		{
			int s1 = Marshal.SizeOf (v1);
			if (IsUnsigned (s1))
				s1 += 8;
			int s2 = Marshal.SizeOf (v2);
			if (IsUnsigned (s2))
				s2 += 8;
			if (s1 > s2)
				return v1.GetType ();
			return v2.GetType ();
		}

		static bool IsUnsigned (object v)
		{
			return (v is byte) || (v is ushort) || (v is uint) || (v is ulong);
		}

		static object EvaluateOperation (BinaryOperatorType op, double v1, double v2)
		{
			switch (op) {
			case BinaryOperatorType.Add: return v1 + v2;
			case BinaryOperatorType.Divide: return v1 / v2;
			case BinaryOperatorType.Multiply: return v1 * v2;
			case BinaryOperatorType.Subtract: return v1 - v2;
			case BinaryOperatorType.GreaterThan: return v1 > v2;
			case BinaryOperatorType.GreaterThanOrEqual: return v1 >= v2;
			case BinaryOperatorType.LessThan: return v1 < v2;
			case BinaryOperatorType.LessThanOrEqual: return v1 <= v2;
			case BinaryOperatorType.Equality: return v1 == v2;
			case BinaryOperatorType.InEquality: return v1 != v2;
			default: throw ParseError ("Invalid binary operator.");
			}
		}

		static object EvaluateOperation (BinaryOperatorType op, long v1, long v2)
		{
			switch (op) {
			case BinaryOperatorType.Add: return v1 + v2;
			case BinaryOperatorType.BitwiseAnd: return v1 & v2;
			case BinaryOperatorType.BitwiseOr: return v1 | v2;
			case BinaryOperatorType.ExclusiveOr: return v1 ^ v2;
			case BinaryOperatorType.Divide: return v1 / v2;
			case BinaryOperatorType.Modulus: return v1 % v2;
			case BinaryOperatorType.Multiply: return v1 * v2;
			case BinaryOperatorType.ShiftLeft: return v1 << (int) v2;
			case BinaryOperatorType.ShiftRight: return v1 >> (int) v2;
			case BinaryOperatorType.Subtract: return v1 - v2;
			case BinaryOperatorType.GreaterThan: return v1 > v2;
			case BinaryOperatorType.GreaterThanOrEqual: return v1 >= v2;
			case BinaryOperatorType.LessThan: return v1 < v2;
			case BinaryOperatorType.LessThanOrEqual: return v1 <= v2;
			case BinaryOperatorType.Equality: return v1 == v2;
			case BinaryOperatorType.InEquality: return v1 != v2;
			default: throw ParseError ("Invalid binary operator.");
			}
		}

		static bool CheckReferenceEquality (EvaluationContext ctx, object v1, object v2)
		{
			if (v1 == null && v2 == null)
				return true;

			if (v1 == null || v2 == null)
				return false;

			object objectType = ctx.Adapter.GetType (ctx, "System.Object");
			object[] argTypes = { objectType, objectType };
			object[] args = { v1, v2 };

			object result = ctx.Adapter.RuntimeInvoke (ctx, objectType, null, "ReferenceEquals", argTypes, args);
			var literal = LiteralValueReference.CreateTargetObjectLiteral (ctx, "result", result);

			return (bool) literal.ObjectValue;
		}

		static bool CheckEquality (EvaluationContext ctx, bool negate, object type1, object type2, object targetVal1, object targetVal2, object val1, object val2)
		{
			if (val1 == null && val2 == null)
				return !negate;

			if (val1 == null || val2 == null)
				return negate;

			string method = negate ? "op_Inequality" : "op_Equality";
			object[] argTypes = { type1, type2 };
			object target, targetType;
			object[] args;

			if (ctx.Adapter.HasMethod (ctx, type1, method, argTypes, BindingFlags.Public | BindingFlags.Static)) {
				args = new [] { targetVal1, targetVal2 };
				targetType = type1;
				target = null;
				negate = false;
			} else if (ctx.Adapter.HasMethod (ctx, type2, method, argTypes, BindingFlags.Public | BindingFlags.Static)) {
				args = new [] { targetVal1, targetVal2 };
				targetType = type2;
				target = null;
				negate = false;
			} else {
				method = ctx.Adapter.IsValueType (type1) ? "Equals" : "ReferenceEquals";
				targetType = ctx.Adapter.GetType (ctx, "System.Object");
				argTypes = new [] { targetType, targetType };
				args = new [] { targetVal1, targetVal2 };
				target = null;
			}

			object result = ctx.Adapter.RuntimeInvoke (ctx, targetType, target, method, argTypes, args);
			var literal = LiteralValueReference.CreateTargetObjectLiteral (ctx, "result", result);
			bool retval = (bool) literal.ObjectValue;

			return negate ? !retval : retval;
		}

		static ValueReference EvaluateOverloadedOperator (EvaluationContext ctx, string expression, BinaryOperatorType op, object type1, object type2, object targetVal1, object targetVal2, object val1, object val2)
		{
			object[] args = new [] { targetVal1, targetVal2 };
			object[] argTypes = { type1, type2 };
			object targetType = null;
			string methodName = null;

			switch (op) {
			case BinaryOperatorType.BitwiseAnd:         methodName = "op_BitwiseAnd"; break;
			case BinaryOperatorType.BitwiseOr:          methodName = "op_BitwiseOr"; break;
			case BinaryOperatorType.ExclusiveOr:        methodName = "op_ExclusiveOr"; break;
			case BinaryOperatorType.GreaterThan:        methodName = "op_GreaterThan"; break;
			case BinaryOperatorType.GreaterThanOrEqual: methodName = "op_GreaterThanOrEqual"; break;
			case BinaryOperatorType.Equality:           methodName = "op_Equality"; break;
			case BinaryOperatorType.InEquality:         methodName = "op_Inequality"; break;
			case BinaryOperatorType.LessThan:           methodName = "op_LessThan"; break;
			case BinaryOperatorType.LessThanOrEqual:    methodName = "op_LessThanOrEqual"; break;
			case BinaryOperatorType.Add:                methodName = "op_Addition"; break;
			case BinaryOperatorType.Subtract:           methodName = "op_Subtraction"; break;
			case BinaryOperatorType.Multiply:           methodName = "op_Multiply"; break;
			case BinaryOperatorType.Divide:             methodName = "op_Division"; break;
			case BinaryOperatorType.Modulus:            methodName = "op_Modulus"; break;
			case BinaryOperatorType.ShiftLeft:          methodName = "op_LeftShift"; break;
			case BinaryOperatorType.ShiftRight:         methodName = "op_RightShift"; break;
			}

			if (methodName == null)
				throw ParseError ("Invalid operands in binary operator.");

			if (ctx.Adapter.HasMethod (ctx, type1, methodName, argTypes, BindingFlags.Public | BindingFlags.Static)) {
				targetType = type1;
			} else if (ctx.Adapter.HasMethod (ctx, type2, methodName, argTypes, BindingFlags.Public | BindingFlags.Static)) {
				targetType = type2;
			} else {
				throw ParseError ("Invalid operands in binary operator.");
			}

			object result = ctx.Adapter.RuntimeInvoke (ctx, targetType, null, methodName, argTypes, args);

			return LiteralValueReference.CreateTargetObjectLiteral (ctx, expression, result);
		}

		ValueReference EvaluateBinaryOperatorExpression (BinaryOperatorType op, ValueReference left, Expression rightExp)
		{
			if (op == BinaryOperatorType.ConditionalAnd) {
				var val = left.ObjectValue;
				if (!(val is bool))
					throw ParseError ("Left operand of logical And must be a boolean.");

				if (!(bool) val)
					return LiteralValueReference.CreateObjectLiteral (ctx, expression, false);

				var vr = rightExp.AcceptVisitor<ValueReference> (this);
				if (vr == null || ctx.Adapter.GetTypeName (ctx, vr.Type) != "System.Boolean")
					throw ParseError ("Right operand of logical And must be a boolean.");

				return vr;
			}

			if (op == BinaryOperatorType.ConditionalOr) {
				var val = left.ObjectValue;
				if (!(val is bool))
					throw ParseError ("Left operand of logical Or must be a boolean.");

				if ((bool) val)
					return LiteralValueReference.CreateObjectLiteral (ctx, expression, true);

				var vr = rightExp.AcceptVisitor<ValueReference> (this);
				if (vr == null || ctx.Adapter.GetTypeName (ctx, vr.Type) != "System.Boolean")
					throw ParseError ("Right operand of logical Or must be a boolean.");

				return vr;
			}

			var right = rightExp.AcceptVisitor<ValueReference> (this);
			var targetVal1 = left.Value;
			var targetVal2 = right.Value;
			var type1 = ctx.Adapter.GetValueType (ctx, targetVal1);
			var type2 = ctx.Adapter.GetValueType (ctx, targetVal2);
			var val1 = left.ObjectValue;
			var val2 = right.ObjectValue;
			object res = null;

			if (ctx.Adapter.IsNullableType (ctx, type1) && ctx.Adapter.NullableHasValue (ctx, type1, val1)) {
				if (val2 == null) {
					if (op == BinaryOperatorType.Equality)
						return LiteralValueReference.CreateObjectLiteral (ctx, expression, false);
					if (op == BinaryOperatorType.InEquality)
						return LiteralValueReference.CreateObjectLiteral (ctx, expression, true);
				}

				ValueReference nullable = ctx.Adapter.NullableGetValue (ctx, type1, val1);
				targetVal1 = nullable.Value;
				val1 = nullable.ObjectValue;
				type1 = nullable.Type;
			}

			if (ctx.Adapter.IsNullableType (ctx, type2) && ctx.Adapter.NullableHasValue (ctx, type2, val2)) {
				if (val1 == null) {
					if (op == BinaryOperatorType.Equality)
						return LiteralValueReference.CreateObjectLiteral (ctx, expression, false);
					if (op == BinaryOperatorType.InEquality)
						return LiteralValueReference.CreateObjectLiteral (ctx, expression, true);
				}

				ValueReference nullable = ctx.Adapter.NullableGetValue (ctx, type2, val2);
				targetVal2 = nullable.Value;
				val2 = nullable.ObjectValue;
				type2 = nullable.Type;
			}

			if (val1 is string || val2 is string) {
				switch (op) {
				case BinaryOperatorType.Add:
					if (val1 != null && val2 != null) {
						if (!(val1 is string))
							val1 = ctx.Adapter.CallToString (ctx, targetVal1);

						if (!(val2 is string))
							val2 = ctx.Adapter.CallToString (ctx, targetVal2);

						res = (string) val1 + (string) val2;
					} else if (val1 != null) {
						res = val1.ToString ();
					} else if (val2 != null) {
						res = val2.ToString ();
					}

					return LiteralValueReference.CreateObjectLiteral (ctx, expression, res);
				case BinaryOperatorType.Equality:
					if ((val1 == null || val1 is string) && (val2 == null || val2 is string))
						return LiteralValueReference.CreateObjectLiteral (ctx, expression, ((string) val1) == ((string) val2));
					break;
				case BinaryOperatorType.InEquality:
					if ((val1 == null || val1 is string) && (val2 == null || val2 is string))
						return LiteralValueReference.CreateObjectLiteral (ctx, expression, ((string) val1) != ((string) val2));
					break;
				}
			}

			if (val1 == null || (!ctx.Adapter.IsPrimitive (ctx, targetVal1) && !ctx.Adapter.IsEnum (ctx, targetVal1))) {
				switch (op) {
				case BinaryOperatorType.Equality:
					return LiteralValueReference.CreateObjectLiteral (ctx, expression, CheckEquality (ctx, false, type1, type2, targetVal1, targetVal2, val1, val2));
				case BinaryOperatorType.InEquality:
					return LiteralValueReference.CreateObjectLiteral (ctx, expression, CheckEquality (ctx, true, type1, type2, targetVal1, targetVal2, val1, val2));
				default:
					if (val1 != null && val2 != null)
						return EvaluateOverloadedOperator (ctx, expression, op, type1, type2, targetVal1, targetVal2, val1, val2);
					break;
				}
			}

			if ((val1 is bool) && (val2 is bool)) {
				switch (op) {
				case BinaryOperatorType.ExclusiveOr:
					return LiteralValueReference.CreateObjectLiteral (ctx, expression, (bool) val1 ^ (bool) val2);
				case BinaryOperatorType.Equality:
					return LiteralValueReference.CreateObjectLiteral (ctx, expression, (bool) val1 == (bool) val2);
				case BinaryOperatorType.InEquality:
					return LiteralValueReference.CreateObjectLiteral (ctx, expression, (bool) val1 != (bool) val2);
				}
			}

			if (val1 == null || val2 == null || (val1 is bool) || (val2 is bool))
				throw ParseError ("Invalid operands in binary operator.");

			var commonType = GetCommonOperationType (val1, val2);

			if (commonType == typeof (double)) {
				double v1, v2;

				try {
					v1 = Convert.ToDouble (val1);
					v2 = Convert.ToDouble (val2);
				} catch {
					throw ParseError ("Invalid operands in binary operator.");
				}

				res = EvaluateOperation (op, v1, v2);
			} else {
				var v1 = ConvertToInt64 (val1);
				var v2 = ConvertToInt64 (val2);

				res = EvaluateOperation (op, v1, v2);
			}

			if (!(res is bool) && !(res is string)) {
				if (ctx.Adapter.IsEnum (ctx, targetVal1)) {
					object tval = ctx.Adapter.Cast (ctx, ctx.Adapter.CreateValue (ctx, res), ctx.Adapter.GetValueType (ctx, targetVal1));
					return LiteralValueReference.CreateTargetObjectLiteral (ctx, expression, tval);
				}

				if (ctx.Adapter.IsEnum (ctx, targetVal2)) {
					object tval = ctx.Adapter.Cast (ctx, ctx.Adapter.CreateValue (ctx, res), ctx.Adapter.GetValueType (ctx, targetVal2));
					return LiteralValueReference.CreateTargetObjectLiteral (ctx, expression, tval);
				}

				var targetType = GetCommonType (val1, val2);

				if (targetType != typeof (IntPtr))
					res = Convert.ChangeType (res, targetType);
				else
					res = new IntPtr ((long) res);
			}

			return LiteralValueReference.CreateObjectLiteral (ctx, expression, res);
		}

		static string ResolveType (EvaluationContext ctx, TypeReferenceExpression mre, List<object> args)
		{
			var memberType = mre.Type as MemberType;

			if (memberType != null) {
				var name = memberType.MemberName;

				if (memberType.TypeArguments.Count > 0) {
					name += "`" + memberType.TypeArguments.Count;

					foreach (var arg in memberType.TypeArguments) {
						var resolved = arg.Resolve (ctx);

						if (resolved == null)
							return null;

						args.Add (resolved);
					}
				}

				return name;
			}

			return mre.ToString ();
		}

		static string ResolveType (EvaluationContext ctx, MemberReferenceExpression mre, List<object> args)
		{
			string parent, name;

			if (mre.Target is MemberReferenceExpression) {
				parent = ResolveType (ctx, (MemberReferenceExpression) mre.Target, args);
			} else if (mre.Target is TypeReferenceExpression) {
				parent = ResolveType (ctx, (TypeReferenceExpression) mre.Target, args);
			} else if (mre.Target is IdentifierExpression) {
				parent = ((IdentifierExpression) mre.Target).Identifier;
			} else {
				return null;
			}

			name = parent + "." + mre.MemberName;
			if (mre.TypeArguments.Count > 0) {
				name += "`" + mre.TypeArguments.Count;

				foreach (var arg in mre.TypeArguments) {
					var resolved = arg.Resolve (ctx);

					if (resolved == null)
						return null;

					args.Add (resolved);
				}
			}

			return name;
		}

		static object ResolveType (EvaluationContext ctx, MemberReferenceExpression mre)
		{
			var args = new List<object> ();
			var name = ResolveType (ctx, mre, args);

			if (name == null)
				return null;

			if (args.Count > 0)
				return ctx.Adapter.GetType (ctx, name, args.ToArray ());

			return ctx.Adapter.GetType (ctx, name);
		}

		static ValueReference ResolveTypeValueReference (EvaluationContext ctx, MemberReferenceExpression mre)
		{
			object resolved = ResolveType (ctx, mre);

			if (resolved != null) {
				ctx.Adapter.ForceLoadType (ctx, resolved);

				return new TypeValueReference (ctx, resolved);
			}

			throw ParseError ("Could not resolve type: {0}", mre);
		}

		static ValueReference ResolveTypeValueReference (EvaluationContext ctx, AstType type)
		{
			object resolved = type.Resolve (ctx);

			if (resolved != null) {
				ctx.Adapter.ForceLoadType (ctx, resolved);

				return new TypeValueReference (ctx, resolved);
			}

			throw ParseError ("Could not resolve type: {0}", ResolveTypeName (type));
		}

		#region IAstVisitor implementation

		public ValueReference VisitAnonymousMethodExpression (AnonymousMethodExpression anonymousMethodExpression)
		{
			throw NotSupported ();
		}

		public ValueReference VisitUndocumentedExpression (UndocumentedExpression undocumentedExpression)
		{
			throw NotSupported ();
		}

		public ValueReference VisitArrayCreateExpression (ArrayCreateExpression arrayCreateExpression)
		{
			throw NotSupported ();
		}

		public ValueReference VisitArrayInitializerExpression (ArrayInitializerExpression arrayInitializerExpression)
		{
			throw NotSupported ();
		}

		public ValueReference VisitAsExpression (AsExpression asExpression)
		{
			var type = asExpression.Type.AcceptVisitor<ValueReference> (this) as TypeValueReference;
			if (type == null)
				throw ParseError ("Invalid type in cast.");

			var val = asExpression.Expression.AcceptVisitor<ValueReference> (this);
			var result = ctx.Adapter.TryCast (ctx, val.Value, type.Type);

			if (result == null)
				return new NullValueReference (ctx, type.Type);

			return LiteralValueReference.CreateTargetObjectLiteral (ctx, expression, result);
		}

		public ValueReference VisitAssignmentExpression (AssignmentExpression assignmentExpression)
		{
			if (!options.AllowMethodEvaluation)
				throw NotSupported ();

			var left = assignmentExpression.Left.AcceptVisitor<ValueReference> (this);

			if (assignmentExpression.Operator == AssignmentOperatorType.Assign) {
				var right = assignmentExpression.Right.AcceptVisitor<ValueReference> (this);
				left.Value = right.Value;
			} else {
				BinaryOperatorType op;

				switch (assignmentExpression.Operator) {
				case AssignmentOperatorType.Add:         op = BinaryOperatorType.Add; break;
				case AssignmentOperatorType.Subtract:    op = BinaryOperatorType.Subtract; break;
				case AssignmentOperatorType.Multiply:    op = BinaryOperatorType.Multiply; break;
				case AssignmentOperatorType.Divide:      op = BinaryOperatorType.Divide; break;
				case AssignmentOperatorType.Modulus:     op = BinaryOperatorType.Modulus; break;
				case AssignmentOperatorType.ShiftLeft:   op = BinaryOperatorType.ShiftLeft; break;
				case AssignmentOperatorType.ShiftRight:  op = BinaryOperatorType.ShiftRight; break;
				case AssignmentOperatorType.BitwiseAnd:  op = BinaryOperatorType.BitwiseAnd; break;
				case AssignmentOperatorType.BitwiseOr:   op = BinaryOperatorType.BitwiseOr; break;
				case AssignmentOperatorType.ExclusiveOr: op = BinaryOperatorType.ExclusiveOr; break;
				default: throw ParseError ("Invalid operator in assignment.");
				}

				var result = EvaluateBinaryOperatorExpression (op, left, assignmentExpression.Right);
				left.Value = result.Value;
			}

			return left;
		}

		public ValueReference VisitBaseReferenceExpression (BaseReferenceExpression baseReferenceExpression)
		{
			var self = ctx.Adapter.GetThisReference (ctx);

			if (self != null)
				return LiteralValueReference.CreateTargetBaseObjectLiteral (ctx, expression, self.Value);

			throw ParseError ("'base' reference not available in static methods.");
		}

		public ValueReference VisitBinaryOperatorExpression (BinaryOperatorExpression binaryOperatorExpression)
		{
			var left = binaryOperatorExpression.Left.AcceptVisitor<ValueReference> (this);

			return EvaluateBinaryOperatorExpression (binaryOperatorExpression.Operator, left, binaryOperatorExpression.Right);
		}

		public ValueReference VisitCastExpression (CastExpression castExpression)
		{
			var type = castExpression.Type.AcceptVisitor<ValueReference> (this) as TypeValueReference;
			if (type == null)
				throw ParseError ("Invalid type in cast.");

			var val = castExpression.Expression.AcceptVisitor<ValueReference> (this);
			object result = ctx.Adapter.TryCast (ctx, val.Value, type.Type);
			if (result == null)
				throw ParseError ("Invalid cast.");

			return LiteralValueReference.CreateTargetObjectLiteral (ctx, expression, result);
		}

		public ValueReference VisitCheckedExpression (CheckedExpression checkedExpression)
		{
			throw NotSupported ();
		}

		public ValueReference VisitConditionalExpression (ConditionalExpression conditionalExpression)
		{
			ValueReference val = conditionalExpression.Condition.AcceptVisitor<ValueReference> (this);
			if (val is TypeValueReference)
				throw NotSupported ();

			if ((bool) val.ObjectValue)
				return conditionalExpression.TrueExpression.AcceptVisitor<ValueReference> (this);

			return conditionalExpression.FalseExpression.AcceptVisitor<ValueReference> (this);
		}

		public ValueReference VisitDefaultValueExpression (DefaultValueExpression defaultValueExpression)
		{
			throw NotSupported ();
		}

		public ValueReference VisitDirectionExpression (DirectionExpression directionExpression)
		{
			throw NotSupported ();
		}

		public ValueReference VisitIdentifierExpression (IdentifierExpression identifierExpression)
		{
			var name = identifierExpression.Identifier;

			if (name == "__EXCEPTION_OBJECT__")
				return ctx.Adapter.GetCurrentException (ctx);

			// Look in user defined variables

			ValueReference userVar;
			if (userVariables.TryGetValue (name, out userVar))
				return userVar;

			// Look in variables

			ValueReference var = ctx.Adapter.GetLocalVariable (ctx, name);
			if (var != null)
				return var;

			// Look in parameters

			var = ctx.Adapter.GetParameter (ctx, name);
			if (var != null)
				return var;

			// Look in instance fields and properties

			ValueReference self = ctx.Adapter.GetThisReference (ctx);

			if (self != null) {
				// check for fields and properties in this instance

				// first try if current type has field or property
				var = ctx.Adapter.GetMember (ctx, self, ctx.Adapter.GetEnclosingType (ctx), self.Value, name);
				if (var != null)
					return var;
				
				var = ctx.Adapter.GetMember (ctx, self, self.Type, self.Value, name);
				if (var != null)
					return var;
			}

			// Look in static fields & properties of the enclosing type and all parent types

			object type = ctx.Adapter.GetEnclosingType (ctx);
			object vtype = type;

			while (vtype != null) {
				// check for static fields and properties
				var = ctx.Adapter.GetMember (ctx, null, vtype, null, name);
				if (var != null)
					return var;

				vtype = ctx.Adapter.GetParentType (ctx, vtype);
			}

			// Look in types

			vtype = ctx.Adapter.GetType (ctx, name);
			if (vtype != null)
				return new TypeValueReference (ctx, vtype);

			if (self == null && ctx.Adapter.HasMember (ctx, type, name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)) {
				string message = string.Format ("An object reference is required for the non-static field, method, or property '{0}.{1}'.",
				                                ctx.Adapter.GetDisplayTypeName (ctx, type), name);
				throw ParseError (message);
			}

			throw ParseError ("Unknown identifier: {0}", name);
		}

		public ValueReference VisitIndexerExpression (IndexerExpression indexerExpression)
		{
			int n = 0;

			var target = indexerExpression.Target.AcceptVisitor<ValueReference> (this);
			if (target is TypeValueReference)
				throw NotSupported ();

			if (ctx.Adapter.IsArray (ctx, target.Value)) {
				int[] indexes = new int [indexerExpression.Arguments.Count];

				foreach (var arg in indexerExpression.Arguments) {
					var index = arg.AcceptVisitor<ValueReference> (this);
					indexes[n++] = (int) Convert.ChangeType (index.ObjectValue, typeof (int));
				}

				return new ArrayValueReference (ctx, target.Value, indexes);
			}

			object[] args = new object [indexerExpression.Arguments.Count];
			foreach (var arg in indexerExpression.Arguments)
				args[n++] = arg.AcceptVisitor<ValueReference> (this).Value;

			var indexer = ctx.Adapter.GetIndexerReference (ctx, target.Value, args);
			if (indexer == null)
				throw NotSupported ();

			return indexer;
		}

		string ResolveMethodName (MemberReferenceExpression method, out object[] typeArgs)
		{
			if (method.TypeArguments.Count > 0) {
				var args = new List<object> ();

				foreach (var arg in method.TypeArguments) {
					var type = arg.AcceptVisitor (this);
					args.Add (type.Type);
				}

				typeArgs = args.ToArray ();
			} else {
				typeArgs = null;
			}

			return method.MemberName;
		}

		string ResolveMethodName (IdentifierExpression method, out object[] typeArgs)
		{
			if (method.TypeArguments.Count > 0) {
				var args = new List<object> ();

				foreach (var arg in method.TypeArguments) {
					var type = arg.AcceptVisitor (this);
					args.Add (type.Type);
				}

				typeArgs = args.ToArray ();
			} else {
				typeArgs = null;
			}

			return method.Identifier;
		}

		public ValueReference VisitInvocationExpression (InvocationExpression invocationExpression)
		{
			if (!options.AllowMethodEvaluation)
				throw NotSupported ();

			bool invokeBaseMethod = false;
			ValueReference target = null;
			string methodName;

			var types = new object [invocationExpression.Arguments.Count];
			var args = new object [invocationExpression.Arguments.Count];
			object[] typeArgs = null;
			int n = 0;

			foreach (var arg in invocationExpression.Arguments) {
				var vref = arg.AcceptVisitor<ValueReference> (this);
				args[n] = vref.Value;
				types[n] = ctx.Adapter.GetValueType (ctx, args[n]);
				n++;
			}
			object vtype = null;
			if (invocationExpression.Target is MemberReferenceExpression) {
				var field = (MemberReferenceExpression) invocationExpression.Target;
				target = field.Target.AcceptVisitor<ValueReference> (this);
				if (field.Target is BaseReferenceExpression)
					invokeBaseMethod = true;
				methodName = ResolveMethodName (field, out typeArgs);
			} else if (invocationExpression.Target is IdentifierExpression) {
				var method = (IdentifierExpression) invocationExpression.Target;
				var vref = ctx.Adapter.GetThisReference (ctx);

				methodName = ResolveMethodName (method, out typeArgs);

				if (vref != null && ctx.Adapter.HasMethod (ctx, vref.Type, methodName, typeArgs, null, BindingFlags.Instance)) {
					vtype = ctx.Adapter.GetEnclosingType (ctx);
					// There is an instance method for 'this', although it may not have an exact signature match. Check it now.
					if (ctx.Adapter.HasMethod (ctx, vref.Type, methodName, typeArgs, types, BindingFlags.Instance)) {
						target = vref;
					} else {
						// There isn't an instance method with exact signature match.
						// If there isn't a static method, then use the instance method,
						// which will report the signature match error when invoked
						if (!ctx.Adapter.HasMethod (ctx, vtype, methodName, typeArgs, types, BindingFlags.Static))
							target = vref;
					}
				} else {
					if (ctx.Adapter.HasMethod (ctx, ctx.Adapter.GetEnclosingType (ctx), methodName, types, BindingFlags.Instance))
						throw new EvaluatorException ("Cannot invoke an instance method from a static method.");
					target = null;
				}
			} else {
				throw NotSupported ();
			}

			if (vtype == null)
				vtype = target != null ? target.Type : ctx.Adapter.GetEnclosingType (ctx);
			object vtarget = (target is TypeValueReference) || target == null ? null : target.Value;

			if (invokeBaseMethod) {
				vtype = ctx.Adapter.GetBaseType (ctx, vtype);
			} else if (target != null && !ctx.Adapter.HasMethod (ctx, vtype, methodName, typeArgs, types, BindingFlags.Instance | BindingFlags.Static)) {
				// Look for LINQ extension methods...
				var linq = ctx.Adapter.GetType (ctx, "System.Linq.Enumerable");
				if (linq != null) {
					object[] xtypeArgs = typeArgs;

					if (xtypeArgs == null) {
						// try to infer the generic type arguments from the type of the object...
						object xtype = vtype;
						while (xtype != null && !ctx.Adapter.IsGenericType (ctx, xtype))
							xtype = ctx.Adapter.GetBaseType (ctx, xtype);

						if (xtype != null)
							xtypeArgs = ctx.Adapter.GetTypeArgs (ctx, xtype);
					}

					if (xtypeArgs != null) {
						var xtypes = new object[types.Length + 1];
						Array.Copy (types, 0, xtypes, 1, types.Length);
						xtypes[0] = vtype;

						var xargs = new object[args.Length + 1];
						Array.Copy (args, 0, xargs, 1, args.Length);
						xargs[0] = vtarget;

						if (ctx.Adapter.HasMethod (ctx, linq, methodName, xtypeArgs, xtypes, BindingFlags.Static)) {
							vtarget = null;
							vtype = linq;

							typeArgs = xtypeArgs;
							types = xtypes;
							args = xargs;
						}
					}
				}
			}

			object result = ctx.Adapter.RuntimeInvoke (ctx, vtype, vtarget, methodName, typeArgs, types, args);
			if (result != null)
				return LiteralValueReference.CreateTargetObjectLiteral (ctx, expression, result);

			return LiteralValueReference.CreateVoidReturnLiteral (ctx, expression);
		}

		public ValueReference VisitIsExpression (IsExpression isExpression)
		{
			// FIXME: we could probably implement this one...
			throw NotSupported ();
		}

		public ValueReference VisitLambdaExpression (LambdaExpression lambdaExpression)
		{
			throw NotSupported ();
		}

		public ValueReference VisitMemberReferenceExpression (MemberReferenceExpression memberReferenceExpression)
		{
			if (memberReferenceExpression.TypeArguments.Count > 0)
				return ResolveTypeValueReference (ctx, memberReferenceExpression);

			var target = memberReferenceExpression.Target.AcceptVisitor<ValueReference> (this);
			var member = target.GetChild (memberReferenceExpression.MemberName, ctx.Options);

			if (member == null) {
				if (!(target is TypeValueReference)) {
					if (ctx.Adapter.IsNull (ctx, target.Value))
						throw new EvaluatorException ("{0} is null", target.Name);
				}

				throw ParseError ("Unknown member: {0}", memberReferenceExpression.MemberName);
			}

			return member;
		}

		public ValueReference VisitNamedArgumentExpression (NamedArgumentExpression namedArgumentExpression)
		{
			throw NotSupported ();
		}

		public ValueReference VisitNamedExpression (NamedExpression namedExpression)
		{
			throw NotSupported ();
		}

		public ValueReference VisitNullReferenceExpression (NullReferenceExpression nullReferenceExpression)
		{
			return new NullValueReference (ctx, ctx.Adapter.GetType (ctx, "System.Object"));
		}

		public ValueReference VisitObjectCreateExpression (ObjectCreateExpression objectCreateExpression)
		{
			var type = objectCreateExpression.Type.AcceptVisitor<ValueReference> (this) as TypeValueReference;
			var args = new List<object> ();

			foreach (var arg in objectCreateExpression.Arguments) {
				var val = arg.AcceptVisitor<ValueReference> (this);
				args.Add (val != null ? val.Value : null);
			}

			return LiteralValueReference.CreateTargetObjectLiteral (ctx, expression, ctx.Adapter.CreateValue (ctx, type.Type, args.ToArray ()));
		}

		public ValueReference VisitAnonymousTypeCreateExpression (AnonymousTypeCreateExpression anonymousTypeCreateExpression)
		{
			throw NotSupported ();
		}

		public ValueReference VisitParenthesizedExpression (ParenthesizedExpression parenthesizedExpression)
		{
			return parenthesizedExpression.Expression.AcceptVisitor<ValueReference> (this);
		}

		public ValueReference VisitPointerReferenceExpression (PointerReferenceExpression pointerReferenceExpression)
		{
			throw NotSupported ();
		}

		public ValueReference VisitPrimitiveExpression (PrimitiveExpression primitiveExpression)
		{
			if (primitiveExpression.Value != null)
				return LiteralValueReference.CreateObjectLiteral (ctx, expression, primitiveExpression.Value);

			if (expectedType != null)
				return new NullValueReference (ctx, expectedType);

			return new NullValueReference (ctx, ctx.Adapter.GetType (ctx, "System.Object"));
		}

		public ValueReference VisitSizeOfExpression (SizeOfExpression sizeOfExpression)
		{
			throw NotSupported ();
		}

		public ValueReference VisitStackAllocExpression (StackAllocExpression stackAllocExpression)
		{
			throw NotSupported ();
		}

		public ValueReference VisitThisReferenceExpression (ThisReferenceExpression thisReferenceExpression)
		{
			var self = ctx.Adapter.GetThisReference (ctx);

			if (self == null)
				throw ParseError ("'this' reference not available in the current evaluation context.");

			return self;
		}

		public ValueReference VisitTypeOfExpression (TypeOfExpression typeOfExpression)
		{
			var name = ResolveTypeName (typeOfExpression.Type);
			var type = typeOfExpression.Type.Resolve (ctx);

			if (type == null)
				throw ParseError ("Could not load type: {0}", name);

			object result = ctx.Adapter.CreateTypeObject (ctx, type);
			if (result == null)
				throw NotSupported ();

			return LiteralValueReference.CreateTargetObjectLiteral (ctx, name, result);
		}

		public ValueReference VisitTypeReferenceExpression (TypeReferenceExpression typeReferenceExpression)
		{
			var type = typeReferenceExpression.Type.Resolve (ctx);

			if (type != null) {
				ctx.Adapter.ForceLoadType (ctx, type);

				return new TypeValueReference (ctx, type);
			}

			var name = ResolveTypeName (typeReferenceExpression.Type);

			// Assume it is a namespace.
			return new NamespaceValueReference (ctx, name);
		}

		public ValueReference VisitUnaryOperatorExpression (UnaryOperatorExpression unaryOperatorExpression)
		{
			var vref = unaryOperatorExpression.Expression.AcceptVisitor<ValueReference> (this);
			var val = vref.ObjectValue;
			object newVal;
			long num;

			switch (unaryOperatorExpression.Operator) {
			case UnaryOperatorType.BitNot:
				num = ~GetInteger (val);
				val = Convert.ChangeType (num, val.GetType ());
				break;
			case UnaryOperatorType.Minus:
				if (val is decimal) {
					val = -(decimal)val;
				} else {
					num = -GetInteger (val);
					val = Convert.ChangeType (num, val.GetType ());
				}
				break;
			case UnaryOperatorType.Not:
				if (!(val is bool))
					throw ParseError ("Expected boolean type in Not operator.");

				val = !(bool) val;
				break;
			case UnaryOperatorType.PostDecrement:
				num = GetInteger (val) - 1;
				newVal = Convert.ChangeType (num, val.GetType ());
				vref.Value = ctx.Adapter.CreateValue (ctx, newVal);
				break;
			case UnaryOperatorType.Decrement:
				num = GetInteger (val) - 1;
				val = Convert.ChangeType (num, val.GetType ());
				vref.Value = ctx.Adapter.CreateValue (ctx, val);
				break;
			case UnaryOperatorType.PostIncrement:
				num = GetInteger (val) + 1;
				newVal = Convert.ChangeType (num, val.GetType ());
				vref.Value = ctx.Adapter.CreateValue (ctx, newVal);
				break;
			case UnaryOperatorType.Increment:
				num = GetInteger (val) + 1;
				val = Convert.ChangeType (num, val.GetType ());
				vref.Value = ctx.Adapter.CreateValue (ctx, val);
				break;
			case UnaryOperatorType.Plus:
				break;
			default:
				throw NotSupported ();
			}

			return LiteralValueReference.CreateObjectLiteral (ctx, expression, val);
		}

		public ValueReference VisitUncheckedExpression (UncheckedExpression uncheckedExpression)
		{
			throw NotSupported ();
		}

		public ValueReference VisitEmptyExpression (EmptyExpression emptyExpression)
		{
			throw NotSupported ();
		}

		public ValueReference VisitQueryExpression (QueryExpression queryExpression)
		{
			throw NotSupported ();
		}

		public ValueReference VisitQueryContinuationClause (QueryContinuationClause queryContinuationClause)
		{
			throw NotSupported ();
		}

		public ValueReference VisitQueryFromClause (QueryFromClause queryFromClause)
		{
			throw NotSupported ();
		}

		public ValueReference VisitQueryLetClause (QueryLetClause queryLetClause)
		{
			throw NotSupported ();
		}

		public ValueReference VisitQueryWhereClause (QueryWhereClause queryWhereClause)
		{
			throw NotSupported ();
		}

		public ValueReference VisitQueryJoinClause (QueryJoinClause queryJoinClause)
		{
			throw NotSupported ();
		}

		public ValueReference VisitQueryOrderClause (QueryOrderClause queryOrderClause)
		{
			throw NotSupported ();
		}

		public ValueReference VisitQueryOrdering (QueryOrdering queryOrdering)
		{
			throw NotSupported ();
		}

		public ValueReference VisitQuerySelectClause (QuerySelectClause querySelectClause)
		{
			throw NotSupported ();
		}

		public ValueReference VisitQueryGroupClause (QueryGroupClause queryGroupClause)
		{
			throw NotSupported ();
		}

		public ValueReference VisitAttribute (ICSharpCode.NRefactory.CSharp.Attribute attribute)
		{
			throw NotSupported ();
		}

		public ValueReference VisitAttributeSection (AttributeSection attributeSection)
		{
			throw NotSupported ();
		}

		public ValueReference VisitDelegateDeclaration (DelegateDeclaration delegateDeclaration)
		{
			throw NotSupported ();
		}

		public ValueReference VisitNamespaceDeclaration (NamespaceDeclaration namespaceDeclaration)
		{
			throw NotSupported ();
		}

		public ValueReference VisitTypeDeclaration (TypeDeclaration typeDeclaration)
		{
			throw NotSupported ();
		}

		public ValueReference VisitUsingAliasDeclaration (UsingAliasDeclaration usingAliasDeclaration)
		{
			throw NotSupported ();
		}

		public ValueReference VisitUsingDeclaration (UsingDeclaration usingDeclaration)
		{
			throw NotSupported ();
		}

		public ValueReference VisitExternAliasDeclaration (ExternAliasDeclaration externAliasDeclaration)
		{
			throw NotSupported ();
		}

		public ValueReference VisitBlockStatement (BlockStatement blockStatement)
		{
			throw NotSupported ();
		}

		public ValueReference VisitBreakStatement (BreakStatement breakStatement)
		{
			throw NotSupported ();
		}

		public ValueReference VisitCheckedStatement (CheckedStatement checkedStatement)
		{
			throw NotSupported ();
		}

		public ValueReference VisitContinueStatement (ContinueStatement continueStatement)
		{
			throw NotSupported ();
		}

		public ValueReference VisitDoWhileStatement (DoWhileStatement doWhileStatement)
		{
			throw NotSupported ();
		}

		public ValueReference VisitEmptyStatement (EmptyStatement emptyStatement)
		{
			throw NotSupported ();
		}

		public ValueReference VisitExpressionStatement (ExpressionStatement expressionStatement)
		{
			throw NotSupported ();
		}

		public ValueReference VisitFixedStatement (FixedStatement fixedStatement)
		{
			throw NotSupported ();
		}

		public ValueReference VisitForeachStatement (ForeachStatement foreachStatement)
		{
			throw NotSupported ();
		}

		public ValueReference VisitForStatement (ForStatement forStatement)
		{
			throw NotSupported ();
		}

		public ValueReference VisitGotoCaseStatement (GotoCaseStatement gotoCaseStatement)
		{
			throw NotSupported ();
		}

		public ValueReference VisitGotoDefaultStatement (GotoDefaultStatement gotoDefaultStatement)
		{
			throw NotSupported ();
		}

		public ValueReference VisitGotoStatement (GotoStatement gotoStatement)
		{
			throw NotSupported ();
		}

		public ValueReference VisitIfElseStatement (IfElseStatement ifElseStatement)
		{
			throw NotSupported ();
		}

		public ValueReference VisitLabelStatement (LabelStatement labelStatement)
		{
			throw NotSupported ();
		}

		public ValueReference VisitLockStatement (LockStatement lockStatement)
		{
			throw NotSupported ();
		}

		public ValueReference VisitReturnStatement (ReturnStatement returnStatement)
		{
			throw NotSupported ();
		}

		public ValueReference VisitSwitchStatement (SwitchStatement switchStatement)
		{
			throw NotSupported ();
		}

		public ValueReference VisitSwitchSection (SwitchSection switchSection)
		{
			throw NotSupported ();
		}

		public ValueReference VisitCaseLabel (CaseLabel caseLabel)
		{
			throw NotSupported ();
		}

		public ValueReference VisitThrowStatement (ThrowStatement throwStatement)
		{
			throw NotSupported ();
		}

		public ValueReference VisitTryCatchStatement (TryCatchStatement tryCatchStatement)
		{
			throw NotSupported ();
		}

		public ValueReference VisitCatchClause (CatchClause catchClause)
		{
			throw NotSupported ();
		}

		public ValueReference VisitUncheckedStatement (UncheckedStatement uncheckedStatement)
		{
			throw NotSupported ();
		}

		public ValueReference VisitUnsafeStatement (UnsafeStatement unsafeStatement)
		{
			throw NotSupported ();
		}

		public ValueReference VisitUsingStatement (UsingStatement usingStatement)
		{
			throw NotSupported ();
		}

		public ValueReference VisitVariableDeclarationStatement (VariableDeclarationStatement variableDeclarationStatement)
		{
			throw NotSupported ();
		}

		public ValueReference VisitWhileStatement (WhileStatement whileStatement)
		{
			throw NotSupported ();
		}

		public ValueReference VisitYieldBreakStatement (YieldBreakStatement yieldBreakStatement)
		{
			throw NotSupported ();
		}

		public ValueReference VisitYieldReturnStatement (YieldReturnStatement yieldReturnStatement)
		{
			throw NotSupported ();
		}

		public ValueReference VisitAccessor (Accessor accessor)
		{
			throw NotSupported ();
		}

		public ValueReference VisitConstructorDeclaration (ConstructorDeclaration constructorDeclaration)
		{
			throw NotSupported ();
		}

		public ValueReference VisitConstructorInitializer (ConstructorInitializer constructorInitializer)
		{
			throw NotSupported ();
		}

		public ValueReference VisitDestructorDeclaration (DestructorDeclaration destructorDeclaration)
		{
			throw NotSupported ();
		}

		public ValueReference VisitEnumMemberDeclaration (EnumMemberDeclaration enumMemberDeclaration)
		{
			throw NotSupported ();
		}

		public ValueReference VisitEventDeclaration (EventDeclaration eventDeclaration)
		{
			throw NotSupported ();
		}

		public ValueReference VisitCustomEventDeclaration (CustomEventDeclaration customEventDeclaration)
		{
			throw NotSupported ();
		}

		public ValueReference VisitFieldDeclaration (FieldDeclaration fieldDeclaration)
		{
			throw NotSupported ();
		}

		public ValueReference VisitIndexerDeclaration (IndexerDeclaration indexerDeclaration)
		{
			throw NotSupported ();
		}

		public ValueReference VisitMethodDeclaration (MethodDeclaration methodDeclaration)
		{
			throw NotSupported ();
		}

		public ValueReference VisitOperatorDeclaration (OperatorDeclaration operatorDeclaration)
		{
			throw NotSupported ();
		}

		public ValueReference VisitParameterDeclaration (ParameterDeclaration parameterDeclaration)
		{
			throw NotSupported ();
		}

		public ValueReference VisitPropertyDeclaration (PropertyDeclaration propertyDeclaration)
		{
			throw NotSupported ();
		}

		public ValueReference VisitVariableInitializer (VariableInitializer variableInitializer)
		{
			throw NotSupported ();
		}

		public ValueReference VisitFixedFieldDeclaration (FixedFieldDeclaration fixedFieldDeclaration)
		{
			throw NotSupported ();
		}

		public ValueReference VisitFixedVariableInitializer (FixedVariableInitializer fixedVariableInitializer)
		{
			throw NotSupported ();
		}

		public ValueReference VisitSyntaxTree (SyntaxTree syntaxTree)
		{
			throw NotSupported ();
		}

		public ValueReference VisitSimpleType (SimpleType simpleType)
		{
			return ResolveTypeValueReference (ctx, simpleType);
		}

		public ValueReference VisitMemberType (MemberType memberType)
		{
			return ResolveTypeValueReference (ctx, memberType);
		}

		public ValueReference VisitComposedType (ComposedType composedType)
		{
			return ResolveTypeValueReference (ctx, composedType);
		}

		public ValueReference VisitArraySpecifier (ArraySpecifier arraySpecifier)
		{
			throw NotSupported ();
		}

		public ValueReference VisitPrimitiveType (PrimitiveType primitiveType)
		{
			return ResolveTypeValueReference (ctx, primitiveType);
		}

		public ValueReference VisitComment (Comment comment)
		{
			throw NotSupported ();
		}

		public ValueReference VisitWhitespace (WhitespaceNode whitespaceNode)
		{
			throw NotSupported ();
		}

		public ValueReference VisitText (TextNode textNode)
		{
			throw NotSupported ();
		}

		public ValueReference VisitNewLine (NewLineNode newLineNode)
		{
			throw NotSupported ();
		}

		public ValueReference VisitPreProcessorDirective (PreProcessorDirective preProcessorDirective)
		{
			throw NotSupported ();
		}

		public ValueReference VisitDocumentationReference (DocumentationReference documentationReference)
		{
			throw NotSupported ();
		}

		public ValueReference VisitTypeParameterDeclaration (TypeParameterDeclaration typeParameterDeclaration)
		{
			throw NotSupported ();
		}

		public ValueReference VisitConstraint (Constraint constraint)
		{
			throw NotSupported ();
		}

		public ValueReference VisitCSharpTokenNode (CSharpTokenNode cSharpTokenNode)
		{
			throw NotSupported ();
		}

		public ValueReference VisitIdentifier (Identifier identifier)
		{
			throw NotSupported ();
		}

		public ValueReference VisitPatternPlaceholder (AstNode placeholder, ICSharpCode.NRefactory.PatternMatching.Pattern pattern)
		{
			throw NotSupported ();
		}

		public ValueReference VisitNullNode(AstNode nullNode)
		{
			throw NotSupported ();
		}

		public ValueReference VisitErrorNode (AstNode errorNode)
		{
			throw NotSupported ();
		}

		#endregion
	}
}
