﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;

namespace Json.JsonE.Expressions;

public class ContextAccessor
{
	private readonly IContextAccessorSegment[] _segments;
	private readonly string _asString;

	internal static ContextAccessor Now { get; } = new(new[] { new PropertySegment("now", false) }, "now");
	internal static ContextAccessor Default { get; } = new(new[] { new PropertySegment("x", false) }, "x");

	private ContextAccessor(IEnumerable<IContextAccessorSegment> segments, string asString)
	{
		_segments = segments.ToArray();
		_asString = asString;
	}

	internal static bool TryParse(ReadOnlySpan<char> source, ref int index, out ContextAccessor? accessor)
	{
		int i = index;
		if (!source.ConsumeWhitespace(ref i))
		{
			accessor = null;
			return false;
		}

		if (!source.TryParseName(ref i, out var name))
		{
			accessor = null;
			return false;
		}

		if (name.In("true", "false", "null"))
		{
			accessor = null;
			return false;
		}

		var segments = new List<IContextAccessorSegment>{new PropertySegment(name!, false)};

		while (i < source.Length)
		{
			if (!source.ConsumeWhitespace(ref i))
			{
				accessor = null;
				return false;
			}

			switch (source[i])
			{
				case '.':
					i++;
					if (!source.TryParseName(ref i, out name))
					{
						accessor = null;
						return false;
					}

					segments.Add(new PropertySegment(name!, false));
					continue;
				case '[':
					i++;

					if (!source.ConsumeWhitespace(ref i))
					{
						accessor = null;
						return false;
					}

					if (!TryParseQuotedName(source, ref i, out var segment) &&
					    !TryParseSlice(source, ref i, out segment) &&
					    !TryParseIndex(source, ref i, out segment) &&
					    !TryParseExpression(source, ref i, out segment))
					{
						accessor = null;
						return false;
					}

					segments.Add(segment!);

					if (!source.ConsumeWhitespace(ref i))
					{
						accessor = null;
						return false;
					}

					if (source[i] != ']')
					{
						accessor = null;
						return false;
					}

					i++;

					continue;
			}

			break;
		}

		var asString = source[index..i].ToString();
		index = i;
		accessor = new ContextAccessor(segments, asString);
		return true;
	}

	private static bool TryParseQuotedName(ReadOnlySpan<char> source, ref int index, out IContextAccessorSegment? segment)
	{
		char quoteChar;
		var i = index;
		switch (source[index])
		{
			case '"':
				quoteChar = '"';
				i++;
				break;
			case '\'':
				quoteChar = '\'';
				i++;
				break;
			default:
				segment = null;
				return false;
		}

		var done = false;
		var sb = new StringBuilder();
		while (i < source.Length && !done)
		{
			if (source[i] == quoteChar)
			{
				done = true;
				i++;
			}
			else
			{
				if (!source.EnsureValidNameCharacter(i))
				{
					segment = null;
					return false;
				}
				sb.Append(source[i]);
				i++;
			}
		}

		if (!done)
		{
			segment = null;
			return false;
		}

		index = i;
		segment = new PropertySegment(sb.ToString(), true);
		return true;

	}

	private static bool TryParseIndex(ReadOnlySpan<char> source, ref int index, out IContextAccessorSegment? segment)
	{
		if (!source.TryGetInt(ref index, out var i))
		{
			segment = null;
			return false;
		}

		segment = new IndexSegment(i);
		return true;
	}

	private static bool TryParseSlice(ReadOnlySpan<char> source, ref int index, out IContextAccessorSegment? segment)
	{
		var i = index;
		int? start = null, end = null, step = null;

		if (source.TryGetInt(ref i, out var value))
			start = value;

		if (!source.ConsumeWhitespace(ref i))
		{
			segment = null;
			return false;
		}

		if (source[i] != ':')
		{
			segment = null;
			return false;
		}

		i++; // consume :

		if (!source.ConsumeWhitespace(ref i))
		{
			segment = null;
			return false;
		}

		if (source.TryGetInt(ref i, out value))
			end = value;

		if (!source.ConsumeWhitespace(ref i))
		{
			segment = null;
			return false;
		}

		if (source[i] == ':')
		{
			i++; // consume :

			if (!source.ConsumeWhitespace(ref i))
			{
				segment = null;
				return false;
			}

			if (source.TryGetInt(ref i, out value))
				step = value;
		}

		index = i;
		segment = new SliceSegment(start, end, step);
		return true;
	}

	private static bool TryParseExpression(ReadOnlySpan<char> source, ref int i, out IContextAccessorSegment? segment)
	{
		if (!ExpressionParser.TryParse(source, ref i, out var expression))
		{
			segment = null;
			return false;
		}

		segment = new ExpressionSegment(expression!);
		return true;
	}

	internal bool TryFind(JsonNode? context, out JsonNode? value)
	{
		var current = context;
		foreach (var segment in _segments)
		{
			if (!segment.TryFind(current, out value)) return false;

			current = value;
		}

		value = current;
		return true;
	}

	public override string ToString() => _asString;
}