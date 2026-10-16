using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using AwesomeAssertions;
using OrcaCore;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
public sealed class DagInternalMemberReferenceGuards
{
    private const string Task82ValidationArtifactSha256 =
        "c0cabc507a3a5ac6318b75462ba9581fd3f086ead6cec8057d2932b9010fa9b6";

    // Canonical decoded signatures avoid unstable metadata row numbers in raw blobs.
    private static readonly string[] AllowedInternalReferences =
    [
        "OrcaCore.AuthoredLocation::.ctor(String):Void",
        "OrcaCore.DefinitionFingerprint::.ctor(String):Void",
        "OrcaCore.DefinitionFingerprint::ComputeCanonicalHash(String):String",
        "OrcaCore.Validation`1::.ctor(!0,[System.Runtime]System.Collections.Generic.IReadOnlyList`1<[OrcaCore]OrcaCore.WorkflowDiagnostic>):Void",
        "OrcaCore.WorkflowDefinitionException::.ctor([System.Runtime]System.Collections.Generic.IReadOnlyList`1<[OrcaCore]OrcaCore.WorkflowDiagnostic>):Void",
        "OrcaCore.WorkflowDiagnostic::.ctor(String,[OrcaCore]OrcaCore.WorkflowDiagnosticSeverity,[OrcaCore]OrcaCore.AuthoredLocation,[System.Runtime]System.Collections.Generic.IReadOnlyList`1<[OrcaCore]OrcaCore.AuthoredLocation>,String):Void"
    ];

