//
// NRefactoryExpressionResolverVisitor.cs
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
using System.Text;
using System.Collections.Generic;

using ICSharpCode.NRefactory.CSharp;

using Mono.Debugging.Client;

namespace Mono.Debugging.Evaluation
{
	// FIXME: if we passed the DebuggerSession and SourceLocation into the NRefactoryExpressionEvaluatorVisitor,
	// we wouldn't need to do this resolve step.
	public class NRefactoryExpressionResolverVisitor : DepthFirstAstVisitor
	{
		readonly List<Replacement> replacements = new List<Replacement> ();
		readonly SourceLocation location;
		readonly DebuggerSession session;
		readonly string expression;
		string parentType;

		class Replacement
		{
			public string NewText;
			public int Offset;
			public int Length;
		}

		public NRefactoryExpressionResolverVisitor (DebuggerSession session, SourceLocation location, string expression)
		{
			this.expression = expression.Replace ("\n", "").Replace ("\r", "");
			this.session = session;
			this.location = location;
		}

		internal string GetResolvedExpression ()
		{
			if (replacements.Count == 0)
				return expression;

			replacements.Sort ((r1, r2) => r1.Offset.CompareTo (r2.Offset));
			var resolved = new StringBuilder ();
			int i = 0;

			foreach (var replacement in replacements) {
				resolved.Append (expression, i, replacement.Offset - i);
				resolved.Append (replacement.NewText);
				i = replacement.Offset + replacement.Length;
			}

			var last = replacements [replacements.Count - 1];
			resolved.Append (expression, last.Offset + last.Length, expression.Length - (last.Offset + last.Length));

			return resolved.ToString ();
		}

		string GenerateGenericArgs (int genericArgs)
		{
			if (genericArgs == 0)
				return "";

			string result = "<";
			for (int i = 0; i < genericArgs; i++)
				result += "int,";

			return result.Remove (result.Length - 1) + ">";
		}

		void ReplaceType (string name, int genericArgs, int offset, int length, bool memberType = false)
		{
			string type = null;

			if (genericArgs == 0)
				type = session.ResolveIdentifierAsType (name, location);
			else
				type = session.ResolveIdentifierAsType (name + "`" + genericArgs, location);

			if (string.IsNullOrEmpty (type)) {
				parentType = null;
			} else {
				if (memberType) {
					type = type.Substring (type.LastIndexOf ('.') + 1);
				} else {
					type = "global::" + type;
				}

				parentType = type + GenerateGenericArgs (genericArgs);
				var replacement = new Replacement { Offset = offset, Length = length, NewText = type };
				replacements.Add (replacement);
			}
		}

		void ReplaceType (AstType type)
		{
			int length = type.EndLocation.Column - type.StartLocation.Column;
			int offset = type.StartLocation.Column - 1;

			ReplaceType (type.ToString (), 0, offset, length);
		}

		public override void VisitIdentifierExpression (IdentifierExpression identifierExpression)
		{
			base.VisitIdentifierExpression (identifierExpression);

			int length = identifierExpression.IdentifierToken.EndLocation.Column - identifierExpression.IdentifierToken.StartLocation.Column;
			int offset = identifierExpression.IdentifierToken.StartLocation.Column - 1;

			ReplaceType (identifierExpression.Identifier, identifierExpression.TypeArguments.Count, offset, length);
		}

		public override void VisitTypeReferenceExpression (TypeReferenceExpression typeReferenceExpression)
		{
			ReplaceType (typeReferenceExpression.Type);
		}

		public override void VisitComposedType (ComposedType composedType)
		{
			// Note: we specifically do not handle this case because the 'base' implementation will eventually
			// call VisitMemberType() or VisitSimpleType() on the ComposedType.BaseType which is all we really
			// care to resolve.
			base.VisitComposedType (composedType);
		}

		public override void VisitMemberType (MemberType memberType)
		{
			base.VisitMemberType (memberType);
			if (parentType == null)
				return;
			int length = memberType.MemberNameToken.EndLocation.Column - memberType.MemberNameToken.StartLocation.Column;
			int offset = memberType.MemberNameToken.StartLocation.Column - 1;
			ReplaceType (parentType + "." + memberType.MemberName, memberType.TypeArguments.Count, offset, length, true);
		}

		public override void VisitSimpleType (SimpleType simpleType)
		{
			base.VisitSimpleType (simpleType);

			int length = simpleType.IdentifierToken.EndLocation.Column - simpleType.IdentifierToken.StartLocation.Column;
			int offset = simpleType.IdentifierToken.StartLocation.Column - 1;

			ReplaceType (simpleType.Identifier, simpleType.TypeArguments.Count, offset, length);
		}
	}
}
