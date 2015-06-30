using System;
using Mono.Debugging.Backend;

namespace Mono.Debugging.Client
{
	[Serializable]
	public class StackFrame
	{
		long address;
		string addressSpace;
		SourceLocation location;
		IBacktrace sourceBacktrace;
		string language;
		int index;
		bool isExternalCode;
		bool isDebuggerHidden;
		bool hasDebugInfo;
		string fullModuleName;
		string fullTypeName;
		
		[NonSerialized]
		DebuggerSession session;

		public StackFrame (long address, string addressSpace, SourceLocation location, string language, bool isExternalCode, bool hasDebugInfo, bool isDebuggerHidden, string fullModuleName, string fullTypeName)
		{
			this.address = address;
			this.addressSpace = addressSpace;
			this.location = location;
			this.language = language;
			this.isExternalCode = isExternalCode;
			this.isDebuggerHidden = isDebuggerHidden;
			this.hasDebugInfo = hasDebugInfo;
			this.fullModuleName = fullModuleName;
			this.fullTypeName = fullTypeName;
		}

		public StackFrame (long address, string addressSpace, SourceLocation location, string language, bool isExternalCode, bool hasDebugInfo, string fullModuleName, string fullTypeName)
			: this (address, addressSpace, location, language, isExternalCode, hasDebugInfo, false, fullModuleName, fullTypeName)
		{
		}

		[Obsolete]
		public StackFrame (long address, string addressSpace, SourceLocation location, string language)
			: this (address, addressSpace, location, language, string.IsNullOrEmpty (location.FileName), true, "", "")
		{
		}

		[Obsolete]
		public StackFrame (long address, string addressSpace, string module, string method, string filename, int line, string language)
			: this (address, addressSpace, new SourceLocation (method, filename, line), language)
		{
		}

		public StackFrame (long address, SourceLocation location, string language, bool isExternalCode, bool hasDebugInfo)
			: this (address, "", location, language, string.IsNullOrEmpty (location.FileName) || isExternalCode, hasDebugInfo, "", "")
		{
		}
		
		public StackFrame (long address, SourceLocation location, string language)
			: this (address, "", location, language, string.IsNullOrEmpty (location.FileName), true, "", "")
		{
		}

		internal void Attach (DebuggerSession debugSession)
		{
			session = debugSession;
		}
		
		public DebuggerSession DebuggerSession {
			get { return session; }
		}
		
		public SourceLocation SourceLocation {
			get { return location; }
		}

		public long Address {
			get { return address; }
		}
		
		public string AddressSpace {
			get { return addressSpace; }
		}

		internal IBacktrace SourceBacktrace {
			get { return sourceBacktrace; }
			set { sourceBacktrace = value; }
		}

		public int Index {
			get { return index; }
			internal set { index = value; }
		}

		public string Language {
			get { return language; }
		}
		
		public bool IsExternalCode {
			get { return isExternalCode; }
		}

		public bool IsDebuggerHidden {
			get { return isDebuggerHidden; }
		}
		
		public bool HasDebugInfo {
			get { return hasDebugInfo; }
		}
		
		public string FullModuleName {
			get { return fullModuleName; }
		}
		
		public string FullTypeName {
			get { return fullTypeName; }
		}
		
		public ObjectValue[] GetLocalVariables ()
		{
			return GetLocalVariables (session.EvaluationOptions);
		}
		
		public ObjectValue[] GetLocalVariables (EvaluationOptions options)
		{
			if (!hasDebugInfo)
				return new ObjectValue [0];

			var values = sourceBacktrace.GetLocalVariables (index, options);
			ObjectValue.ConnectCallbacks (this, values);
			return values;
		}
		
		public ObjectValue[] GetParameters ()
		{
			return GetParameters (session.EvaluationOptions);
		}
		
		public ObjectValue[] GetParameters (EvaluationOptions options)
		{
			if (!hasDebugInfo)
				return new ObjectValue [0];

			var values = sourceBacktrace.GetParameters (index, options);
			ObjectValue.ConnectCallbacks (this, values);
			return values;
		}
		
		public ObjectValue[] GetAllLocals ()
		{
			if (!hasDebugInfo)
				return new ObjectValue [0];

			var evaluator = session.FindExpressionEvaluator (this);

			return evaluator != null ? evaluator.GetLocals (this) : GetAllLocals (session.EvaluationOptions);
		}
		
		public ObjectValue[] GetAllLocals (EvaluationOptions options)
		{
			if (!hasDebugInfo)
				return new ObjectValue [0];

			var values = sourceBacktrace.GetAllLocals (index, options);
			ObjectValue.ConnectCallbacks (this, values);
			return values;
		}
		
