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

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
public sealed class DagRuntimeViewBoundaryGuards
{
    private const string DagAssembly = "OrcaCore.Dag";
    private const string SourceValidationSha256 = "76d740246772660e475d4698618befda6f6a15ce109ba721e82b55333fe5ceac";
    private const string BehaviorSourceSha256 =
        "ee97d40d9adad32e9a217416ccef0e61604f311c0798bd1e5837ea1efffe3014";
    private static readonly string[] AllowedTypes =
    [
        "OrcaCore.Dag.DagMappedInputResult",
        "OrcaCore.Dag.DagRuntimeNodeDescriptor",
        "OrcaCore.Dag.DagRuntimeView`1"
    ];
    private static readonly string[] AllowedMembers =
    [
        "OrcaCore.Dag.WorkflowDagPlan`1::GetRuntimeView():[OrcaCore.Dag]OrcaCore.Dag.DagRuntimeView`1<!0>",
        "OrcaCore.Dag.DagRuntimeView`1::get_Nodes():[System.Runtime]System.Collections.Generic.IReadOnlyList`1<[OrcaCore.Dag]OrcaCore.Dag.DagRuntimeNodeDescriptor>",
        "OrcaCore.Dag.DagRuntimeView`1::EvaluateMapping([OrcaCore.Dag]OrcaCore.Dag.DagNodeRef,!0,[System.Runtime]System.Collections.Generic.IReadOnlyDictionary`2<[OrcaCore.Dag]OrcaCore.Dag.DagNodeRef,Object>):[OrcaCore.Dag]OrcaCore.Dag.DagMappedInputResult",
        "OrcaCore.Dag.DagRuntimeNodeDescriptor::get_Reference():[OrcaCore.Dag]OrcaCore.Dag.DagNodeRef",
        "OrcaCore.Dag.DagRuntimeNodeDescriptor::get_AuthoredOrdinal():Int32",
        "OrcaCore.Dag.DagRuntimeNodeDescriptor::get_ChildDefinitionId():[OrcaCore]OrcaCore.DefinitionId",
        "OrcaCore.Dag.DagRuntimeNodeDescriptor::get_ChildDefinitionVersion():[OrcaCore]OrcaCore.DefinitionVersion",
        "OrcaCore.Dag.DagRuntimeNodeDescriptor::get_ChildFingerprint():[OrcaCore]OrcaCore.DefinitionFingerprint",
        "OrcaCore.Dag.DagRuntimeNodeDescriptor::get_InputType():[System.Runtime]System.Type",
        "OrcaCore.Dag.DagRuntimeNodeDescriptor::get_OutputType():[System.Runtime]System.Type",
        "OrcaCore.Dag.DagRuntimeNodeDescriptor::get_Dependencies():[System.Runtime]System.Collections.Generic.IReadOnlyList`1<[OrcaCore.Dag]OrcaCore.Dag.DagNodeRef>",
        "OrcaCore.Dag.DagMappedInputResult::get_IsValid():Boolean",
        "OrcaCore.Dag.DagMappedInputResult::get_Input():Object",
        "OrcaCore.Dag.DagMappedInputResult::get_InputType():[System.Runtime]System.Type",
        "OrcaCore.Dag.DagMappedInputResult::get_FailureCode():String"
    ];