    [Fact]
    public void DagAssembly_ReferencesOnlyTheApprovedInternalOrcaCoreMembers()
    {
        typeof(OrcaCore.Dag.Dag).Assembly.GetReferencedAssemblies()
            .Select(assembly => assembly.Name!)
            .Where(name => name.StartsWith("OrcaCore", StringComparison.Ordinal))
            .Should().Equal(["OrcaCore"], "the DAG application package has one product dependency");
        var friends = typeof(DefinitionFingerprint).Assembly
            .GetCustomAttributes<InternalsVisibleToAttribute>()
            .Select(attribute => attribute.AssemblyName).ToArray();
        friends.Should().ContainSingle(friend => friend == "OrcaCore.Dag")
            .And.NotContain("OrcaCore.Dag.Hosting");

        using var stream = File.OpenRead(typeof(OrcaCore.Dag.Dag).Assembly.Location);
        using var pe = new PEReader(stream);
        var metadata = pe.GetMetadataReader();
        AssertApprovedReferences(metadata, AllowedInternalReferences);

        AssertTypeOnlyNegativeProbes();

        var artifact = Path.Combine(FixtureDefinitions.RepositoryRoot(), "openspec", "changes",
            "admit-dag-authoring-friend-boundary", "artifacts",
            "task-8-2-authoring-source-validation-2026-09-28.md");
        var normalized = File.ReadAllText(artifact).Replace("\r\n", "\n").Replace('\r', '\n');
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)))
            .ToLowerInvariant().Should().Be(Task82ValidationArtifactSha256,
                "the Task 8.2 source-validation decision must remain byte-exact after LF normalization");
    }

    private static void AssertApprovedReferences(MetadataReader metadata, string[] approvedMembers)
    {
        NonPublicOrcaCoreTypeReferences(metadata).Should().BeEmpty(
            "a friend grant must not permit type-only references, signatures, base classes, " +
            "or interface implementations of non-public OrcaCore types");
        InternalOrcaCoreMemberReferences(metadata).Should().Equal(
            approvedMembers.OrderBy(value => value, StringComparer.Ordinal));
    }

    private static string[] NonPublicOrcaCoreTypeReferences(MetadataReader metadata)
    {
        var assembly = typeof(DefinitionFingerprint).Assembly;
        var actual = new HashSet<string>(StringComparer.Ordinal);
        foreach (var handle in metadata.TypeReferences)
        {
            var target = TargetType(metadata, handle);
            if (target is null || target.Value.Assembly != "OrcaCore")
            {
                continue;
            }

            var type = assembly.GetType(target.Value.Name);
            type.Should().NotBeNull($"the referenced OrcaCore type '{target.Value.Name}' must resolve");
            if (!type!.IsVisible)
            {
                actual.Add(target.Value.Name);
            }
        }

        return actual.OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    private static string[] InternalOrcaCoreMemberReferences(MetadataReader metadata)
    {
        var actual = new List<string>();

        foreach (var handle in metadata.MemberReferences)
        {
            var reference = metadata.GetMemberReference(handle);
            var parent = TargetType(metadata, reference.Parent);
            if (parent is null || parent.Value.Assembly != "OrcaCore")
            {
                continue;
            }

            var memberName = metadata.GetString(reference.Name);
            var targetType = typeof(DefinitionFingerprint).Assembly.GetType(parent.Value.Name);
            targetType.Should().NotBeNull($"{parent.Value.Name}::{memberName} must resolve in OrcaCore");
            var provider = new CanonicalSignatureProvider();
            var signatureReader = metadata.GetBlobReader(reference.Signature);
            var header = signatureReader.ReadSignatureHeader();
            if (header.Kind == SignatureKind.Field)
            {
                var fieldType = reference.DecodeFieldSignature(provider, null);
                var fields = targetType!.GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .Where(field => field.Name == memberName).ToArray();
                fields.Should().ContainSingle($"{parent.Value.Name}::{memberName} must resolve");
                if (!targetType.IsVisible ||
                    (!fields[0].IsPublic && !fields[0].IsFamily && !fields[0].IsFamilyOrAssembly))
                {
                    actual.Add($"{parent.Value.Name}::{memberName}:{fieldType}");
                }
                continue;
            }

            var signature = reference.DecodeMethodSignature(provider, null);
            var identity = $"{parent.Value.Name}::{memberName}(" +
                string.Join(",", signature.ParameterTypes) + $"):{signature.ReturnType}";
            if (!targetType!.IsVisible ||
                IsInternalReference(targetType, memberName, signature.ParameterTypes.Length))
            {
                actual.Add(identity);
            }
        }

        return actual.OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    private static void AssertTypeOnlyNegativeProbes()
    {
        (string Name, string Source, string ForbiddenType)[] probes =
        [
            ("typeof", "internal static class Probe { internal static System.Type Value => typeof(WorkflowDiagnosticCatalog); }",
                "OrcaCore.WorkflowDiagnosticCatalog"),
            ("field-signature", "internal sealed class Probe { internal WorkflowDiagnosticDescriptor? Value; }",
                "OrcaCore.WorkflowDiagnosticDescriptor"),
            ("interface-implementation", "internal sealed class Probe : IWorkflowDefinitionRuntimeMetadata { object IWorkflowDefinitionRuntimeMetadata.RuntimeDefinition => new(); System.Type IWorkflowDefinitionRuntimeMetadata.RuntimeStateType => typeof(object); }",
                "OrcaCore.IWorkflowDefinitionRuntimeMetadata"),
            ("is-type", "internal static class Probe { internal static bool Check(object value) => value is IWorkflowDefinitionRuntimeMetadata; }",
                "OrcaCore.IWorkflowDefinitionRuntimeMetadata")
        ];

        foreach (var probe in probes)
        {
            var directory = Path.Combine(Path.GetTempPath(), "orcacore-dag-type-probe-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var assemblyPath = SecurityElement.Escape(typeof(DefinitionFingerprint).Assembly.Location);
                var project = $$"""
                    <Project Sdk="Microsoft.NET.Sdk">
                      <PropertyGroup>
                        <TargetFramework>net10.0</TargetFramework>
                        <AssemblyName>OrcaCore.Dag</AssemblyName>
                      </PropertyGroup>
                      <ItemGroup>
                        <Reference Include="OrcaCore"><HintPath>{{assemblyPath}}</HintPath></Reference>
                      </ItemGroup>
                    </Project>
                    """;
                var projectPath = Path.Combine(directory, "TypeProbe.csproj");
                // Temporary projects must select the same SDK as the reviewed repository,
                // rather than whichever SDK happens to be the machine-wide default.
                File.Copy(Path.Combine(FixtureDefinitions.RepositoryRoot(), "global.json"),
                    Path.Combine(directory, "global.json"));
                File.ReadAllBytes(Path.Combine(directory, "global.json")).Should().Equal(
                    File.ReadAllBytes(Path.Combine(FixtureDefinitions.RepositoryRoot(), "global.json")),
                    "every external probe must retain the reviewed repository SDK selection");
                File.WriteAllText(projectPath, project);
                File.WriteAllText(Path.Combine(directory, "Probe.cs"),
                    "using OrcaCore; namespace OrcaCore.Dag; " + probe.Source);

                var start = new ProcessStartInfo("dotnet")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    WorkingDirectory = directory
                };
                foreach (var argument in new[] { "build", projectPath, "-c", "Release", "--verbosity", "quiet" })
                {
                    start.ArgumentList.Add(argument);
                }

                using var process = Process.Start(start) ??
                    throw new InvalidOperationException($"Could not start {probe.Name} metadata probe build.");
                var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
                process.WaitForExit();
                process.ExitCode.Should().Be(0,
                    $"the {probe.Name} type-only probe must compile through the approved friend edge: {output}");

                var dll = Path.Combine(directory, "bin", "Release", "net10.0", "OrcaCore.Dag.dll");
                using var stream = File.OpenRead(dll);
                using var pe = new PEReader(stream);
                var metadata = pe.GetMetadataReader();
                var approvedMembers = InternalOrcaCoreMemberReferences(metadata);
                Action validate = () => AssertApprovedReferences(metadata, approvedMembers);
                validate.Should().Throw<Exception>()
                    .WithMessage($"*{probe.ForbiddenType}*",
                        $"the {probe.Name} probe must fail on its forbidden type, independently of member references");
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static bool IsInternalReference(
        Type type, string memberName, int parameterCount)
    {
        var candidates = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Cast<MethodBase>()
            .Concat(type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(method => method.Name == memberName &&
                method.GetParameters().Length == parameterCount)
            .ToArray();
        candidates.Should().NotBeEmpty($"{type.FullName}::{memberName} with {parameterCount} parameters must resolve");
        // Protected members are inherited without the friend grant. The seam covers
        // assembly/private-protected/private access, not ordinary subclass access.
        return candidates.Any(method => method.IsAssembly || method.IsFamilyAndAssembly ||
            method.IsPrivate);
    }

    private static (string Assembly, string Name)? TargetType(
        MetadataReader metadata, EntityHandle handle)
    {
        if (handle.Kind == HandleKind.TypeSpecification)
        {
            var blob = metadata.GetBlobReader(metadata.GetTypeSpecification(
                (TypeSpecificationHandle)handle).Signature);
            if (blob.ReadByte() != 0x15 || blob.ReadByte() is not (0x11 or 0x12))
            {
                throw new InvalidDataException("Unsupported DAG member type specification.");
            }

            var coded = blob.ReadCompressedInteger();
            return TargetType(metadata, TypeDefOrRef(coded));
        }

        if (handle.Kind != HandleKind.TypeReference)
        {
            return null;
        }

        var reference = metadata.GetTypeReference((TypeReferenceHandle)handle);
        var name = metadata.GetString(reference.Name);
        var scope = reference.ResolutionScope;
        if (scope.Kind == HandleKind.TypeReference)
        {
            var declaring = TargetType(metadata, scope);
            return declaring is null ? null :
                (declaring.Value.Assembly, $"{declaring.Value.Name}+{name}");
        }

        if (scope.Kind != HandleKind.AssemblyReference)
        {
            return null;
        }

        var assembly = metadata.GetString(metadata.GetAssemblyReference(
            (AssemblyReferenceHandle)scope).Name);
        var ns = metadata.GetString(reference.Namespace);
        return (assembly, string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}");
    }

    private static EntityHandle TypeDefOrRef(int coded)
    {
        var row = coded >> 2;
        return (coded & 3) switch
        {
            0 => MetadataTokens.TypeDefinitionHandle(row),
            1 => MetadataTokens.TypeReferenceHandle(row),
            2 => MetadataTokens.TypeSpecificationHandle(row),
            _ => throw new InvalidDataException("Invalid type reference in DAG metadata.")
        };
    }

    private sealed class CanonicalSignatureProvider : ISignatureTypeProvider<string, object?>
    {
        public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode.ToString();

        public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle,
            byte rawTypeKind)
        {
            var type = TargetType(reader, handle) ??
                throw new InvalidDataException("Unresolved DAG signature type reference.");
            return $"[{type.Assembly}]{type.Name}";
        }

        public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle,
            byte rawTypeKind)
        {
            var definition = reader.GetTypeDefinition(handle);
            var ns = reader.GetString(definition.Namespace);
            return $"[OrcaCore.Dag]{ns}.{reader.GetString(definition.Name)}";
        }

        public string GetTypeFromSpecification(MetadataReader reader, object? context,
            TypeSpecificationHandle handle, byte rawTypeKind) =>
            reader.GetTypeSpecification(handle).DecodeSignature(this, context);

        public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments) =>
            $"{genericType}<{string.Join(",", typeArguments)}>";

        public string GetGenericTypeParameter(object? context, int index) => $"!{index}";
        public string GetGenericMethodParameter(object? context, int index) => $"!!{index}";
        public string GetArrayType(string elementType, ArrayShape shape) =>
            $"{elementType}[{new string(',', shape.Rank - 1)}]";
        public string GetSZArrayType(string elementType) => $"{elementType}[]";
        public string GetByReferenceType(string elementType) => $"{elementType}&";
        public string GetPointerType(string elementType) => $"{elementType}*";
        public string GetPinnedType(string elementType) => $"pinned {elementType}";
        public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) =>
            $"{unmodifiedType} mod{(isRequired ? "req" : "opt")}({modifier})";
        public string GetFunctionPointerType(MethodSignature<string> signature) =>
            $"fn({string.Join(",", signature.ParameterTypes)}):{signature.ReturnType}";
    }
}
