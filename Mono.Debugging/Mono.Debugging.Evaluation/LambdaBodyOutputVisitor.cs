using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

using Mono.Debugging.Client;

using ICSharpCode.NRefactory.CSharp;

namespace Mono.Debugging.Evaluation
{
	// Outputs lambda expression inputted on Immediate Pad. Also
	// this class works for followings.
	// - Check if body of lambda has not-supported expression
	// - Output reserved words like `this` or `base` with generated
	//   identifer.
	// - Collect variable references for outside the lambda
	//   (local variables/properties...)
	public class LambdaBodyOutputVisitor : CSharpOutputVisitor
	{
		readonly Dictionary<string, ValueReference> userVariables;
		readonly EvaluationContext ctx;

		Dictionary<string, Tuple<string, object>> localValues;
		List<string> definedIdentifier;
		int gensymCount;

		public LambdaBodyOutputVisitor (EvaluationContext ctx, Dictionary<string, ValueReference> userVariables, TextWriter writer) : base (writer, FormattingOptionsFactory.CreateMono ())
		{
			this.ctx = ctx;
			this.userVariables = userVariables;
			this.localValues = new Dictionary<string, Tuple<string, object>> ();
			this.definedIdentifier = new List<string> ();
		}

		public Tuple<string, object>[] GetLocalValues ()
		{
			var locals = new Tuple<string, object>[localValues.Count];
			int n = 0;
			foreach(var localv in localValues.Values) {
				locals [n] = localv;
				n++;
			}
			return locals;
		}

		static Exception NotSupportedToConsistency ()
		{
			return new NotSupportedExpressionException ();
		}

		static Exception NotSupported ()
		{
			return new NotSupportedExpressionException ();
		}

		static Exception EvaluationError (string message, params object [] args)
		{
			return new EvaluatorException (message, args);
		}

		bool IsPublicValueFlag (ObjectValueFlags flags)
		{
			var isField = (flags & ObjectValueFlags.Field) != 0;
			var isProperty = (flags & ObjectValueFlags.Property) != 0;
			var isPublic = (flags & ObjectValueFlags.Public) != 0;

			return !(isField || isProperty) || isPublic;
		}

		void AssertPublicType (object type)
		{
			if (!ctx.Adapter.IsPublic (ctx, type)) {
				var typeName = ctx.Adapter.GetDisplayTypeName (ctx, type);
				throw EvaluationError ("Not Support to reference non-public type: `{0}'", typeName);
			}
		}

		void AssertPublicValueReference (ValueReference vr)
		{
			if (!(vr is NamespaceValueReference)) {
				var typ = vr.Type;
				AssertPublicType (typ);
			}
			if (!IsPublicValueFlag (vr.Flags)) {
				throw EvaluationError ("Not Support to reference non-public thing: `{0}'", vr.Name);
			}
		}

		ValueReference Evaluate (IdentifierExpression t)
		{
			var visitor = new NRefactoryExpressionEvaluatorVisitor (ctx, t.Identifier, null, userVariables);
			return t.AcceptVisitor<ValueReference> (visitor);
		}

		ValueReference Evaluate (BaseReferenceExpression t)
		{
			var visitor = new NRefactoryExpressionEvaluatorVisitor (ctx, "base", null, userVariables);
			return t.AcceptVisitor<ValueReference> (visitor);
		}

		ValueReference Evaluate (ThisReferenceExpression t)
		{
			var visitor = new NRefactoryExpressionEvaluatorVisitor (ctx, "this", null, userVariables);
			return t.AcceptVisitor<ValueReference> (visitor);
		}

		string GenerateSymbol (string s)
		{
			var prefix = "__" + s;
			var sym = prefix;
			while (ExistsLocalName (sym)) {
				sym = prefix + gensymCount++;
			}

			return sym;
		}

		string AddToLocals (string name, ValueReference vr, bool shouldRename = false)
		{
			if (localValues.ContainsKey (name))
				return GetLocalName (name);

			string localName;
			if (shouldRename) {
				localName = GenerateSymbol (name);
			} else if (!ExistsLocalName (name)) {
				localName = name;
			} else {
				throw EvaluationError ("Cannot use a variable named {0} inside lambda", name);
			}

			AssertPublicValueReference (vr);

			var valu = vr != null ? vr.Value : null;
			var pair = Tuple.Create (localName, valu);
			localValues.Add (name, pair);
			return localName;
		}

		string GetLocalName (string name)
		{
			Tuple<string, object> pair;
			if (localValues.TryGetValue(name, out pair))
				return pair.Item1;
			return null;
		}

		bool ExistsLocalName (string localName)
		{
			foreach(var pair in localValues.Values) {
				if (pair.Item1 == localName)
					return true;
			}
			return definedIdentifier.Contains (localName);
		}

