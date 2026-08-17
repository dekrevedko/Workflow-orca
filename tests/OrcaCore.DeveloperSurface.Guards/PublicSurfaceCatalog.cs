using System.Reflection;
using System.Text.Json;

namespace OrcaCore.DeveloperSurface.Guards;

internal enum InterfaceTier
{
    Application,
    Internal,
    Engine,
    RuntimeProtocol,
    ProviderAuthoring,
    DurableHosting,
    Dag,
    DagHosting,
    Companion
}

internal sealed record ClassifiedPublicType(Type Type, InterfaceTier Tier);
internal sealed record TargetAssembly(string Name, InterfaceTier Tier);

internal static class PublicSurfaceCatalog
{
    internal static IReadOnlyList<TargetAssembly> TargetAssemblies { get; } = ReadTargets();
    internal static IReadOnlyList<string> TargetAssemblyNames { get; } = TargetAssemblies.Select(x => x.Name).ToArray();
    internal static IReadOnlyList<InterfaceTier> TargetAudienceTiers { get; } = ReadAudienceTiers();
    internal static IReadOnlyDictionary<string, InterfaceTier> TargetCompanionFixtures { get; } =
        ReadCompanionFixtures().ToDictionary(x => x, _ => InterfaceTier.Companion, StringComparer.Ordinal);

    internal static IReadOnlyList<Assembly> Assemblies { get; } = LoadTargetAssemblies();

    internal static IReadOnlyList<ClassifiedPublicType> ExportedTypes { get; } = Assemblies
        .SelectMany(SafeExportedTypes)
        .OrderBy(type => type.Assembly.GetName().Name, StringComparer.Ordinal)
        .ThenBy(type => type.FullName, StringComparer.Ordinal)
        .Select(type => new ClassifiedPublicType(type, Classify(type)))
        .ToArray();

    internal static InterfaceTier Classify(Type type)
    {
        var assemblyName = Normalize(type).Assembly.GetName().Name ?? string.Empty;
        return TargetAssemblies.SingleOrDefault(x => x.Name == assemblyName)?.Tier
            ?? throw new InvalidOperationException($"Assembly '{assemblyName}' is not in the frozen v1 target inventory.");
    }

