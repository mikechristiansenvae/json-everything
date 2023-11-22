﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text;
using Json.JsonE.Expressions.Functions;

namespace Json.JsonE.Expressions;

internal class FunctionExpressionNode : ExpressionNode
{
	public ContextAccessor FunctionAccessor { get; }
	public ExpressionNode[] Parameters { get; }

	public FunctionExpressionNode(ContextAccessor functionAccessor, IEnumerable<ExpressionNode> parameters)
	{
		FunctionAccessor = functionAccessor;
		Parameters = parameters.ToArray();
	}

	public override JsonNode? Evaluate(EvaluationContext context)
	{
		if (context.Find(FunctionAccessor) is not JsonValue functionNode ||
		    !functionNode.TryGetValue(out FunctionDefinition? function))
			throw new TemplateException($"Cannot find function for `{FunctionAccessor}`");

		var parameterValues = Parameters.Select(x => x.Evaluate(context)).ToArray();

		return function.Invoke(parameterValues, context);
	}

	public override void BuildString(StringBuilder builder)
	{
		//builder.Append(Function.Name);
		builder.Append('(');

		if (Parameters.Any())
		{
			Parameters[0].BuildString(builder);
			for (int i = 1; i < Parameters.Length; i++)
			{
				builder.Append(',');
				Parameters[i].BuildString(builder);
			}
		}

		builder.Append(')');
	}

	public override string ToString()
	{
		throw new NotImplementedException();
		//var parameterList = string.Join(", ", Parameters);
		//return $"{Function.Name}({parameterList})";
	}
}

internal class FunctionExpressionParser : IOperandExpressionParser
{
	public bool TryParse(ReadOnlySpan<char> source, ref int index, out ExpressionNode? expression)
	{
		if (!TryParseFunction(source, ref index, out var accessor, out var args))
		{
			expression = null;
			return false;
		}

		expression = new FunctionExpressionNode(accessor!, args!);
		return true;
	}

	private static bool TryParseFunction(ReadOnlySpan<char> source, ref int index, out ContextAccessor? accessor, out List<ExpressionNode>? arguments)
	{
		int i = index;

		if (!source.ConsumeWhitespace(ref i))
		{
			arguments = null;
			accessor = null;
			return false;
		}

		// parse function accessor
		if (!ContextAccessor.TryParse(source, ref i, out accessor))
		{
			arguments = null;
			accessor = null;
			return false;
		}

		if (!source.ConsumeWhitespace(ref i) || i == source.Length)
		{
			arguments = null;
			accessor = null;
			return false;
		}

		// consume (
		if (source[i] != '(')
		{
			arguments = null;
			accessor = null;
			return false;
		}

		i++;

		// parse list of arguments - all expressions
		arguments = new List<ExpressionNode>();
		var done = false;

		while (i < source.Length && !done)
		{
			if (!source.ConsumeWhitespace(ref i))
			{
				arguments = null;
				accessor = null;
				return false;
			}

			if (!ExpressionParser.TryParse(source, ref i, out var expr))
			{
				arguments = null;
				accessor = null;
				return false;
			}

			arguments.Add(expr!);

			if (!source.ConsumeWhitespace(ref i))
			{
				arguments = null;
				accessor = null;
				return false;
			}

			switch (source[i])
			{
				case ')':
					done = true;
					break;
				case ',':
					break;
				default:
					arguments = null;
					accessor = null;
					return false;
			}

			i++;
		}

		index = i;
		return true;
	}
}