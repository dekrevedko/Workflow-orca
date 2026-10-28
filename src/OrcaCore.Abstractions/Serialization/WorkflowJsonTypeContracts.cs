using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace OrcaCore.Internal;

internal sealed class WorkflowJsonContractException(string code, string message)
    : NotSupportedException(message)
{
    internal string DiagnosticCode { get; } = code;
}

internal static class WorkflowJsonTypeContracts
{
    internal static JsonTypeInfo Validate(Type root, Type context, JsonSerializerOptions options)
    {
        var visited = new HashSet<Type>();
        return Visit(root);

        JsonTypeInfo Visit(Type type)
        {
            JsonTypeInfo info;
            try { info = options.GetTypeInfo(type); }
            catch (NotSupportedException)
            {
                throw new WorkflowJsonContractException("SFE-TYPE-003",
                    $"Generated workflow metadata for root '{root.FullName}' is missing type '{type.FullName}' in context '{context.FullName}'. Add that finite closure to the source-generated Metadata context and restart before workflow use.");
            }
            info.MakeReadOnly();
            if (!visited.Add(type)) return info;
            if (Nullable.GetUnderlyingType(type) is { } underlying) Visit(underlying);
            if (info.ElementType is { } element) Visit(element);
            if (info.KeyType is { } key) Visit(key);
            if (info.PolymorphismOptions is { } polymorphism)
                foreach (var derived in polymorphism.DerivedTypes) Visit(derived.DerivedType);
            foreach (var property in info.Properties)
            {
                if (property.AssociatedParameter?.IsMemberInitializer == true)
                {
                    var provider = property.AttributeProvider;
                    if (provider is null)
                        throw new WorkflowJsonContractException("SFE-TYPE-002",
                            $"Generated workflow root '{root.FullName}' cannot prove omission metadata for '{type.FullName}.{property.Name}' in context '{context.FullName}'. Preserve the generated member attributes; native qualification is required.");
                    var ignore = provider.GetCustomAttributes(typeof(JsonIgnoreAttribute), inherit: false)
                        .Cast<JsonIgnoreAttribute>().SingleOrDefault();
                    if (ignore is not null && ignore.Condition != JsonIgnoreCondition.Never)
                        throw new WorkflowJsonContractException("SFE-TYPE-002",
                            $"Generated .NET 10 omission restriction: root '{root.FullName}' contains '{type.FullName}.{property.Name}' with an omittable synthetic member initializer in context '{context.FullName}'. Use an ordinary setter, a genuine JSON-constructor-bound member, or remove the omission policy.");
                }
                if (property.Get is not null || property.Set is not null || property.AssociatedParameter is not null)
                    Visit(property.PropertyType);
            }
            return info;
        }
    }
}
