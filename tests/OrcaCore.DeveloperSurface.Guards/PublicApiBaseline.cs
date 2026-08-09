using System.Collections;
using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;

namespace OrcaCore.DeveloperSurface.Guards;

internal static class PublicApiBaseline
{
    internal const int FormatVersion = 1;
    internal const string CandidateDirectoryVariable = "ORCACORE_PUBLIC_API_CANDIDATE_DIR";
    internal const string PackageFeedVariable = "ORCACORE_PUBLIC_API_PACKAGE_FEED";
    private const int ExtractionCleanupAttemptLimit = 3;

    private static readonly HashSet<string> SignatureAttributeNames = new(StringComparer.Ordinal)
    {
        "System.Diagnostics.CodeAnalysis.AllowNullAttribute",
        "System.Diagnostics.CodeAnalysis.DisallowNullAttribute",
        "System.Diagnostics.CodeAnalysis.DoesNotReturnAttribute",
        "System.Diagnostics.CodeAnalysis.DoesNotReturnIfAttribute",
        "System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute",
        "System.Diagnostics.CodeAnalysis.MaybeNullAttribute",
        "System.Diagnostics.CodeAnalysis.MaybeNullWhenAttribute",
        "System.Diagnostics.CodeAnalysis.MemberNotNullAttribute",
        "System.Diagnostics.CodeAnalysis.MemberNotNullWhenAttribute",
        "System.Diagnostics.CodeAnalysis.NotNullAttribute",
        "System.Diagnostics.CodeAnalysis.NotNullIfNotNullAttribute",
        "System.Diagnostics.CodeAnalysis.NotNullWhenAttribute",
        "System.Diagnostics.CodeAnalysis.RequiresAssemblyFilesAttribute",
        "System.Diagnostics.CodeAnalysis.RequiresDynamicCodeAttribute",
        "System.Diagnostics.CodeAnalysis.RequiresUnreferencedCodeAttribute",
        "System.Diagnostics.CodeAnalysis.StringSyntaxAttribute",
        "System.Diagnostics.CodeAnalysis.UnscopedRefAttribute",
        "System.ObsoleteAttribute",
        "System.ParamArrayAttribute",
        "System.Runtime.CompilerServices.DynamicAttribute",
        "System.Runtime.CompilerServices.ExtensionAttribute",
        "System.Runtime.CompilerServices.IsReadOnlyAttribute",
        "System.Runtime.CompilerServices.IsUnmanagedAttribute",
        "System.Runtime.CompilerServices.NativeIntegerAttribute",
        "System.Runtime.CompilerServices.NullableAttribute",
        "System.Runtime.CompilerServices.RequiredMemberAttribute",
        "System.Runtime.CompilerServices.ScopedRefAttribute",
        "System.Runtime.CompilerServices.SetsRequiredMembersAttribute",
        "System.Runtime.CompilerServices.TupleElementNamesAttribute"
    };

    internal static IReadOnlyDictionary<string, string> CaptureCurrent()
    {
        var paths = PublicSurfaceCatalog.TargetAssemblyNames.ToDictionary(
            name => name,
            name => Path.Combine(AppContext.BaseDirectory, $"{name}.dll"),
            StringComparer.Ordinal);
        return Capture(paths);
    }

    internal static string CaptureAssemblyForTesting(Assembly assembly) => FormatAssembly(assembly);