		#region IAstVisitor implementation

		public override void VisitAnonymousMethodExpression (AnonymousMethodExpression anonymousMethodExpression)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitUndocumentedExpression (UndocumentedExpression undocumentedExpression)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitArrayCreateExpression (ArrayCreateExpression arrayCreateExpression)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitArrayInitializerExpression (ArrayInitializerExpression arrayInitializerExpression)
		{
			throw NotSupportedToConsistency ();
		}
		/*
		public override void VisitAsExpression (AsExpression asExpression)
		{
		}
		*/
		public override void VisitAssignmentExpression (AssignmentExpression assignmentExpression)
		{
			throw EvaluationError ("Not support assignment expression inside lambda");
		}

		public override void VisitBaseReferenceExpression (BaseReferenceExpression baseReferenceExpression)
		{
			StartNode (baseReferenceExpression);
			var basee = "base";
			var localbase = GetLocalName(basee);
			if (localbase == null) {
				var vr = Evaluate (baseReferenceExpression);
				localbase = AddToLocals (basee, vr, true);
			}
			WriteKeyword (localbase);
			EndNode (baseReferenceExpression);
		}
		/*
		public override void VisitBinaryOperatorExpression (BinaryOperatorExpression binaryOperatorExpression)
		{
		}
		*//*
		public override void VisitCastExpression (CastExpression castExpression)
		{
		}
		*/
		public override void VisitCheckedExpression (CheckedExpression checkedExpression)
		{
			throw NotSupportedToConsistency ();
		}
		/*
		public override void VisitConditionalExpression (ConditionalExpression conditionalExpression)
		{
		}
		*/
		public override void VisitDefaultValueExpression (DefaultValueExpression defaultValueExpression)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitDirectionExpression (DirectionExpression directionExpression)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitIdentifierExpression (IdentifierExpression identifierExpression)
		{
			StartNode (identifierExpression);
			var identifier = identifierExpression.Identifier;
			var localIdentifier = "";

			if (definedIdentifier.Contains (identifier)) {
				localIdentifier = identifier;
			} else {
				localIdentifier = GetLocalName (identifier);
				if (localIdentifier == null) {
					var vr = Evaluate (identifierExpression);
					localIdentifier = AddToLocals (identifier, vr);
				}
			}
			var idToken = identifierExpression.IdentifierToken;
			idToken.Name = localIdentifier;
			WriteIdentifier (idToken);
			WriteTypeArguments (identifierExpression.TypeArguments);
			EndNode (identifierExpression);
		}
		/*
		public override void VisitIndexerExpression (IndexerExpression indexerExpression)
		{
		}
		*/
		public override void VisitInvocationExpression (InvocationExpression invocationExpression)
		{
			var invocationTarget = invocationExpression.Target;
			if (!(invocationTarget is IdentifierExpression)) {
				base.VisitInvocationExpression (invocationExpression);
				return;
			}

			var argc = invocationExpression.Arguments.Count;
			var method = (IdentifierExpression)invocationTarget;
			var methodName = method.Identifier;
			var vref = ctx.Adapter.GetThisReference (ctx);
			var vtype = ctx.Adapter.GetEnclosingType (ctx);
			string accessor = null;

			var hasInstanceMethod = ctx.Adapter.HasMethodWithParamLength (ctx, vtype, methodName, BindingFlags.Instance, argc);
			var hasStaticMethod = ctx.Adapter.HasMethodWithParamLength (ctx, vtype, methodName, BindingFlags.Static, argc);
			var publicFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
			var hasPublicMethod = ctx.Adapter.HasMethodWithParamLength (ctx, vtype, methodName, publicFlags, argc);

			if ((hasInstanceMethod || hasStaticMethod) && !hasPublicMethod)
				throw EvaluationError ("Only support public method invocation inside lambda");

			if (vref == null && hasStaticMethod) {
				AssertPublicType (vtype);
				var typeName = ctx.Adapter.GetTypeName (ctx, vtype);
				accessor = ctx.Adapter.GetDisplayTypeName (typeName);
			} else if (vref != null) {
				AssertPublicValueReference (vref);
				if (hasInstanceMethod) {
					if (hasStaticMethod) {
						// It's hard to determine which one is expected because
						// we don't have any information of parameter types now.
						throw EvaluationError ("Not supported invocation of static/instance overloaded method");
					}
					accessor = GetLocalName ("this");
					if (accessor == null)
						accessor = AddToLocals ("this", vref, true);
				} else if (hasStaticMethod) {
					var typeName = ctx.Adapter.GetTypeName (ctx, vtype);
					accessor = ctx.Adapter.GetDisplayTypeName (typeName);
				}
			}

			StartNode (invocationExpression);
			if (accessor == null)
				WriteIdentifier (method.Identifier);
			else
				WriteKeyword (accessor + "." + methodName);
			Space (policy.SpaceBeforeMethodCallParentheses);
			WriteCommaSeparatedListInParenthesis (invocationExpression.Arguments, policy.SpaceWithinMethodCallParentheses);
			EndNode (invocationExpression);
		}