		public ObjectValue GetThisReference ()
		{
			return GetThisReference (session.EvaluationOptions);
		}
		
		public ObjectValue GetThisReference (EvaluationOptions options)
		{
			if (!hasDebugInfo)
				return null;

			var value = sourceBacktrace.GetThisReference (index, options);
			if (value != null)
				ObjectValue.ConnectCallbacks (this, value);

			return value;
		}
		
		public ExceptionInfo GetException ()
		{
			return GetException (session.EvaluationOptions);
		}
		
		public ExceptionInfo GetException (EvaluationOptions options)
		{
			var value = sourceBacktrace.GetException (index, options);
			if (value != null)
				value.ConnectCallback (this);

			return value;
		}
		
		public string ResolveExpression (string exp)
		{
			return session.ResolveExpression (exp, location);
		}
		
		public ObjectValue[] GetExpressionValues (string[] expressions, bool evaluateMethods)
		{
			var options = session.EvaluationOptions.Clone ();
			options.AllowMethodEvaluation = evaluateMethods;
			return GetExpressionValues (expressions, options);
		}
		
		public ObjectValue[] GetExpressionValues (string[] expressions, EvaluationOptions options)
		{
			if (!hasDebugInfo) {
				var vals = new ObjectValue [expressions.Length];
				for (int n = 0; n < expressions.Length; n++)
					vals[n] = ObjectValue.CreateUnknown (expressions[n]);

				return vals;
			}

			if (options.UseExternalTypeResolver) {
				var resolved = new string [expressions.Length];
				for (int n = 0; n < expressions.Length; n++)
					resolved[n] = ResolveExpression (expressions[n]);

				expressions = resolved;
			}

			var values = sourceBacktrace.GetExpressionValues (index, expressions, options);
			ObjectValue.ConnectCallbacks (this, values);
			return values;
		}

		public ObjectValue GetExpressionValue (string expression, bool evaluateMethods)
		{
			var options = session.EvaluationOptions.Clone ();
			options.AllowMethodEvaluation = evaluateMethods;
			return GetExpressionValue (expression, options);
		}
		
		public ObjectValue GetExpressionValue (string expression, EvaluationOptions options)
		{
			if (!hasDebugInfo)
				return ObjectValue.CreateUnknown (expression);

			if (options.UseExternalTypeResolver)
				expression = ResolveExpression (expression);

			var values = sourceBacktrace.GetExpressionValues (index, new [] { expression }, options);
			ObjectValue.ConnectCallbacks (this, values);
			return values [0];
		}
		
		/// <summary>
		/// Returns True if the expression is valid and can be evaluated for this frame.
		/// </summary>
		public bool ValidateExpression (string expression)
		{
			return ValidateExpression (expression, session.EvaluationOptions);
		}
		
		/// <summary>
		/// Returns True if the expression is valid and can be evaluated for this frame.
		/// </summary>
		public ValidationResult ValidateExpression (string expression, EvaluationOptions options)
		{
			if (options.UseExternalTypeResolver)
				expression = ResolveExpression (expression);

			return sourceBacktrace.ValidateExpression (index, expression, options);
		}
		
		public CompletionData GetExpressionCompletionData (string exp)
		{
			return hasDebugInfo ? sourceBacktrace.GetExpressionCompletionData (index, exp) : null;
		}
		
		// Returns disassembled code for this stack frame.
		// firstLine is the relative code line. It can be negative.
		public AssemblyLine[] Disassemble (int firstLine, int count)
		{
			return sourceBacktrace.Disassemble (index, firstLine, count);
		}

		public override string ToString()
		{
			string loc;

			if (location.Line != -1 && !string.IsNullOrEmpty (location.FileName)) {
				loc = " at " + location.FileName + ":" + location.Line;
				if (location.Column != 0)
					loc += "," + location.Column;
			} else if (!string.IsNullOrEmpty (location.FileName)) {
				loc = " at " + location.FileName;
			} else {
				loc = string.Empty;
			}

			return string.Format ("0x{0:X} in {1}{2}", address, location.MethodName, loc);
		}

		public void UpdateSourceFile (string newFilePath)
		{
			location = new SourceLocation (location.MethodName, newFilePath, location.Line, location.Column, location.EndLine, location.EndColumn, location.FileHash);
		}
	}
	
	[Serializable]
	public struct ValidationResult
	{
		readonly string message;
		readonly bool isValid;
		
		public ValidationResult (bool isValid, string message)
		{
			this.isValid = isValid;
			this.message = message;
		}
		
		public bool IsValid { get { return isValid; } }
		public string Message { get { return message; } }
		
		public static implicit operator bool (ValidationResult result)
		{
			return result.isValid;
		}
	}
}