    internal static IReadOnlyDictionary<string, string> CapturePackages(string feed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(feed);
        var extractionRoot = Path.Combine(
            Path.GetTempPath(),
            "orcacore-public-api",
            $"{Environment.ProcessId}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(extractionRoot);

        try
        {
            return Capture(ExtractPackageAssemblies(feed, extractionRoot));
        }
        finally
        {
            DeleteExtractionRoot(extractionRoot);
        }
    }

    internal static string BaselineDirectory() => Path.Combine(
        FixtureDefinitions.RepositoryRoot(),
        "tests",
        "OrcaCore.DeveloperSurface.Guards",
        "Fixtures",
        "PublicApi",
        "v1");

    internal static IReadOnlyDictionary<string, string> ReadApproved() =>
        ReadApproved(BaselineDirectory());

    internal static IReadOnlyDictionary<string, string> ReadApproved(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (!Directory.Exists(directory))
            throw new InvalidOperationException($"Approved public API baseline directory is missing: '{directory}'.");

        var expectedNames = PublicSurfaceCatalog.TargetAssemblyNames.Order(StringComparer.Ordinal).ToArray();
        var actualNames = Directory.EnumerateFiles(directory, "*.api.txt")
            .Select(Path.GetFileNameWithoutExtension)
            .Select(name => name![..^4])
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (!actualNames.SequenceEqual(expectedNames, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"Public API baseline coverage mismatch. Expected [{string.Join(", ", expectedNames)}], " +
                $"found [{string.Join(", ", actualNames)}].");
        }

        return expectedNames.ToDictionary(
            name => name,
            name => ReadCanonicalFile(Path.Combine(directory, $"{name}.api.txt")),
            StringComparer.Ordinal);
    }

    internal static string Diff(
        IReadOnlyDictionary<string, string> expected,
        IReadOnlyDictionary<string, string> actual)
    {
        var findings = new List<string>();
        foreach (var assemblyName in PublicSurfaceCatalog.TargetAssemblyNames.Order(StringComparer.Ordinal))
        {
            if (!expected.TryGetValue(assemblyName, out var expectedText))
            {
                findings.Add($"{assemblyName}: approved baseline is missing.");
                continue;
            }

            if (!actual.TryGetValue(assemblyName, out var actualText))
            {
                findings.Add($"{assemblyName}: product assembly is missing.");
                continue;
            }

            if (string.Equals(expectedText, actualText, StringComparison.Ordinal))
                continue;

            var expectedLines = expectedText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var actualLines = actualText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var missing = expectedLines.Except(actualLines, StringComparer.Ordinal).ToArray();
            var unexpected = actualLines.Except(expectedLines, StringComparer.Ordinal).ToArray();
            findings.Add($"{assemblyName}: public API differs from the approved baseline.");
            findings.AddRange(missing.Select(line => $"  - {line}"));
            findings.AddRange(unexpected.Select(line => $"  + {line}"));
            if (missing.Length == 0 && unexpected.Length == 0)
                findings.Add("  ! declaration ordering or duplication changed");
        }

        return string.Join(Environment.NewLine, findings);
    }

    internal static void WriteCandidates(
        IReadOnlyDictionary<string, string> captured,
        string destination)
    {
        ArgumentNullException.ThrowIfNull(captured);
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        var approved = Path.GetFullPath(BaselineDirectory()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var requested = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (requested.StartsWith(approved, StringComparison.OrdinalIgnoreCase) ||
            approved.StartsWith(requested, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Candidate capture refuses to write into or above the checked-in approved baseline directory.");
        }

        var expectedNames = PublicSurfaceCatalog.TargetAssemblyNames.Order(StringComparer.Ordinal).ToArray();
        var capturedNames = captured.Keys.Order(StringComparer.Ordinal).ToArray();
        if (!capturedNames.SequenceEqual(expectedNames, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"Candidate public API coverage mismatch. Expected [{string.Join(", ", expectedNames)}], " +
                $"found [{string.Join(", ", capturedNames)}].");
        }

        if (Directory.Exists(requested) && Directory.EnumerateFileSystemEntries(requested).Any())
        {
            throw new InvalidOperationException(
                $"Candidate capture destination must be absent or empty: '{requested}'.");
        }

        Directory.CreateDirectory(requested);
        foreach (var pair in captured.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            File.WriteAllText(Path.Combine(requested, $"{pair.Key}.api.txt"), pair.Value, new UTF8Encoding(false));
    }

    internal static IReadOnlyList<string> FindForbiddenInternalPlaceholders(
        IReadOnlyDictionary<string, string>? assemblyPaths = null)
    {
        assemblyPaths ??= PublicSurfaceCatalog.TargetAssemblyNames.ToDictionary(
            name => name,
            name => Path.Combine(AppContext.BaseDirectory, $"{name}.dll"),
            StringComparer.Ordinal);
        var forbidden = RemovedInternalPlaceholderCatalog.All
            .ToLookup(item => item.Assembly, item => item.MetadataName, StringComparer.Ordinal);
        var findings = new List<string>();

        using var context = new ProductAssemblyLoadContext(assemblyPaths);
        foreach (var pair in assemblyPaths.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (!File.Exists(pair.Value))
                throw new InvalidOperationException($"Target assembly is missing: '{pair.Value}'.");
            var assembly = context.LoadFromAssemblyPath(Path.GetFullPath(pair.Value));
            var forbiddenNames = forbidden[pair.Key].ToHashSet(StringComparer.Ordinal);
            findings.AddRange(SafeTypes(assembly)
                .Where(type => type.FullName is not null && forbiddenNames.Contains(type.FullName))
                .Select(type => $"{pair.Key}::{type.FullName}"));
        }

        return findings.Order(StringComparer.Ordinal).ToArray();
    }

    internal static IReadOnlyList<string> FindForbiddenInternalPlaceholdersInPackages(string feed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(feed);
        var extractionRoot = Path.Combine(
            Path.GetTempPath(),
            "orcacore-internal-api",
            $"{Environment.ProcessId}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(extractionRoot);
        try
        {
            return FindForbiddenInternalPlaceholders(ExtractPackageAssemblies(feed, extractionRoot));
        }
        finally
        {
            DeleteExtractionRoot(extractionRoot);
        }
    }

    internal static IReadOnlyList<string> FindForbiddenPublicSymbols(IEnumerable<string> identities)
    {
        var paths = PublicSurfaceCatalog.TargetAssemblyNames.ToDictionary(
            name => name,
            name => Path.Combine(AppContext.BaseDirectory, $"{name}.dll"),
            StringComparer.Ordinal);
        return FindForbiddenPublicSymbols(paths, identities);
    }

    internal static IReadOnlyList<string> FindForbiddenPublicSymbolsInPackages(
        string feed,
        IEnumerable<string> identities)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(feed);
        var extractionRoot = Path.Combine(
            Path.GetTempPath(),
            "orcacore-forbidden-api",
            $"{Environment.ProcessId}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(extractionRoot);
        try
        {
            return FindForbiddenPublicSymbols(ExtractPackageAssemblies(feed, extractionRoot), identities);
        }
        finally
        {
            DeleteExtractionRoot(extractionRoot);
        }
    }

    internal static IReadOnlyList<string> FindForbiddenMetadataTypes(IEnumerable<string> identities)
    {
        var paths = PublicSurfaceCatalog.TargetAssemblyNames.ToDictionary(
            name => name,
            name => Path.Combine(AppContext.BaseDirectory, $"{name}.dll"),
            StringComparer.Ordinal);
        return FindForbiddenMetadataTypes(paths, identities);
    }

    internal static IReadOnlyList<string> FindForbiddenMetadataTypesInPackages(
        string feed,
        IEnumerable<string> identities)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(feed);
        var extractionRoot = Path.Combine(
            Path.GetTempPath(),
            "orcacore-forbidden-metadata",
            $"{Environment.ProcessId}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(extractionRoot);
        try
        {
            return FindForbiddenMetadataTypes(ExtractPackageAssemblies(feed, extractionRoot), identities);
        }
        finally
        {
            DeleteExtractionRoot(extractionRoot);
        }
    }

    private static IReadOnlyList<string> FindForbiddenPublicSymbols(
        IReadOnlyDictionary<string, string> paths,
        IEnumerable<string> identities)
    {
        using var context = new ProductAssemblyLoadContext(paths);
        var assemblies = paths.ToDictionary(
            pair => pair.Key,
            pair => context.LoadFromAssemblyPath(Path.GetFullPath(pair.Value)),
            StringComparer.Ordinal);
        var findings = new List<string>();
        foreach (var identity in identities.Order(StringComparer.Ordinal))
        {
            var parts = identity.Split("::", StringSplitOptions.None);
            if (parts.Length is < 2 or > 3)
                throw new InvalidOperationException($"Invalid forbidden public symbol identity '{identity}'.");
            if (!assemblies.TryGetValue(parts[0], out var assembly))
                throw new InvalidOperationException($"Forbidden public symbol '{identity}' names an unknown assembly.");
            var type = assembly.GetType(parts[1], throwOnError: false, ignoreCase: false);
            if (type is null || !IsExternallyVisible(type)) continue;
            if (parts.Length == 2)
            {
                findings.Add(identity);
                continue;
            }

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic |
                                       BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            if (type.GetMember(parts[2], flags).Any(IsExternallyVisible)) findings.Add(identity);
        }
        return findings;
    }

    private static IReadOnlyList<string> FindForbiddenMetadataTypes(
        IReadOnlyDictionary<string, string> paths,
        IEnumerable<string> identities)
    {
        ArgumentNullException.ThrowIfNull(identities);
        using var context = new ProductAssemblyLoadContext(paths);
        var assemblies = paths.ToDictionary(
            pair => pair.Key,
            pair => context.LoadFromAssemblyPath(Path.GetFullPath(pair.Value)),
            StringComparer.Ordinal);
        var findings = new List<string>();
        foreach (var identity in identities.Order(StringComparer.Ordinal))
        {
            var parts = identity.Split("::", StringSplitOptions.None);
            if (parts.Length != 2)
                throw new InvalidOperationException($"Invalid forbidden metadata type identity '{identity}'.");
            if (!assemblies.TryGetValue(parts[0], out var assembly))
                throw new InvalidOperationException($"Forbidden metadata type '{identity}' names an unknown assembly.");
            if (assembly.GetType(parts[1], throwOnError: false, ignoreCase: false) is not null)
                findings.Add(identity);
        }

        return findings;
    }

    private static IReadOnlyDictionary<string, string> Capture(IReadOnlyDictionary<string, string> paths)
    {
        var expectedNames = PublicSurfaceCatalog.TargetAssemblyNames.Order(StringComparer.Ordinal).ToArray();
        var actualNames = paths.Keys.Order(StringComparer.Ordinal).ToArray();
        if (!actualNames.SequenceEqual(expectedNames, StringComparer.Ordinal))
            throw new InvalidOperationException("Public API input assembly coverage does not match the frozen package inventory.");
        foreach (var path in paths.Values)
            if (!File.Exists(path)) throw new InvalidOperationException($"Public API input assembly is missing: '{path}'.");

        using var context = new ProductAssemblyLoadContext(paths);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in paths.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var assembly = context.LoadFromAssemblyPath(Path.GetFullPath(pair.Value));
            var actualName = assembly.GetName().Name;
            if (!string.Equals(actualName, pair.Key, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Assembly path '{pair.Value}' contains '{actualName}', expected '{pair.Key}'.");
            }

            result.Add(pair.Key, FormatAssembly(assembly));
        }

        return result;
    }

    private static void DeleteExtractionRoot(string extractionRoot)
    {
        for (var attempt = 1; attempt <= ExtractionCleanupAttemptLimit; attempt++)
        {
            try
            {
                Directory.Delete(extractionRoot, recursive: true);
                return;
            }
            catch (UnauthorizedAccessException) when (attempt < ExtractionCleanupAttemptLimit)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }
    }

    private static IReadOnlyDictionary<string, string> ExtractPackageAssemblies(string feed, string extractionRoot)
    {
        var version = FixtureDefinitions.Read<PublicContractPackageVersion>(
            "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/v1-public-contract.json").PackageVersion;
        var paths = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var assemblyName in PublicSurfaceCatalog.TargetAssemblyNames)
        {
            var package = Path.Combine(feed, $"{assemblyName}.{version}.nupkg");
            if (!File.Exists(package))
                throw new InvalidOperationException($"Public API package is missing: '{package}'.");

            using var archive = ZipFile.OpenRead(package);
            var candidates = archive.Entries
                .Where(entry => entry.FullName.StartsWith("lib/", StringComparison.Ordinal) &&
                                entry.FullName.EndsWith($"/{assemblyName}.dll", StringComparison.Ordinal))
                .ToArray();
            if (candidates.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Package '{package}' must contain exactly one lib/*/{assemblyName}.dll; found {candidates.Length}.");
            }

            var target = Path.Combine(extractionRoot, $"{assemblyName}.dll");
            candidates[0].ExtractToFile(target);
            paths.Add(assemblyName, target);
        }
        return paths;
    }

    private static string FormatAssembly(Assembly assembly)
    {
        var assemblyName = assembly.GetName().Name!;
        var builder = new StringBuilder();
        builder.Append("# orcacore-public-api-v").Append(FormatVersion).Append('\n');
        builder.Append("assembly ").Append(assemblyName).Append('\n');
        foreach (var forwarded in assembly.GetForwardedTypes().OrderBy(MetadataName, StringComparer.Ordinal))
            builder.Append("forward type ").Append(MetadataName(forwarded)).Append(" -> ")
                .Append(forwarded.Assembly.GetName().Name).Append('\n');
        foreach (var type in SafeTypes(assembly).Where(IsExternallyVisible).OrderBy(MetadataName, StringComparer.Ordinal))
        {
            builder.Append(FormatTypeDeclaration(type)).Append('\n');
            foreach (var generic in type.GetGenericArguments().Where(argument => argument.DeclaringType == type))
                builder.Append("  generic ").Append(FormatGenericParameter(generic)).Append('\n');
            foreach (var member in FormatDeclaredMembers(type).Order(StringComparer.Ordinal))
                builder.Append("  ").Append(member).Append('\n');
        }

        return builder.ToString();
    }

    private static string FormatTypeDeclaration(Type type)
    {
        var modifiers = new List<string> { TypeAccessibility(type) };
        if (type.IsByRefLike) modifiers.Add("ref");
        if (type.IsValueType && type.IsDefined(typeof(IsReadOnlyAttribute), inherit: false)) modifiers.Add("readonly");
        if (type.IsClass && type.IsAbstract && type.IsSealed) modifiers.Add("static");
        else
        {
            if (type.IsAbstract && !type.IsInterface) modifiers.Add("abstract");
            if (type.IsSealed && !type.IsValueType) modifiers.Add("sealed");
        }
        modifiers.Add(TypeKind(type));

        var declaration = $"type {string.Join(' ', modifiers)} {MetadataName(type)}";
        if (type.IsEnum)
            declaration += $" : {ScopedMetadataName(Enum.GetUnderlyingType(type))}";
        else if (type.BaseType is not null && type.BaseType != typeof(object) && !IsDelegate(type))
            declaration += $" : {FormatSignatureType(type.BaseType, null)}";
        var interfaces = DirectInterfaces(type).Select(interfaceType => FormatSignatureType(interfaceType, null))
            .Order(StringComparer.Ordinal).ToArray();
        if (interfaces.Length > 0)
            declaration += $" interfaces [{string.Join(", ", interfaces)}]";
        var attributes = FormatSignatureAttributes(type.CustomAttributes);
        return attributes.Length == 0 ? declaration : $"{declaration} attributes [{attributes}]";
    }

    private static IEnumerable<string> FormatDeclaredMembers(Type type)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic |
                                   BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        var nullability = new NullabilityInfoContext();
        var accessorMethods = new HashSet<MethodInfo>();

        foreach (var property in type.GetProperties(flags))
        {
            if (property.GetMethod is not null) accessorMethods.Add(property.GetMethod);
            if (property.SetMethod is not null) accessorMethods.Add(property.SetMethod);
            if (!IsExternallyVisible(property.GetMethod) && !IsExternallyVisible(property.SetMethod)) continue;
            yield return FormatProperty(property, nullability);
        }

        foreach (var eventInfo in type.GetEvents(flags))
        {
            if (eventInfo.AddMethod is not null) accessorMethods.Add(eventInfo.AddMethod);
            if (eventInfo.RemoveMethod is not null) accessorMethods.Add(eventInfo.RemoveMethod);
            if (eventInfo.RaiseMethod is not null) accessorMethods.Add(eventInfo.RaiseMethod);
            if (!IsExternallyVisible(eventInfo.AddMethod) && !IsExternallyVisible(eventInfo.RemoveMethod)) continue;
            yield return FormatEvent(eventInfo, nullability);
        }

        foreach (var constructor in type.GetConstructors(flags).Where(IsExternallyVisible))
            yield return FormatConstructor(constructor, nullability);
        foreach (var field in type.GetFields(flags).Where(IsExternallyVisible))
            yield return FormatField(field, nullability);
        foreach (var method in type.GetMethods(flags).Where(IsExternallyVisible))
        {
            if (accessorMethods.Contains(method)) continue;
            yield return FormatMethod(method, nullability);
        }
    }

    private static string FormatConstructor(ConstructorInfo constructor, NullabilityInfoContext nullability)
    {
        var modifiers = MethodModifiers(constructor);
        var parameters = constructor.GetParameters().Select(parameter => FormatParameter(parameter, nullability));
        var attributes = FormatSignatureAttributes(constructor.CustomAttributes);
        return $"ctor {modifiers} .ctor({string.Join(", ", parameters)}){AttributeSuffix(attributes)}";
    }

    private static string FormatMethod(MethodInfo method, NullabilityInfoContext nullability)
    {
        var kind = method.IsSpecialName && method.Name.StartsWith("op_", StringComparison.Ordinal)
            ? "operator"
            : "method";
        var modifiers = MethodModifiers(method);
        var genericArguments = method.IsGenericMethodDefinition
            ? $"<{string.Join(", ", method.GetGenericArguments().Select(argument => argument.Name))}>"
            : string.Empty;
        var parameters = method.GetParameters().Select(parameter => FormatParameter(parameter, nullability));
        var returnType = FormatSignatureType(method.ReturnType, nullability.Create(method.ReturnParameter));
        var returnModifiers = FormatCustomModifiers(method.ReturnParameter);
        var returnAttributes = FormatSignatureAttributes(method.ReturnParameter.CustomAttributes);
        var constraints = method.IsGenericMethodDefinition
            ? " where [" + string.Join("; ", method.GetGenericArguments().Select(FormatGenericParameter)) + "]"
            : string.Empty;
        var attributes = FormatSignatureAttributes(method.CustomAttributes);
        var returnAttributeSuffix = returnAttributes.Length == 0
            ? string.Empty
            : $" return-attributes [{returnAttributes}]";
        return $"{kind} {modifiers} {method.Name}{genericArguments}({string.Join(", ", parameters)}) -> {returnType}{returnModifiers}{returnAttributeSuffix}{constraints}{AttributeSuffix(attributes)}";
    }

    private static string FormatProperty(PropertyInfo property, NullabilityInfoContext nullability)
    {
        var index = property.GetIndexParameters();
        var name = index.Length == 0
            ? property.Name
            : $"{property.Name}[{string.Join(", ", index.Select(parameter => FormatParameter(parameter, nullability)))}]";
        var accessors = new List<string>();
        if (IsExternallyVisible(property.GetMethod))
            accessors.Add(FormatGetter(property.GetMethod!));
        if (IsExternallyVisible(property.SetMethod))
            accessors.Add(FormatSetter(property.SetMethod!));
        var propertyType = FormatSignatureType(property.PropertyType, nullability.Create(property));
        var propertyModifiers = FormatCustomModifiers(property.GetRequiredCustomModifiers(), property.GetOptionalCustomModifiers());
        var attributes = FormatSignatureAttributes(property.CustomAttributes);
        return $"property {propertyType}{propertyModifiers} {name} {{ {string.Join("; ", accessors)} }}{AttributeSuffix(attributes)}";
    }

    private static string FormatEvent(EventInfo eventInfo, NullabilityInfoContext nullability)
    {
        var accessors = new List<string>();
        if (IsExternallyVisible(eventInfo.AddMethod)) accessors.Add(FormatEventAccessor("add", eventInfo.AddMethod!));
        if (IsExternallyVisible(eventInfo.RemoveMethod)) accessors.Add(FormatEventAccessor("remove", eventInfo.RemoveMethod!));
        var eventType = FormatSignatureType(eventInfo.EventHandlerType!, nullability.Create(eventInfo));
        var attributes = FormatSignatureAttributes(eventInfo.CustomAttributes);
        return $"event {eventType} {eventInfo.Name} {{ {string.Join("; ", accessors)} }}{AttributeSuffix(attributes)}";
    }

    private static string FormatField(FieldInfo field, NullabilityInfoContext nullability)
    {
        var modifiers = new List<string> { FieldAccessibility(field) };
        if (field.IsLiteral) modifiers.Add("const");
        else
        {
            if (field.IsStatic) modifiers.Add("static");
            if (field.IsInitOnly) modifiers.Add("readonly");
        }
        var type = FormatSignatureType(field.FieldType, nullability.Create(field));
        var customModifiers = FormatCustomModifiers(field.GetRequiredCustomModifiers(), field.GetOptionalCustomModifiers());
        var value = field.IsLiteral ? $" = {FormatConstant(field.GetRawConstantValue())}" : string.Empty;
        var attributes = FormatSignatureAttributes(field.CustomAttributes);
        return $"field {string.Join(' ', modifiers)} {type}{customModifiers} {field.Name}{value}{AttributeSuffix(attributes)}";
    }

    private static string FormatParameter(ParameterInfo parameter, NullabilityInfoContext nullability)
    {
        var type = parameter.ParameterType;
        var passing = string.Empty;
        if (type.IsByRef)
        {
            passing = parameter.IsOut ? "out " : parameter.IsIn ? "in " : "ref ";
            type = type.GetElementType()!;
        }
        var paramsPrefix = parameter.IsDefined(typeof(ParamArrayAttribute), inherit: false) ? "params " : string.Empty;
        var formatted = $"{paramsPrefix}{passing}{FormatSignatureType(type, nullability.Create(parameter))} {parameter.Name ?? "_"}";
        if (parameter.HasDefaultValue) formatted += $" = {FormatConstant(parameter.DefaultValue)}";
        formatted += FormatCustomModifiers(parameter);
        var attributes = FormatSignatureAttributes(parameter.CustomAttributes);
        return formatted + AttributeSuffix(attributes);
    }

    private static string FormatSignatureType(Type type, NullabilityInfo? nullability)
    {
        if (type.IsByRef) return $"ref {FormatSignatureType(type.GetElementType()!, nullability?.ElementType)}";
        if (type.IsPointer) return $"{FormatSignatureType(type.GetElementType()!, nullability?.ElementType)}*";
        if (type.IsArray)
        {
            var commas = new string(',', type.GetArrayRank() - 1);
            return $"{FormatSignatureType(type.GetElementType()!, nullability?.ElementType)}[{commas}]" + NullableSuffix(type, nullability);
        }
        if (type.IsGenericParameter) return $"{(type.DeclaringMethod is null ? "!" : "!!")}{type.Name}" + NullableSuffix(type, nullability);
        if (type.IsFunctionPointer)
        {
            var parameters = type.GetFunctionPointerParameterTypes().Select(parameter => FormatSignatureType(parameter, null));
            var conventions = type.GetFunctionPointerCallingConventions()
                .Select(ScopedMetadataName).Order(StringComparer.Ordinal).ToArray();
            var convention = conventions.Length == 0 ? string.Empty : $"[{string.Join(", ", conventions)}]";
            return $"delegate*{convention}<{string.Join(", ", parameters.Append(FormatSignatureType(type.GetFunctionPointerReturnType(), null)))}>";
        }
        if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition();
            var nullableArguments = nullability?.GenericTypeArguments ?? Array.Empty<NullabilityInfo>();
            var arguments = type.GetGenericArguments().Select((argument, index) =>
                FormatSignatureType(argument, index < nullableArguments.Length ? nullableArguments[index] : null));
            return $"{ScopedMetadataName(definition)}[{string.Join(", ", arguments)}]" + NullableSuffix(type, nullability);
        }
        return ScopedMetadataName(type) + NullableSuffix(type, nullability);
    }

    private static string NullableSuffix(Type type, NullabilityInfo? nullability)
    {
        if (type.IsValueType || type.IsPointer || type.IsFunctionPointer) return string.Empty;
        return nullability?.ReadState switch
        {
            NullabilityState.Nullable => "?",
            NullabilityState.NotNull => "!",
            _ => "~"
        };
    }

    private static string FormatGenericParameter(Type parameter)
    {
        var parts = new List<string>();
        var variance = parameter.GenericParameterAttributes & GenericParameterAttributes.VarianceMask;
        if (variance == GenericParameterAttributes.Covariant) parts.Add("out");
        if (variance == GenericParameterAttributes.Contravariant) parts.Add("in");
        var constraints = parameter.GenericParameterAttributes & GenericParameterAttributes.SpecialConstraintMask;
        if (constraints.HasFlag(GenericParameterAttributes.ReferenceTypeConstraint)) parts.Add("class");
        if (parameter.CustomAttributes.Any(attribute =>
                attribute.AttributeType.FullName == "System.Runtime.CompilerServices.IsUnmanagedAttribute")) parts.Add("unmanaged");
        else if (constraints.HasFlag(GenericParameterAttributes.NotNullableValueTypeConstraint)) parts.Add("struct");
        foreach (var typeConstraint in parameter.GetGenericParameterConstraints().Where(type => type != typeof(ValueType))
                     .OrderBy(ScopedMetadataName, StringComparer.Ordinal))
            parts.Add(FormatSignatureType(typeConstraint, null));
        if (constraints.HasFlag(GenericParameterAttributes.DefaultConstructorConstraint) &&
            !constraints.HasFlag(GenericParameterAttributes.NotNullableValueTypeConstraint)) parts.Add("new()");
        var attributes = FormatSignatureAttributes(parameter.CustomAttributes);
        if (attributes.Length > 0) parts.Add($"attributes [{attributes}]");
        return parts.Count == 0 ? parameter.Name : $"{parameter.Name} : {string.Join(" & ", parts)}";
    }

    private static string FormatCustomModifiers(ParameterInfo parameter)
    {
        return FormatCustomModifiers(parameter.GetRequiredCustomModifiers(), parameter.GetOptionalCustomModifiers());
    }

    private static string FormatCustomModifiers(IEnumerable<Type> requiredTypes, IEnumerable<Type> optionalTypes)
    {
        var required = requiredTypes.Select(ScopedMetadataName).Order(StringComparer.Ordinal).ToArray();
        var optional = optionalTypes.Select(ScopedMetadataName).Order(StringComparer.Ordinal).ToArray();
        var parts = new List<string>();
        if (required.Length > 0) parts.Add($"modreq[{string.Join(", ", required)}]");
        if (optional.Length > 0) parts.Add($"modopt[{string.Join(", ", optional)}]");
        return parts.Count == 0 ? string.Empty : $" {string.Join(' ', parts)}";
    }

    private static string MethodModifiers(MethodBase method)
    {
        var modifiers = new List<string> { MethodAccessibility(method) };
        if (method.IsStatic) modifiers.Add("static");
        if (method.CallingConvention.HasFlag(CallingConventions.VarArgs)) modifiers.Add("varargs");
        if (method.CallingConvention.HasFlag(CallingConventions.ExplicitThis)) modifiers.Add("explicit-this");
        if (method.IsAbstract) modifiers.Add("abstract");
        else if (method.IsVirtual)
        {
            if (method is MethodInfo info && info.GetBaseDefinition() != info) modifiers.Add("override");
            else modifiers.Add("virtual");
            if (method.IsFinal) modifiers.Add("sealed");
        }
        return string.Join(' ', modifiers);
    }

    private static string FormatGetter(MethodInfo getter)
    {
        var returnModifiers = FormatCustomModifiers(getter.ReturnParameter);
        var returnAttributes = FormatSignatureAttributes(getter.ReturnParameter.CustomAttributes);
        var methodAttributes = FormatSignatureAttributes(getter.CustomAttributes);
        return $"get:{MethodModifiers(getter)}{returnModifiers}" +
               (returnAttributes.Length == 0 ? string.Empty : $" return-attributes [{returnAttributes}]") +
               AttributeSuffix(methodAttributes);
    }

    private static string FormatSetter(MethodInfo setter)
    {
        var init = setter.ReturnParameter.GetRequiredCustomModifiers()
            .Any(type => type.FullName == "System.Runtime.CompilerServices.IsExternalInit");
        var value = setter.GetParameters()[^1];
        var valueModifiers = FormatCustomModifiers(value);
        var valueAttributes = FormatSignatureAttributes(value.CustomAttributes);
        var methodAttributes = FormatSignatureAttributes(setter.CustomAttributes);
        return $"{(init ? "init" : "set")}:{MethodModifiers(setter)}{valueModifiers}" +
               (valueAttributes.Length == 0 ? string.Empty : $" value-attributes [{valueAttributes}]") +
               AttributeSuffix(methodAttributes);
    }

    private static string FormatEventAccessor(string kind, MethodInfo accessor)
    {
        var value = accessor.GetParameters().Single();
        var valueModifiers = FormatCustomModifiers(value);
        var valueAttributes = FormatSignatureAttributes(value.CustomAttributes);
        var methodAttributes = FormatSignatureAttributes(accessor.CustomAttributes);
        return $"{kind}:{MethodModifiers(accessor)}{valueModifiers}" +
               (valueAttributes.Length == 0 ? string.Empty : $" value-attributes [{valueAttributes}]") +
               AttributeSuffix(methodAttributes);
    }

    private static string MethodAccessibility(MethodBase method) => method switch
    {
        { IsPublic: true } => "public",
        { IsFamilyOrAssembly: true } => "protected-internal",
        { IsFamily: true } => "protected",
        _ => throw new InvalidOperationException($"Method '{method}' is not externally visible.")
    };

    private static string FieldAccessibility(FieldInfo field) => field switch
    {
        { IsPublic: true } => "public",
        { IsFamilyOrAssembly: true } => "protected-internal",
        { IsFamily: true } => "protected",
        _ => throw new InvalidOperationException($"Field '{field}' is not externally visible.")
    };

    private static string TypeAccessibility(Type type) => type switch
    {
        { IsPublic: true } => "public",
        { IsNestedPublic: true } => "public",
        { IsNestedFamORAssem: true } => "protected-internal",
        { IsNestedFamily: true } => "protected",
        _ => throw new InvalidOperationException($"Type '{type}' is not externally visible.")
    };

    private static bool IsExternallyVisible(Type type)
    {
        if (type.IsPublic) return true;
        if (!type.IsNested || !(type.IsNestedPublic || type.IsNestedFamily || type.IsNestedFamORAssem)) return false;
        return type.DeclaringType is not null && IsExternallyVisible(type.DeclaringType);
    }

    private static bool IsExternallyVisible(MethodBase? method) => method is not null &&
        (method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly);

    private static bool IsExternallyVisible(FieldInfo field) =>
        field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly;

    private static bool IsExternallyVisible(MemberInfo member) => member switch
    {
        MethodBase method => IsExternallyVisible(method),
        FieldInfo field => IsExternallyVisible(field),
        PropertyInfo property => IsExternallyVisible(property.GetMethod) || IsExternallyVisible(property.SetMethod),
        EventInfo eventInfo => IsExternallyVisible(eventInfo.AddMethod) || IsExternallyVisible(eventInfo.RemoveMethod),
        Type nestedType => IsExternallyVisible(nestedType),
        _ => false
    };

    private static string TypeKind(Type type)
    {
        if (type.IsEnum) return "enum";
        if (IsDelegate(type)) return "delegate";
        if (type.IsInterface) return "interface";
        if (type.IsValueType) return "struct";
        return "class";
    }

    private static bool IsDelegate(Type type) => typeof(MulticastDelegate).IsAssignableFrom(type.BaseType);

    private static IEnumerable<Type> DirectInterfaces(Type type)
    {
        var candidates = type.GetInterfaces().ToHashSet();
        if (type.BaseType is not null) candidates.ExceptWith(type.BaseType.GetInterfaces());
        foreach (var inherited in candidates.SelectMany(candidate => candidate.GetInterfaces()).ToArray())
            candidates.Remove(inherited);
        return candidates;
    }

    private static Type[] SafeTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException exception)
        {
            var failures = string.Join(Environment.NewLine, exception.LoaderExceptions.Select(error => error?.Message));
            throw new InvalidOperationException(
                $"Could not load every type from '{assembly.GetName().Name}':{Environment.NewLine}{failures}",
                exception);
        }
    }

    private static string MetadataName(Type type) => type.FullName ?? type.Name;

    private static string ScopedMetadataName(Type type) =>
        $"[{type.Assembly.GetName().Name}]{MetadataName(type)}";

    private static string FormatConstant(object? value) => value switch
    {
        null => "null",
        string text => JsonSerializer.Serialize(text),
        char character => JsonSerializer.Serialize(character.ToString()),
        bool boolean => boolean ? "true" : "false",
        float number => number.ToString("R", CultureInfo.InvariantCulture),
        double number => number.ToString("R", CultureInfo.InvariantCulture),
        decimal number => number.ToString(CultureInfo.InvariantCulture),
        DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
        Missing => "missing",
        DBNull => "dbnull",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };

    private static string FormatSignatureAttributes(IEnumerable<CustomAttributeData> attributes) => string.Join(
        ", ",
        attributes.Where(attribute => SignatureAttributeNames.Contains(attribute.AttributeType.FullName ?? string.Empty))
            .Select(FormatAttribute)
            .Order(StringComparer.Ordinal));

    private static string FormatAttribute(CustomAttributeData attribute)
    {
        var arguments = attribute.ConstructorArguments.Select(FormatAttributeArgument)
            .Concat(attribute.NamedArguments.OrderBy(argument => argument.MemberName, StringComparer.Ordinal)
                .Select(argument => $"{argument.MemberName}={FormatAttributeArgument(argument.TypedValue)}"));
        return $"{ScopedMetadataName(attribute.AttributeType)}({string.Join(", ", arguments)})";
    }

    private static string FormatAttributeArgument(CustomAttributeTypedArgument argument)
    {
        if (argument.Value is IEnumerable values and not string)
        {
            return "[" + string.Join(", ", values.Cast<object>().Select(value =>
                value is CustomAttributeTypedArgument typed ? FormatAttributeArgument(typed) : FormatConstant(value))) + "]";
        }
        return FormatConstant(argument.Value);
    }

    private static string AttributeSuffix(string attributes) =>
        attributes.Length == 0 ? string.Empty : $" attributes [{attributes}]";

    private static string ReadCanonicalFile(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var text = new UTF8Encoding(false, true).GetString(bytes);
        if (text.Replace("\r\n", string.Empty, StringComparison.Ordinal).Contains('\r', StringComparison.Ordinal) ||
            !(text.EndsWith('\n') || text.EndsWith("\r\n", StringComparison.Ordinal)))
            throw new InvalidOperationException($"Public API baseline '{path}' must be UTF-8 and newline-terminated.");
        return text.Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private sealed class ProductAssemblyLoadContext : AssemblyLoadContext, IDisposable
    {
        private readonly IReadOnlyDictionary<string, string> paths;

        internal ProductAssemblyLoadContext(IReadOnlyDictionary<string, string> paths)
            : base($"OrcaCorePublicApi-{Guid.NewGuid():N}", isCollectible: true)
        {
            this.paths = paths;
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (assemblyName.Name is not null && paths.TryGetValue(assemblyName.Name, out var path))
                return LoadFromAssemblyPath(Path.GetFullPath(path));
            var dependency = Path.Combine(AppContext.BaseDirectory, $"{assemblyName.Name}.dll");
            return File.Exists(dependency) ? LoadFromAssemblyPath(dependency) : null;
        }

        public void Dispose() => Unload();
    }

    private sealed record PublicContractPackageVersion(string PackageVersion);
}

internal sealed record RemovedInternalPlaceholder(string Assembly, string MetadataName, string Task);

internal static class RemovedInternalPlaceholderCatalog
{
    internal static IReadOnlyList<RemovedInternalPlaceholder> All { get; } = FixtureDefinitions.Read<RemovedInternalPlaceholder[]>(
        "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/removed-internal-placeholders.json");
}