		/*
		public override void VisitIsExpression (IsExpression isExpression)
		{
		}*/

		public override void VisitLambdaExpression (LambdaExpression lambdaExpression)
		{
			foreach (var par in lambdaExpression.Parameters) {
				if (par.ParameterModifier != ICSharpCode.NRefactory.CSharp.ParameterModifier.None)
					throw NotSupported();

				definedIdentifier.Add(par.Name);
			}

			base.VisitLambdaExpression (lambdaExpression);
		}
		/*
		public override void VisitMemberReferenceExpression (MemberReferenceExpression memberReferenceExpression)
		{
		}*/

		public override void VisitNamedArgumentExpression (NamedArgumentExpression namedArgumentExpression)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitNamedExpression (NamedExpression namedExpression)
		{
			throw NotSupportedToConsistency ();
		}
		/*
		public override void VisitNullReferenceExpression (NullReferenceExpression nullReferenceExpression)
		{
		}
		*//*
		public override void VisitObjectCreateExpression (ObjectCreateExpression objectCreateExpression)
		{
		}*/

		public override void VisitAnonymousTypeCreateExpression (AnonymousTypeCreateExpression anonymousTypeCreateExpression)
		{
			throw NotSupportedToConsistency ();
		}
		/*
		public override void VisitParenthesizedExpression (ParenthesizedExpression parenthesizedExpression)
		{
		}*/

		public override void VisitPointerReferenceExpression (PointerReferenceExpression pointerReferenceExpression)
		{
			throw NotSupportedToConsistency ();
		}
		/*
		public override void VisitPrimitiveExpression (PrimitiveExpression primitiveExpression)
		{
			return primitiveExpression.ToString ();
		}*/

		public override void VisitSizeOfExpression (SizeOfExpression sizeOfExpression)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitStackAllocExpression (StackAllocExpression stackAllocExpression)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitThisReferenceExpression (ThisReferenceExpression thisReferenceExpression)
		{
			StartNode (thisReferenceExpression);
			var thiss = "this";
			var localthis = GetLocalName (thiss);
			if (localthis == null) {
				var vr = Evaluate (thisReferenceExpression);
				localthis = AddToLocals (thiss, vr, true);
			}
			WriteKeyword (localthis);
			EndNode (thisReferenceExpression);
		}
		/*
		public override void VisitTypeOfExpression (TypeOfExpression typeOfExpression)
		{
		}*/
		/*
		public override void VisitTypeReferenceExpression (TypeReferenceExpression typeReferenceExpression)
		{
		}*//*
		public override void VisitUnaryOperatorExpression (UnaryOperatorExpression unaryOperatorExpression)
		{
		}*/