    [Fact]
    public void Hosting_ConsumesExactlyTheThreeTypesAndFifteenReviewedSignatures()
    {
        var validation = File.ReadAllText(Path.Combine(FixtureDefinitions.RepositoryRoot(),
            "openspec", "changes", "admit-dag-hosting-runtime-view", "artifacts",
            "task-2-runtime-view-source-validation-2026-10-04.md")).Replace("\r\n", "\n").Replace('\r', '\n');
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(validation))).ToLowerInvariant()
            .Should().Be(SourceValidationSha256, "the reviewed source claims must remain immutable after the freeze");
        typeof(OrcaCore.Dag.Dag).Assembly.GetCustomAttributes<InternalsVisibleToAttribute>()
            .Select(friend => friend.AssemblyName).Should().Equal("OrcaCore.Dag.Hosting");
        var references = Inspect(typeof(OrcaCore.Dag.Hosting.DagHostOptions).Assembly.Location,
            typeof(OrcaCore.Dag.Dag).Assembly.Location);
        references.Types.Should().Equal(AllowedTypes.Order(StringComparer.Ordinal));
        references.Members.Should().Equal(AllowedMembers.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void HostingLevelMapping_ExercisesTheRealAdapterWithoutAnotherTestFriend()
    {
        InProbeDirectory(directory =>
        {
            var root = FixtureDefinitions.RepositoryRoot();
            File.Copy(Path.Combine(root, "src", "OrcaCore.Dag.Hosting", "DagInputMappingAdapter.cs"),
                Path.Combine(directory, "Adapter.cs"));
            var behaviorPath = Path.Combine(root, "tests", "OrcaCore.DeveloperSurface.Guards", "Fixtures",
                "dag-runtime-view-behavior.cs.txt");
            var behavior = File.ReadAllText(behaviorPath).Replace("\r\n", "\n").Replace('\r', '\n');
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(behavior))).ToLowerInvariant()
                .Should().Be(BehaviorSourceSha256, "the full behavior catalog must not lose a case after archival");
            File.Copy(behaviorPath, Path.Combine(directory, "Program.cs"));
            var dll = Build(directory, "OrcaCore.Dag.Hosting", true,
                typeof(OrcaCore.Dag.Dag).Assembly.Location, typeof(OrcaCore.DefinitionId).Assembly.Location,
                Path.Combine(Path.GetDirectoryName(typeof(OrcaCore.DefinitionId).Assembly.Location)!, "OrcaCore.Core.dll"));
            var result = Run("dotnet", directory, dll);
            result.ExitCode.Should().Be(0, result.Output);
            result.Output.Should().Contain("HOSTING_MAPPING_ASSERTIONS=");
            var count = int.Parse(result.Output.Split("HOSTING_MAPPING_ASSERTIONS=")[1].Trim());
            count.Should().Be(34, "the linked real-adapter harness must execute every declared behavioral assertion");
        });
    }

    [Fact]
    public void MetadataPolicy_RejectsEachCompiledTypeAndMemberEvasionIndependently()
    {
        InProbeDirectory(directory =>
        {
            var root = FixtureDefinitions.RepositoryRoot();
            var target = Path.Combine(directory, "target");
            Directory.CreateDirectory(target);
            foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "src", "OrcaCore.Dag"), "*.cs"))
            {
                var text = File.ReadAllText(file).Replace("\r\n", "\n").Replace('\r', '\n');
                if (Path.GetFileName(file) == "DagAuthoring.cs")
                    text = text.Replace("internal DagRuntimeView<TRunInput> GetRuntimeView() => new(NodePlans);",
                        "internal DagRuntimeView<TRunInput> GetRuntimeView() => new(NodePlans);\n" +
                        "internal DagRuntimeView<TRunInput> GetRuntimeView(int forbiddenOverload) => new(NodePlans);",
                        StringComparison.Ordinal);
                if (Path.GetFileName(file) == "DagRuntimeView.cs")
                    text = text.Replace("internal sealed class DagRuntimeNodeDescriptor\n{",
                        "internal sealed class DagRuntimeNodeDescriptor\n{\n    internal int ForbiddenField;",
                        StringComparison.Ordinal);
                File.WriteAllText(Path.Combine(target, Path.GetFileName(file)), text);
            }
            File.WriteAllText(Path.Combine(target, "Sentinels.cs"),
                "namespace OrcaCore.Dag; internal interface IForbiddenRuntime { } internal interface IIsOnlyRuntime { } internal class ForbiddenBase { } " +
                "internal class AttributeOnly { } internal class GenericOnly { }");
            var targetDll = Build(target, DagAssembly, false, typeof(OrcaCore.DefinitionId).Assembly.Location);
            var client = Path.Combine(directory, "client");
            Directory.CreateDirectory(client);
            File.WriteAllText(Path.Combine(client, "Probe.cs"), """
                using System;
                using System.Collections.Generic;
                using OrcaCore.Dag;
                internal sealed class TypeTokenAttribute(Type type) : Attribute { }
                [TypeToken(typeof(AttributeOnly))] internal sealed class AttributeProbe { }
                internal sealed class FieldProbe { internal DagNodeDraft<int>? Field; }
                internal static class SignatureProbe { internal static DagNodePlan<int>? Value() => null; }
                internal static class TypeProbe { internal static Type Value => typeof(DagValueTypes); }
                internal sealed class GenericProbe { internal List<GenericOnly>? Value; }
                internal sealed class InterfaceProbe : IForbiddenRuntime { }
                internal static class IsProbe { internal static bool Value(object value) => value is IIsOnlyRuntime; }
                internal sealed class BaseProbe : ForbiddenBase { }
                internal static class MemberProbe { internal static object Value(DagNodeRef node) => node.PlanToken; }
                internal static class OverloadProbe { internal static object Value(WorkflowDagPlan<int> plan) => plan.GetRuntimeView(1); }
                internal static class ConstructorProbe { internal static object Value() => new DagMappedInputResult(1, typeof(int)); }
                internal static class FieldReadProbe { internal static int Value(DagRuntimeNodeDescriptor node) => node.ForbiddenField; }
                """);
            var clientDll = Build(client, "OrcaCore.Dag.Hosting", false,
                targetDll, typeof(OrcaCore.DefinitionId).Assembly.Location);
            var references = Inspect(clientDll, targetDll);
            foreach (var type in new[] { "OrcaCore.Dag.DagValueTypes", "OrcaCore.Dag.DagNodeDraft`1",
                         "OrcaCore.Dag.DagNodePlan`1", "OrcaCore.Dag.IForbiddenRuntime", "OrcaCore.Dag.ForbiddenBase",
                         "OrcaCore.Dag.AttributeOnly", "OrcaCore.Dag.GenericOnly", "OrcaCore.Dag.IIsOnlyRuntime" })
            {
                references.Types.Should().Contain(type, "compiled type-only probes must emit real TypeRefs");
                Action validate = () => AssertTypes([type]);
                validate.Should().Throw<Exception>().WithMessage("*" + type + "*");
            }
            foreach (var fragment in new[] { "::get_PlanToken()", "::GetRuntimeView(Int32)", "::.ctor(Object,", "::ForbiddenField:" })
            {
                var identity = references.Members.Single(member => member.Contains(fragment, StringComparison.Ordinal));
                Action validate = () => AssertMembers([identity]);
                validate.Should().Throw<Exception>().WithMessage("*" + fragment + "*");
            }
            File.WriteAllText(Path.Combine(client, "Probe.cs"),
                "using OrcaCore.Dag; internal static class PublicProbe { internal static object Value(DagNodeRef node) => node.NodeId; }");
            var publicDll = Build(client, "OrcaCore.Dag.Hosting", false,
                targetDll, typeof(OrcaCore.DefinitionId).Assembly.Location);
            var publicReferences = Inspect(publicDll, targetDll);
            AssertTypes(publicReferences.Types);
            AssertMembers(publicReferences.Members);
            publicReferences.Types.Should().BeEmpty();
            publicReferences.Members.Should().BeEmpty();
        });
    }

    private static void AssertTypes(IEnumerable<string> types) => types.Should().BeSubsetOf(AllowedTypes,
        "every non-public DAG type reference requires explicit contract authority");
    private static void AssertMembers(IEnumerable<string> members) => members.Should().BeSubsetOf(AllowedMembers,
        "every non-public DAG member or overload requires its exact reviewed signature");

    private static (string[] Types, string[] Members) Inspect(string clientPath, string targetPath)
    {
        using var targetStream = File.OpenRead(targetPath);
        using var targetPe = new PEReader(targetStream);
        var target = targetPe.GetMetadataReader();
        using var clientStream = File.OpenRead(clientPath);
        using var clientPe = new PEReader(clientStream);
        var client = clientPe.GetMetadataReader();
        var definitions = target.TypeDefinitions.ToDictionary(handle => DefinitionName(target, handle));
        var types = new SortedSet<string>(StringComparer.Ordinal);
        var members = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var handle in client.TypeReferences)
        {
            var reference = ReferenceType(client, handle);
            if (reference is null || reference.Value.Assembly != DagAssembly) continue;
            definitions.ContainsKey(reference.Value.Name).Should().BeTrue("every external type must resolve");
            if (!Visible(target, definitions[reference.Value.Name])) types.Add(reference.Value.Name);
        }
        foreach (var handle in client.MemberReferences)
        {
            var member = client.GetMemberReference(handle);
            var type = ReferenceType(client, member.Parent);
            if (type is null || type.Value.Assembly != DagAssembly) continue;
            definitions.ContainsKey(type.Value.Name).Should().BeTrue();
            var definitionHandle = definitions[type.Value.Name];
            var definition = target.GetTypeDefinition(definitionHandle);
            var name = client.GetString(member.Name);
            var reader = client.GetBlobReader(member.Signature);
            var header = reader.ReadSignatureHeader();
            if (header.Kind == SignatureKind.Field)
            {
                var fieldType = member.DecodeFieldSignature(Signatures.Instance, null);
                var field = definition.GetFields().Select(target.GetFieldDefinition).Single(field =>
                    target.GetString(field.Name) == name && field.DecodeSignature(Signatures.Instance, null) == fieldType);
                if (!Visible(target, definitionHandle) || (field.Attributes & FieldAttributes.FieldAccessMask) != FieldAttributes.Public)
                    members.Add($"{type.Value.Name}::{name}:{fieldType}");
            }
            else
            {
                var signature = MethodSignature(member.DecodeMethodSignature(Signatures.Instance, null));
                var method = definition.GetMethods().Select(target.GetMethodDefinition).Single(method =>
                    target.GetString(method.Name) == name && MethodSignature(method.DecodeSignature(Signatures.Instance, null)) == signature);
                if (!Visible(target, definitionHandle) || (method.Attributes & MethodAttributes.MemberAccessMask) != MethodAttributes.Public)
                    members.Add($"{type.Value.Name}::{name}{signature}");
            }
        }
        return (types.ToArray(), members.ToArray());
    }

    private static string MethodSignature(MethodSignature<string> signature) =>
        (signature.GenericParameterCount == 0 ? "" : $"``{signature.GenericParameterCount}") +
        $"({string.Join(',', signature.ParameterTypes)}):{signature.ReturnType}";

    private static bool Visible(MetadataReader reader, TypeDefinitionHandle handle)
    {
        var type = reader.GetTypeDefinition(handle);
        var access = type.Attributes & TypeAttributes.VisibilityMask;
        return access == TypeAttributes.Public || access == TypeAttributes.NestedPublic && Visible(reader, type.GetDeclaringType());
    }

    private static string DefinitionName(MetadataReader reader, TypeDefinitionHandle handle)
    {
        var type = reader.GetTypeDefinition(handle);
        return type.IsNested ? DefinitionName(reader, type.GetDeclaringType()) + "+" + reader.GetString(type.Name) :
            string.IsNullOrEmpty(reader.GetString(type.Namespace)) ? reader.GetString(type.Name) :
            reader.GetString(type.Namespace) + "." + reader.GetString(type.Name);
    }

    private static (string Assembly, string Name)? ReferenceType(MetadataReader reader, EntityHandle handle)
    {
        if (handle.Kind == HandleKind.TypeSpecification)
        {
            var blob = reader.GetBlobReader(reader.GetTypeSpecification((TypeSpecificationHandle)handle).Signature);
            if (blob.ReadByte() != 0x15 || blob.ReadByte() is not (0x11 or 0x12))
                throw new InvalidDataException("Unsupported external member declaring type.");
            var coded = blob.ReadCompressedInteger();
            var row = coded >> 2;
            return ReferenceType(reader, (coded & 3) switch
            {
                0 => MetadataTokens.TypeDefinitionHandle(row),
                1 => MetadataTokens.TypeReferenceHandle(row),
                2 => MetadataTokens.TypeSpecificationHandle(row),
                _ => throw new InvalidDataException("Invalid type token.")
            });
        }
        if (handle.Kind != HandleKind.TypeReference) return null;
        var type = reader.GetTypeReference((TypeReferenceHandle)handle);
        var name = reader.GetString(type.Name);
        if (type.ResolutionScope.Kind == HandleKind.TypeReference)
        {
            var parent = ReferenceType(reader, type.ResolutionScope);
            return parent is null ? null : (parent.Value.Assembly, parent.Value.Name + "+" + name);
        }
        if (type.ResolutionScope.Kind != HandleKind.AssemblyReference) return null;
        var ns = reader.GetString(type.Namespace);
        return (reader.GetString(reader.GetAssemblyReference((AssemblyReferenceHandle)type.ResolutionScope).Name),
            string.IsNullOrEmpty(ns) ? name : ns + "." + name);
    }

    private static void InProbeDirectory(Action<string> action)
    {
        var directory = Path.Combine(Path.GetTempPath(), "orcacore-runtime-view-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.Copy(Path.Combine(FixtureDefinitions.RepositoryRoot(), "global.json"), Path.Combine(directory, "global.json"));
            File.ReadAllBytes(Path.Combine(directory, "global.json")).Should().Equal(
                File.ReadAllBytes(Path.Combine(FixtureDefinitions.RepositoryRoot(), "global.json")),
                "every temporary Hosting probe must inherit the reviewed repository SDK selection");
            action(directory);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static string Build(string directory, string assembly, bool executable, params string[] references)
    {
        var project = "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework>" +
            "<Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings><AssemblyName>" + assembly + "</AssemblyName>" +
            (executable ? "<OutputType>Exe</OutputType>" : "") + "</PropertyGroup><ItemGroup>" +
            string.Join("", references.Select(path => "<Reference Include=\"" + Path.GetFileNameWithoutExtension(path) +
                "\"><HintPath>" + SecurityElement.Escape(path) + "</HintPath></Reference>")) + "</ItemGroup></Project>";
        var path = Path.Combine(directory, "Probe.csproj");
        File.WriteAllText(path, project);
        var result = Run("dotnet", directory, "build", path, "-c", "Release", "--verbosity", "quiet");
        result.ExitCode.Should().Be(0, result.Output);
        return Path.Combine(directory, "bin", "Release", "net10.0", assembly + ".dll");
    }

    private static (int ExitCode, string Output) Run(string command, string directory, params string[] arguments)
    {
        var start = new ProcessStartInfo(command) { WorkingDirectory = directory, RedirectStandardOutput = true,
            RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start metadata probe.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return (process.ExitCode, stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult());
    }

    private sealed class Signatures : ISignatureTypeProvider<string, object?>
    {
        internal static readonly Signatures Instance = new();
        public string GetPrimitiveType(PrimitiveTypeCode code) => code.ToString();
        public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte kind) =>
            $"[{DagAssembly}]{DefinitionName(reader, handle)}";
        public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte kind)
        {
            var type = ReferenceType(reader, handle) ?? throw new InvalidDataException("Unresolved signature type.");
            return $"[{type.Assembly}]{type.Name}";
        }
        public string GetTypeFromSpecification(MetadataReader reader, object? context, TypeSpecificationHandle handle, byte kind) =>
            reader.GetTypeSpecification(handle).DecodeSignature(this, context);
        public string GetGenericInstantiation(string type, ImmutableArray<string> arguments) => $"{type}<{string.Join(',', arguments)}>";
        public string GetGenericTypeParameter(object? context, int index) => $"!{index}";
        public string GetGenericMethodParameter(object? context, int index) => $"!!{index}";
        public string GetArrayType(string type, ArrayShape shape) => $"{type}[{new string(',', shape.Rank - 1)}]";
        public string GetSZArrayType(string type) => type + "[]";
        public string GetByReferenceType(string type) => type + "&";
        public string GetPointerType(string type) => type + "*";
        public string GetPinnedType(string type) => "pinned " + type;
        public string GetModifiedType(string modifier, string type, bool required) => $"{type} mod{(required ? "req" : "opt")}({modifier})";
        public string GetFunctionPointerType(MethodSignature<string> signature) => "fn" + MethodSignature(signature);
    }
}
