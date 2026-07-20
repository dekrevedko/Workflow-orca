using System.Reflection;

namespace OrcaCore.DeveloperSurface.Guards;

internal enum InterfaceTier
{
    Application,
    ProviderAuthoring,
    RuntimeProtocol,
    Internal
}

internal sealed record ClassifiedPublicType(Type Type, InterfaceTier Tier);

internal static class PublicSurfaceCatalog
{
    private static readonly string[] OrcaCoreAssemblyNames =
    [
        "OrcaCore.Abstractions",
        "OrcaCore.Core",
        "OrcaCore.Engine.Ephemeral",
        "OrcaCore.Engine.Durable",
        "OrcaCore.Hosting",
        "OrcaCore.Providers.InMemory",
        "OrcaCore.Providers.PostgreSql",
        "OrcaCore.Providers.RabbitMq",
        "OrcaCore.Providers.Redis",
        "OrcaCore.Providers.Relational",
        "OrcaCore.Providers.SqlServer",
        "OrcaCore.Providers.ZeroMq"
    ];

    private static readonly HashSet<string> RuntimeIdentityNames =
    [
        "BranchPlanId",
        "CommandId",
        "FiberId",
        "InstructionId",
        "OutboxRecordId",
        "ScopeId",
        "ScopePlanId",
        "StreamVersion",
        "TimerId"
    ];

    internal static IReadOnlyList<Assembly> Assemblies { get; } = OrcaCoreAssemblyNames
        .Select(Assembly.Load)
        .ToArray();

    internal static IReadOnlyList<ClassifiedPublicType> ExportedTypes { get; } = Assemblies
        .SelectMany(SafeExportedTypes)
        .OrderBy(type => type.Assembly.GetName().Name, StringComparer.Ordinal)
        .ThenBy(type => type.FullName, StringComparer.Ordinal)
        .Select(type => new ClassifiedPublicType(type, Classify(type)))
        .ToArray();

    internal static InterfaceTier Classify(Type type)
    {
        type = Normalize(type);
        var assemblyName = type.Assembly.GetName().Name ?? string.Empty;
        var ns = type.Namespace ?? string.Empty;

        if (assemblyName.StartsWith("OrcaCore.Providers.", StringComparison.Ordinal))
        {
            return InterfaceTier.ProviderAuthoring;
        }

        return assemblyName switch
        {
            "OrcaCore.Abstractions" when ns.StartsWith("OrcaCore.Abstractions.Providers", StringComparison.Ordinal)
                => InterfaceTier.ProviderAuthoring,
            "OrcaCore.Abstractions" when ns.StartsWith("OrcaCore.Abstractions.Durable", StringComparison.Ordinal)
                => InterfaceTier.RuntimeProtocol,
            "OrcaCore.Abstractions" when RuntimeIdentityNames.Contains(type.Name) ||
                                               RuntimeIdentityNames.Any(name => type.Name == $"{name}JsonConverter")
                => InterfaceTier.RuntimeProtocol,
            "OrcaCore.Abstractions" => InterfaceTier.Application,

            "OrcaCore.Core" when ns.StartsWith("OrcaCore.Core.Compilation", StringComparison.Ordinal)
                => InterfaceTier.Internal,
            "OrcaCore.Core" when type.Name.StartsWith("Compiled", StringComparison.Ordinal) ||
                                  type.Name.EndsWith("Plan", StringComparison.Ordinal) ||
                                  type.Name.EndsWith("Policy", StringComparison.Ordinal)
                => InterfaceTier.Internal,
            "OrcaCore.Core" => InterfaceTier.Application,

            "OrcaCore.Engine.Ephemeral" when ns.StartsWith("OrcaCore.Engine.Ephemeral.Execution", StringComparison.Ordinal)
                => InterfaceTier.Internal,
            "OrcaCore.Engine.Ephemeral" => InterfaceTier.Application,

            "OrcaCore.Engine.Durable" when ns.StartsWith("OrcaCore.Engine.Durable.Management", StringComparison.Ordinal)
                => InterfaceTier.Application,
            "OrcaCore.Engine.Durable" when type.Name is "DurableWorkflowRuntime" or
                                                            "DurableWorkflowStartResult" or
                                                            "DurableEventDeliveryResult" or
                                                            "DurableRearmRequest"
                => InterfaceTier.Application,
            "OrcaCore.Engine.Durable" => InterfaceTier.Internal,

            "OrcaCore.Hosting" when ns.StartsWith("OrcaCore.Hosting.Services", StringComparison.Ordinal)
                => InterfaceTier.Internal,
            "OrcaCore.Hosting" => InterfaceTier.Application,
            _ => throw new InvalidOperationException($"No Interface-tier rule exists for exported type '{type}'.")
        };
    }

