﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Json.Schema.CodeGeneration.Model;

namespace Json.Schema.CodeGeneration.Language;

public class CSharpCodeWriter : ICodeWriter
{
	internal CSharpCodeWriter(){}

	public void Write(StringBuilder builder, TypeModel model)
	{
		var allModels = CollectModels(model)
			.Distinct()
			.GroupBy(x => x.Name)
			.ToArray();
		var duplicates = allModels.Where(x => x.Key != null && x.Count() != 1);

		if (duplicates.Any())
		{
			var names = string.Join(",", duplicates.Select(x => x.Key));
			throw new SchemaConversionException($"Found duplicate definitions for the names [{names}]");
		}

		foreach (var singleModel in allModels)
		{
			WriteDeclaration(builder, singleModel.Single());
		}
	}

	private static IEnumerable<TypeModel> CollectModels(TypeModel model)
	{
		yield return model;
		switch (model)
		{
			case EnumModel:
				yield break;
			case ArrayModel arrayModel:
				yield return arrayModel.Items;
				yield break;
			case ObjectModel objectModel:
				foreach (var propertyModel in objectModel.Properties)
				{
					yield return propertyModel.Type;
				}
				yield break;
		}
	}

	private static void WriteUsage(StringBuilder builder, TypeModel model)
	{
		if (model.IsSimple)
		{
			if (ReferenceEquals(model, CommonModels.String))
				builder.Append("string");
			else if (ReferenceEquals(model, CommonModels.Integer))
				builder.Append("int");
			else if (ReferenceEquals(model, CommonModels.Number))
				builder.Append("double");
			else if (ReferenceEquals(model, CommonModels.Boolean))
				builder.Append("bool");
			else
				throw new ArgumentOutOfRangeException(nameof(model));
			return;
		}

		if (model.Name != null)
		{
			builder.Append(model.Name);
			return;
		}

		// only arrays and dictionaries can opt out of names
		switch (model)
		{
			case ArrayModel arrayModel:
				WriteUsage(builder, arrayModel);
				break;
			case DictionaryModel dictionaryModel:
				WriteUsage(builder, dictionaryModel);
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(model));
		}
	}

	private static void WriteDeclaration(StringBuilder builder, TypeModel model)
	{
		if (model.Name == null) return;
		if (model.IsSimple) return;

		switch (model)
		{
			case EnumModel enumModel:
				WriteDeclaration(builder, enumModel);
				break;
			case ArrayModel arrayModel:
				WriteDeclaration(builder, arrayModel);
				break;
			case ObjectModel objectModel:
				WriteDeclaration(builder, objectModel);
				break;
			case DictionaryModel dictionaryModel:
				WriteDeclaration(builder, dictionaryModel);
				break;
			default:
				if (ReferenceEquals(model, CommonModels.String))
					builder.Append(CommonModels.String.Name);
				else if (ReferenceEquals(model, CommonModels.Integer))
					builder.Append(CommonModels.Integer.Name);
				else if (ReferenceEquals(model, CommonModels.Number))
					builder.Append(CommonModels.Number.Name);
				else if (ReferenceEquals(model, CommonModels.Boolean))
					builder.Append(CommonModels.Boolean.Name);
				else
					throw new ArgumentOutOfRangeException(nameof(model));
				break;
		}
	}

	private static void WriteDeclaration(StringBuilder builder, EnumModel model)
	{
		void WriteValue(EnumValue value)
		{
			builder.Append("\t");
			builder.Append(value.Name);
			builder.Append(" = ");
			builder.Append(value.Value);
		}

		builder.Append("public enum ");
		builder.AppendLine(model.Name);
		builder.AppendLine("{");
		for (var i = 0; i < model.Values.Length - 1; i++)
		{
			var value = model.Values[i];
			WriteValue(value);
			builder.AppendLine(",");
		}
		WriteValue(model.Values[model.Values.Length - 1]);
		builder.AppendLine();
		builder.AppendLine("}");
	}

	private static void WriteUsage(StringBuilder builder, ArrayModel model)
	{
		builder.Append(model.Items.Name);
		builder.Append("[]");
	}

	private static void WriteDeclaration(StringBuilder builder, ArrayModel model)
	{
		builder.Append("public class ");
		builder.Append(model.Name);
		builder.Append(" : List<");
		WriteUsage(builder, model.Items);
		builder.AppendLine(">");
		builder.AppendLine("{");
		builder.AppendLine("}");
	}

	private static void WriteDeclaration(StringBuilder builder, ObjectModel model)
	{
		builder.Append("public class ");
		builder.AppendLine(model.Name);
		builder.AppendLine("{");
		foreach (var property in model.Properties)
		{
			builder.Append("\tpublic ");
			WriteUsage(builder, property.Type);
			builder.Append(" ");
			builder.Append(property.Name);
			builder.Append(" { ");
			if (property.CanRead)
				builder.Append("get; ");
			if (property.CanWrite)
				builder.Append("set; ");
			builder.AppendLine("}");
		}
		builder.AppendLine("}");
	}

	private static void WriteUsage(StringBuilder builder, DictionaryModel model)
	{
		builder.Append(model.Items.Name);
		builder.Append("<");
		WriteUsage(builder, model.Keys);
		builder.Append(", ");
		WriteUsage(builder, model.Items);
		builder.Append(">");
	}

	private static void WriteDeclaration(StringBuilder builder, DictionaryModel model)
	{
		builder.Append("public class ");
		builder.Append(model.Name);
		builder.Append(" : Dictionary<");
		WriteUsage(builder, model.Keys);
		builder.Append(", ");
		WriteUsage(builder, model.Items);
		builder.AppendLine(">");
		builder.AppendLine("{");
		builder.AppendLine("}");
	}
}