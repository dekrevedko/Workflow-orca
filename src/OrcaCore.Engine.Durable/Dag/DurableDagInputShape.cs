using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using OrcaCore.Core.Internal;
using OrcaCore.Internal;

namespace OrcaCore.Engine.Durable.Dag;

internal static class DurableDagInputShape
{
    private static readonly Lazy<JsonSerializerOptions> LegacyMetadata = new(() =>
    {
        // Metadata only. Business values still go through CoreWorkflowValueCodec.
        var options = new JsonSerializerOptions
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
            Converters =
            {
                new OrcaCore.Internal.AuthoredLocationJsonConverter(),
                new OrcaCore.Internal.FailureOccurrenceJsonConverter(),
                new OrcaCore.Internal.WorkflowFailureJsonConverter()
            }
        };
        options.MakeReadOnly();
        return options;
    });

    internal static string Digest(Type declaredType)
    {
        if (!CoreWorkflowValueCodec.IsSupportedDeclaredType(declaredType))
            throw new NotSupportedException($"DAG run input '{declaredType}' is not supported by the fixed codec.");

        var options = WorkflowJsonMetadataRegistry.Seal() ?? LegacyMetadata.Value;
        var record = new StringBuilder();
        Visit(declaredType, options, record, []);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(record.ToString())));
    }

    private static void Visit(Type type, JsonSerializerOptions options, StringBuilder record, HashSet<Type> ancestors)
    {
        if (!ancestors.Add(type))
            throw new NotSupportedException($"Recursive declared DAG run input '{type}' is not supported.");
        try
        {
            record.Append(Name(type)).Append('\n');
            if (Nullable.GetUnderlyingType(type) is { } underlying)
            {
                Visit(underlying, options, record, ancestors);
                return;
            }
            if (type.IsEnum)
            {
                record.Append(Name(type.GetEnumUnderlyingType())).Append('\n');
                foreach (var name in type.GetEnumNames().Order(StringComparer.Ordinal))
                    record.Append('"').Append(JsonEncodedText.Encode(name).ToString()).Append("\"\t")
                        .Append(Enum.Format(type, Enum.Parse(type, name), "D")).Append('\n');
                return;
            }

            var info = options.GetTypeInfo(type);
            if (info.PolymorphismOptions is not null || type == typeof(object) ||
                (info.Kind == JsonTypeInfoKind.Object && (type.IsAbstract || type.IsInterface)))
                throw new NotSupportedException($"Polymorphic declared DAG run input '{type}' is not supported.");
            if (info.KeyType is { } key) Visit(key, options, record, ancestors);
            if ((info.ElementType ?? type.GetElementType()) is { } element) Visit(element, options, record, ancestors);
            foreach (var property in info.Properties.OrderBy(property => property.Order))
            {
                if (property.Get is null) continue;
                var member = property.PropertyType;
                record.Append('"').Append(JsonEncodedText.Encode(property.Name).ToString()).Append("\"\t")
                    .Append(Name(member)).Append("\tnon-nullable-value=")
                    .Append(member.IsValueType && Nullable.GetUnderlyingType(member) is null ? "true\n" : "false\n");
                Visit(member, options, record, ancestors);
            }
        }
        finally
        {
            ancestors.Remove(type);
        }
    }

    private static string Name(Type type)
    {
        if (type.IsArray)
            return Name(type.GetElementType()!) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
        if (!type.IsGenericType)
            return (type.FullName ?? type.Name).Replace('+', '.');
        var definition = type.GetGenericTypeDefinition().FullName!.Replace('+', '.');
        return System.Text.RegularExpressions.Regex.Replace(definition, "\x60[0-9]+", "") +
            "<" + string.Join(',', type.GetGenericArguments().Select(Name)) + ">";
    }
}