    internal static IReadOnlyList<string> FindForbiddenSignatureEdges()
    {
        var edges = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var source in ExportedTypes)
        {
            foreach (var referenced in ReferencedPublicSignatureTypes(source.Type))
            {
                var targetName = Normalize(referenced).Assembly.GetName().Name ?? string.Empty;
                var target = TargetAssemblies.SingleOrDefault(x => x.Name == targetName);
                if (target is null) continue;

                var forbidden = source.Tier switch
                {
                    InterfaceTier.Application => target.Tier is not InterfaceTier.Application,
                    InterfaceTier.Dag => target.Tier is not InterfaceTier.Application and not InterfaceTier.Dag,
                    InterfaceTier.RuntimeProtocol => target.Tier == InterfaceTier.ProviderAuthoring,
                    InterfaceTier.ProviderAuthoring => target.Tier is InterfaceTier.Engine or InterfaceTier.Internal or InterfaceTier.DurableHosting,
                    InterfaceTier.DagHosting => target.Tier is InterfaceTier.ProviderAuthoring or InterfaceTier.RuntimeProtocol or InterfaceTier.Internal,
                    _ => false
                };
                if (forbidden)
                    edges.Add($"{source.Tier}:{source.Type.FullName} -> {target.Tier}:{Normalize(referenced).FullName}");
            }
        }
        return edges.ToArray();
    }

    internal static IReadOnlyList<Type> FindCompilerIrSignatureTypes(Type declaringType) =>
        ReferencedPublicSignatureTypes(declaringType, includeInheritedMembers: true)
            .Where(IsCompilerIr)
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<TargetAssembly> ReadTargets()
    {
        var path = Path.Combine(FixtureDefinitions.RepositoryRoot(), "tests", "OrcaCore.DeveloperSurface.Guards",
            "Fixtures", "v1-public-contract.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.GetProperty("packages").EnumerateArray()
            .Select(package => new TargetAssembly(
                package.GetProperty("id").GetString()!,
                Enum.Parse<InterfaceTier>(package.GetProperty("tier").GetString()!, ignoreCase: false)))
            .ToArray();
    }

    private static IReadOnlyList<Assembly> LoadTargetAssemblies() => TargetAssemblyNames
        .Select(name => TryLoadTargetAssembly(name)
            ?? throw new InvalidOperationException(
                $"Target assembly '{name}' could not be loaded; the exact manifest cannot be inspected partially."))
        .ToArray();

    private static IReadOnlyList<InterfaceTier> ReadAudienceTiers()
    {
        var path = Path.Combine(FixtureDefinitions.RepositoryRoot(), "tests", "OrcaCore.DeveloperSurface.Guards",
            "Fixtures", "v1-public-contract.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.GetProperty("audienceTiers").EnumerateArray()
            .Select(value => Enum.Parse<InterfaceTier>(value.GetString()!, ignoreCase: false)).ToArray();
    }

    private static IReadOnlyList<string> ReadCompanionFixtures()
    {
        var path = Path.Combine(FixtureDefinitions.RepositoryRoot(), "tests", "OrcaCore.DeveloperSurface.Guards",
            "Fixtures", "v1-public-contract.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.GetProperty("companionFixtures").EnumerateArray()
            .Select(value => value.GetString()!).ToArray();
    }

    private static Assembly? TryLoadTargetAssembly(string name)
    {
        var loaded = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(x => x.GetName().Name == name);
        if (loaded is not null) return loaded;
        try { return Assembly.Load(name); }
        catch (FileNotFoundException) { }

        var project = Directory.GetFiles(Path.Combine(FixtureDefinitions.RepositoryRoot(), "src"), $"{name}.csproj", SearchOption.AllDirectories)
            .SingleOrDefault();
        if (project is null) return null;
        var dll = Directory.GetFiles(Path.GetDirectoryName(project)!, $"{name}.dll", SearchOption.AllDirectories)
            .Where(path => path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
        return dll is null ? null : Assembly.LoadFrom(dll);
    }

    private static IEnumerable<Type> ReferencedPublicSignatureTypes(Type declaringType, bool includeInheritedMembers = false)
    {
        var pending = new Stack<Type>();
        var seen = new HashSet<Type>();
        void Add(Type? type) { if (type is not null) pending.Push(type); }

        Add(declaringType.BaseType);
        foreach (var implemented in declaringType.GetInterfaces()) Add(implemented);
        foreach (var generic in declaringType.GetGenericArguments())
            foreach (var constraint in generic.GetGenericParameterConstraints()) Add(constraint);

        var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
        flags |= includeInheritedMembers ? BindingFlags.FlattenHierarchy : BindingFlags.DeclaredOnly;
        foreach (var constructor in declaringType.GetConstructors(flags))
            foreach (var parameter in constructor.GetParameters()) Add(parameter.ParameterType);
        foreach (var method in declaringType.GetMethods(flags))
        {
            Add(method.ReturnType);
            foreach (var parameter in method.GetParameters()) Add(parameter.ParameterType);
            foreach (var generic in method.GetGenericArguments())
                foreach (var constraint in generic.GetGenericParameterConstraints()) Add(constraint);
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
            if (current.IsByRef || current.IsPointer || current.IsArray) { Add(current.GetElementType()); continue; }
            if (current.IsGenericParameter)
            {
                foreach (var constraint in current.GetGenericParameterConstraints()) Add(constraint);
                continue;
            }
            var normalized = Normalize(current);
            if (!seen.Add(normalized)) continue;
            yield return normalized;
            if (current.IsGenericType)
                foreach (var argument in current.GetGenericArguments()) Add(argument);
        }
    }

    private static bool IsCompilerIr(Type type)
    {
        type = Normalize(type);
        var ns = type.Namespace ?? string.Empty;
        return type.Assembly.GetName().Name == "OrcaCore.Core" &&
               (ns.StartsWith("OrcaCore.Core.Compilation", StringComparison.Ordinal) ||
                type.Name.Contains("Compiled", StringComparison.Ordinal) ||
                type.Name.Contains("Compiler", StringComparison.Ordinal) || type.Name.EndsWith("Plan", StringComparison.Ordinal));
    }

    private static Type Normalize(Type type) => type.IsGenericType ? type.GetGenericTypeDefinition() : type;

    private static Type[] SafeExportedTypes(Assembly assembly)
    {
        try { return assembly.GetExportedTypes(); }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.OfType<Type>().Where(type => type.IsPublic || type.IsNestedPublic).ToArray();
        }
    }
}
