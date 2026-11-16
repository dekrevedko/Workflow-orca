using System.Security.Cryptography;
using System.Text.Json.Serialization;
using AwesomeAssertions;
using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Durable.Hosting.Dag;
using Xunit;

namespace OrcaCore.Hosting.Tests;

public sealed class DagRunInputBindingTests
{
    [Fact]
    public void Restart_ResolvesOnlyExactLocallyInstalledRunInput()
    {
        var binding = new DagRunBindingV1(DefinitionId.New(), DefinitionVersion.Initial, new string('A', 64));
        var first = new DurableDagRunInputCatalog();
        var key = first.Install(binding, typeof(int));
        key.Should().Be($"dag:{binding.DefinitionId}:1:run-input:" +
            Convert.ToHexStringLower(SHA256.HashData("System.Int32\n"u8)));
        first.Install(binding, typeof(int)).Should().Be(key);
        var graph = new DagRunGraphV1(binding, key, []);

        var restarted = new DurableDagRunInputCatalog();
        restarted.Resolve(graph).Should().BeNull();
        restarted.Install(binding, typeof(int)).Should().Be(key);
        restarted.Resolve(graph).Should().Be(typeof(int));

        var conflict = () => restarted.Install(binding, typeof(long));
        conflict.Should().Throw<InvalidOperationException>();
        var wrongShape = () => restarted.Resolve(graph with { RunInputContractKey = key + "-other" });
        wrongShape.Should().Throw<InvalidDataException>();
        var wrongDefinition = () => restarted.Resolve(graph with { Definition = binding with { Fingerprint = new string('B', 64) } });
        wrongDefinition.Should().Throw<InvalidDataException>();
        restarted.Resolve(graph).Should().Be(typeof(int));
    }

    [Fact]
    public void Shape_UsesCodecMemberNamesOrderAndNullableValueTypes()
    {
        var binding = new DagRunBindingV1(DefinitionId.New(), DefinitionVersion.Initial, new string('A', 64));
        var key = new DurableDagRunInputCatalog().Install(binding, typeof(DeclaredPayload));
        var record = "OrcaCore.Hosting.Tests.DagRunInputBindingTests.DeclaredPayload\n" +
            "\"items\"\tSystem.Nullable<System.Int32>[]\tnon-nullable-value=false\n" +
            "System.Nullable<System.Int32>[]\nSystem.Nullable<System.Int32>\nSystem.Int32\n" +
            "\"count\"\tSystem.Int32\tnon-nullable-value=true\nSystem.Int32\n";
        key.Should().EndWith(Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(record))));
    }

    [Fact]
    public void Shape_RejectsRecursiveDeclaredGraphWithoutInstallingIt()
    {
        var binding = new DagRunBindingV1(DefinitionId.New(), DefinitionVersion.Initial, new string('A', 64));
        var catalog = new DurableDagRunInputCatalog();
        var install = () => catalog.Install(binding, typeof(RecursivePayload));
        install.Should().Throw<NotSupportedException>();
        catalog.Install(binding, typeof(int)).Should().NotBeNullOrEmpty();
    }

    public sealed class DeclaredPayload
    {
        [JsonPropertyName("count")]
        public int Count { get; init; }

        [JsonPropertyOrder(-1), JsonPropertyName("items")]
        public int?[] Items { get; init; } = [];

        [JsonIgnore]
        public string Ignored => "";
    }

    public sealed class RecursivePayload
    {
        public RecursivePayload? Next { get; init; }
    }
}
