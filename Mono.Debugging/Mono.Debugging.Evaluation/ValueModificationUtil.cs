using System;
using Mono.Debugging.Backend;

namespace Mono.Debugging.Evaluation
{
	public static class ValueModificationUtil
	{
		internal static ValueReference EvaluateRightHandValue (EvaluationContext context, string value, object expectedType)
		{
			context.Options.AllowMethodEvaluation = true;
			context.Options.AllowTargetInvoke = true;
			try {
				return context.Evaluator.Evaluate (context, value, expectedType);
			} catch (Exception e) {
				throw new ValueModificationException(string.Format ("Cannot evaluate '{0}': {1}", value, e.Message), e);
			}
		}

		internal static object ConvertRightHandValue (EvaluationContext context, object value, object expectedType)
		{
			try {
				return context.Adapter.Convert (context, value, expectedType);
			} catch (Exception e) {
				throw new ValueModificationException(string.Format ("Conversion error: {0}", e.Message), e);
			}
		}

		internal static EvaluationResult ModifyValue (EvaluationContext context, string value, object expectedType, Action<object> valueSetter)
		{
			var rightHandValue = EvaluateRightHandValue (context, value, expectedType);

			object val;
			try {
				val = rightHandValue.Value;
			} catch (Exception e) {
				throw new ValueModificationException(string.Format ("Cannot get real object of {0}", value), e);
			}
			var convertedValue = ConvertRightHandValue (context, val, expectedType);
			try {
				valueSetter (convertedValue);
			} catch (Exception e) {
				throw new ValueModificationException(string.Format ("Error while assigning new value to object: {0}", e.Message), e);
			}
			// don't wrap with try-catch it, this call normally should not throw exceptions produced by wrong user input.
			// If exception was occured this means something has gone wrong in we have to report it in log
			return context.Evaluator.TargetObjectToExpression (context, convertedValue);
		}

	}
}