		public override void VisitUncheckedExpression (UncheckedExpression uncheckedExpression)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitQueryExpression (QueryExpression queryExpression)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitQueryContinuationClause (QueryContinuationClause queryContinuationClause)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitQueryFromClause (QueryFromClause queryFromClause)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitQueryLetClause (QueryLetClause queryLetClause)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitQueryWhereClause (QueryWhereClause queryWhereClause)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitQueryJoinClause (QueryJoinClause queryJoinClause)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitQueryOrderClause (QueryOrderClause queryOrderClause)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitQueryOrdering (QueryOrdering queryOrdering)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitQuerySelectClause (QuerySelectClause querySelectClause)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitQueryGroupClause (QueryGroupClause queryGroupClause)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitAttribute (ICSharpCode.NRefactory.CSharp.Attribute attribute)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitAttributeSection (AttributeSection attributeSection)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitDelegateDeclaration (DelegateDeclaration delegateDeclaration)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitNamespaceDeclaration (NamespaceDeclaration namespaceDeclaration)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitTypeDeclaration (TypeDeclaration typeDeclaration)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitUsingAliasDeclaration (UsingAliasDeclaration usingAliasDeclaration)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitUsingDeclaration (UsingDeclaration usingDeclaration)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitExternAliasDeclaration (ExternAliasDeclaration externAliasDeclaration)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitBlockStatement (BlockStatement blockStatement)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitBreakStatement (BreakStatement breakStatement)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitCheckedStatement (CheckedStatement checkedStatement)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitContinueStatement (ContinueStatement continueStatement)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitDoWhileStatement (DoWhileStatement doWhileStatement)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitEmptyStatement (EmptyStatement emptyStatement)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitExpressionStatement (ExpressionStatement expressionStatement)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitFixedStatement (FixedStatement fixedStatement)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitForeachStatement (ForeachStatement foreachStatement)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitForStatement (ForStatement forStatement)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitGotoCaseStatement (GotoCaseStatement gotoCaseStatement)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitGotoDefaultStatement (GotoDefaultStatement gotoDefaultStatement)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitGotoStatement (GotoStatement gotoStatement)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitIfElseStatement (IfElseStatement ifElseStatement)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitLabelStatement (LabelStatement labelStatement)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitLockStatement (LockStatement lockStatement)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitReturnStatement (ReturnStatement returnStatement)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitSwitchStatement (SwitchStatement switchStatement)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitSwitchSection (SwitchSection switchSection)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitCaseLabel (CaseLabel caseLabel)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitThrowStatement (ThrowStatement throwStatement)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitTryCatchStatement (TryCatchStatement tryCatchStatement)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitCatchClause (CatchClause catchClause)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitUncheckedStatement (UncheckedStatement uncheckedStatement)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitUnsafeStatement (UnsafeStatement unsafeStatement)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitUsingStatement (UsingStatement usingStatement)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitVariableDeclarationStatement (VariableDeclarationStatement variableDeclarationStatement)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitWhileStatement (WhileStatement whileStatement)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitYieldBreakStatement (YieldBreakStatement yieldBreakStatement)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitYieldReturnStatement (YieldReturnStatement yieldReturnStatement)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitAccessor (Accessor accessor)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitConstructorDeclaration (ConstructorDeclaration constructorDeclaration)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitConstructorInitializer (ConstructorInitializer constructorInitializer)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitDestructorDeclaration (DestructorDeclaration destructorDeclaration)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitEnumMemberDeclaration (EnumMemberDeclaration enumMemberDeclaration)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitEventDeclaration (EventDeclaration eventDeclaration)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitCustomEventDeclaration (CustomEventDeclaration customEventDeclaration)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitFieldDeclaration (FieldDeclaration fieldDeclaration)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitIndexerDeclaration (IndexerDeclaration indexerDeclaration)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitMethodDeclaration (MethodDeclaration methodDeclaration)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitOperatorDeclaration (OperatorDeclaration operatorDeclaration)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitParameterDeclaration (ParameterDeclaration parameterDeclaration)
		{
			if (parameterDeclaration.Parent is LambdaExpression)
				base.VisitParameterDeclaration (parameterDeclaration);
			else
				throw NotSupportedToConsistency ();
		}

		public override void VisitPropertyDeclaration (PropertyDeclaration propertyDeclaration)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitVariableInitializer (VariableInitializer variableInitializer)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitFixedFieldDeclaration (FixedFieldDeclaration fixedFieldDeclaration)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitFixedVariableInitializer (FixedVariableInitializer fixedVariableInitializer)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitSyntaxTree (SyntaxTree syntaxTree)
		{
			throw NotSupportedToConsistency ();
		}
		/*
		public override void VisitSimpleType (SimpleType simpleType)
		{
		}*/
		/*
		public override void VisitMemberType (MemberType memberType)
		{
		}*/
		/*
		public override void VisitComposedType (ComposedType composedType)
		{
		}*/

		public override void VisitArraySpecifier (ArraySpecifier arraySpecifier)
		{
			throw NotSupportedToConsistency ();
		}
		/*
		public override void VisitPrimitiveType (PrimitiveType primitiveType)
		{
		}*/

		public override void VisitComment (Comment comment)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitWhitespace (WhitespaceNode whitespaceNode)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitText (TextNode textNode)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitNewLine (NewLineNode newLineNode)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitPreProcessorDirective (PreProcessorDirective preProcessorDirective)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitDocumentationReference (DocumentationReference documentationReference)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitTypeParameterDeclaration (TypeParameterDeclaration typeParameterDeclaration)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitConstraint (Constraint constraint)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitCSharpTokenNode (CSharpTokenNode cSharpTokenNode)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitIdentifier (Identifier identifier)
		{
			throw NotSupportedToConsistency ();
		}

		public override void VisitPatternPlaceholder (AstNode placeholder, ICSharpCode.NRefactory.PatternMatching.Pattern pattern)
		{
			throw NotSupportedToConsistency ();
		}
		/*
		public override void VisitNullNode (AstNode nullNode)
		{
			throw NotSupportedToConsistency ();
		}
		*//*
		public override void VisitErrorNode (AstNode errorNode)
		{
			throw NotSupportedToConsistency ();
		}*/

		#endregion
	}
}