    internal static IReadOnlyList<string> FindForbiddenSignatureEdges()
    {
        var edges = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var source in ExportedTypes)
        {
            foreach (var referenced in ReferencedPublicSignatureTypes(source.Type))
            {
                if (!IsOrcaCore(referenced))
                {
                    continue;
                }

                var targetTier = Classify(referenced);
                var forbidden = source.Tier switch
                {
                    InterfaceTier.Application => targetTier is not InterfaceTier.Application,
                    InterfaceTier.RuntimeProtocol => targetTier == InterfaceTier.ProviderAuthoring,
                    InterfaceTier.ProviderAuthoring => targetTier == InterfaceTier.Internal,
                    _ => false
                };
                if (forbidden)
                {
                    edges.Add($"{source.Tier}:{source.Type.FullName} -> {targetTier}:{Normalize(referenced).FullName}");
                }
            }
        }

        return edges.ToArray();
    }

    internal static IReadOnlyList<Type> FindCompilerIrSignatureTypes(Type declaringType) =>
        ReferencedPublicSignatureTypes(declaringType, includeInheritedMembers: true)
            .Where(IsCompilerIr)
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();

    private static IEnumerable<Type> ReferencedPublicSignatureTypes(
        Type declaringType,
        bool includeInheritedMembers = false)
    {
        var pending = new Stack<Type>();
        var seen = new HashSet<Type>();

        void Add(Type? type)
        {
            if (type is not null)
            {
                pending.Push(type);
            }
        }

        Add(declaringType.BaseType);
        foreach (var implemented in declaringType.GetInterfaces()) Add(implemented);
        foreach (var generic in declaringType.GetGenericArguments())
        {
            foreach (var constraint in generic.GetGenericParameterConstraints()) Add(constraint);
        }

        var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
        flags |= includeInheritedMembers ? BindingFlags.FlattenHierarchy : BindingFlags.DeclaredOnly;
        foreach (var constructor in declaringType.GetConstructors(flags))
        {
            foreach (var parameter in constructor.GetParameters()) Add(parameter.ParameterType);
        }

        foreach (var method in declaringType.GetMethods(flags))
        {
            Add(method.ReturnType);
            foreach (var parameter in method.GetParameters()) Add(parameter.ParameterType);
            foreach (var generic in method.GetGenericArguments())
            {
                foreach (var constraint in generic.GetGenericParameterConstraints()) Add(constraint);
            }
        }

        foreach (var property in declaringType.GetProperties(flags))
        {
            Add(property.PropertyType);
            foreach (var parameter in property.GetIndexParameters()) Add(parameter.ParameterType);
        }

        foreach (var field in declaringType.GetFields(flags)) Add(field.FieldType);
        foreach (var eventInfo in declaringType.GetEvents(flags)) Add(eventInfo.EventHandlerType);

        while (pending.TryPop(out var current))
        {
            if (current.IsByRef || current.IsPointer || current.IsArray)
            {
                Add(current.GetElementType());
                continue;
            }

            if (current.IsGenericParameter)
            {
                foreach (var constraint in current.GetGenericParameterConstraints()) Add(constraint);
                continue;
            }

            var normalized = Normalize(current);
            if (!seen.Add(normalized))
            {
                continue;
            }

            yield return normalized;
            if (current.IsGenericType)
            {
                foreach (var argument in current.GetGenericArguments()) Add(argument);
            }
        }
    }

    private static bool IsOrcaCore(Type type) =>
        (Normalize(type).Assembly.GetName().Name ?? string.Empty).StartsWith("OrcaCore.", StringComparison.Ordinal);

    private static bool IsCompilerIr(Type type)
    {
        type = Normalize(type);
        if (!IsOrcaCore(type))
        {
            return false;
        }

        var ns = type.Namespace ?? string.Empty;
        return ns.StartsWith("OrcaCore.Core.Compilation", StringComparison.Ordinal) ||
               type.Name.Contains("Compiled", StringComparison.Ordinal) ||
               type.Name.Contains("Compiler", StringComparison.Ordinal) ||
               type.Name.EndsWith("Plan", StringComparison.Ordinal);
    }

    private static Type Normalize(Type type) => type.IsGenericType ? type.GetGenericTypeDefinition() : type;

    private static Type[] SafeExportedTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetExportedTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.OfType<Type>().Where(type => type.IsPublic || type.IsNestedPublic).ToArray();
        }
    }
}
