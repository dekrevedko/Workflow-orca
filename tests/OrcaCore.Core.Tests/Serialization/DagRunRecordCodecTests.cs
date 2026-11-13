using System.Security.Cryptography;
using System.Text;
using AwesomeAssertions;
using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Serialization;
using Xunit;

namespace OrcaCore.Core.Tests.Serialization;

public sealed class DagRunRecordCodecTests
{
    [Theory]
    [InlineData("nul")]
    [InlineData("surrogate")]
    [InlineData("blank")]
    [InlineData("long-node")]
    [InlineData("padded-node")]
    public void StorageText_RejectsUnrepresentableIdentifiersBeforeEncoding(string mutation)
    {
        var original = Created();
        var snapshot = original.CreationSnapshot!;
        var text = mutation switch
        {
            "nul" => "bad\0text",
            "surrogate" => "bad\ud800text",
            "blank" => " ",
            "long-node" => new string('x', 451),
            _ => " padded "
        };
        Action node = () => DagRunRecordCodecV1.Serialize(original with
        {
            CreationSnapshot = snapshot with
            {
                Graph = snapshot.Graph with
                {
                    Nodes = new[] { snapshot.Graph.Nodes[0] with { NodeId = text } }
                }
            }
        });
        node.Should().Throw<InvalidDataException>();
        if (mutation is "long-node" or "padded-node") return;
        Action key = () => new DagRunStartKeyV1(text);
        key.Should().Throw<InvalidDataException>();
        Action contract = () => new DagRunValueV1(true, text, "orcacore-json-v1", "null"u8.ToArray(),
            Convert.ToHexString(SHA256.HashData("null"u8)));
        contract.Should().Throw<InvalidDataException>();
    }

    [Theory]
    [InlineData("value")]
    [InlineData("binding")]
    [InlineData("start-key")]
    public void Serialize_RevalidatesInitPropertiesInsteadOfTrustingConstructorChecks(string target)
    {
        var record = Created();
        var snapshot = record.CreationSnapshot!;
        snapshot = target switch
        {
            "value" => snapshot with { RunInput = snapshot.RunInput with { Fingerprint = new string('a', 64) } },
            "binding" => snapshot with { Graph = snapshot.Graph with { Definition = snapshot.Graph.Definition with { Fingerprint = new string('a', 64) } } },
            _ => snapshot with { StartKey = snapshot.StartKey with { Value = "bad\0key" } }
        };
        Action encode = () => DagRunRecordCodecV1.Serialize(record with { CreationSnapshot = snapshot });
        encode.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void CreatedFrame_RoundTripsWithoutChangingCanonicalBytes()
    {
        var record = Created();
        var encoded = DagRunRecordCodecV1.Serialize(record);
        var restored = DagRunRecordCodecV1.Deserialize(encoded);

        DagRunRecordCodecV1.Serialize(restored).Should().Equal(encoded);
        restored.CreationSnapshot!.RunInput.Fingerprint.Should().Be(
            Convert.ToHexString(SHA256.HashData("null"u8)));
        restored.CreationSnapshot.RunInput.IsPresent.Should().BeTrue();
    }

    [Theory]
    [InlineData("extra")]
    [InlineData("duplicate")]
    [InlineData("version")]
    [InlineData("kind")]
    [InlineData("missing")]
    [InlineData("guid")]
    [InlineData("timestamp")]
    public void Frame_RejectsUnknownDuplicateMissingAndNoncanonicalCoordinates(string mutation)
    {
        var original = Encoding.UTF8.GetString(DagRunRecordCodecV1.Serialize(Created()));
        var text = mutation switch
        {
            "extra" => original.Insert(1, "\"unknown\":1,"),
            "duplicate" => original.Insert(1, "\"version\":1,"),
            "version" => original.Replace("\"version\":1", "\"version\":2", StringComparison.Ordinal),
            "kind" => original.Replace("dag-run-created-v1", "dag-run-unknown-v1", StringComparison.Ordinal),
            "missing" => original.Replace("\"aggregateKind\":\"dag-run-v1\",", "", StringComparison.Ordinal),
            "guid" => original.Replace("01900000-0000-7000-8000-00000000000a", "01900000-0000-7000-8000-00000000000A",
                StringComparison.Ordinal),
            _ => original.Replace("\\u002B00:00", "\\u002B01:00", StringComparison.Ordinal)
        };
        text.Should().NotBe(original, "the negative control must actually alter the wire bytes");

        Action decode = () => DagRunRecordCodecV1.Deserialize(Encoding.UTF8.GetBytes(text));
        decode.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void ValueAndGraph_DefensivelyCopyCallerBuffersAndCollections()
    {
        byte[] bytes = "null"u8.ToArray();
        var value = new DagRunValueV1(true, "input", "orcacore-json-v1", bytes,
            Convert.ToHexString(SHA256.HashData(bytes)));
        bytes[0] = (byte)'X';
        value.Bytes[0] = (byte)'Y';
        value.Bytes.Should().Equal("null"u8.ToArray());

        var nodes = new List<DagRunNodeV1>(Created().CreationSnapshot!.Graph.Nodes);
        var graph = new DagRunGraphV1(nodes[0].Workflow, "input", nodes);
        nodes.Clear();
        graph.Nodes.Should().ContainSingle();
    }

    [Fact]
    public void Value_RejectsWellFormedButIncorrectCommittedByteHash()
    {
        Action create = () => new DagRunValueV1(true, "input", "orcacore-json-v1",
            "null"u8.ToArray(), new string('A', 64));
        create.Should().Throw<InvalidDataException>();
    }

    private static DagRunRecordV1 Created()
    {
        var run = Guid.Parse("01900000-0000-7000-8000-00000000000a");
        var time = new DateTimeOffset(2026, 10, 29, 0, 0, 0, TimeSpan.Zero);
        var binding = new DagRunBindingV1(DefinitionId.Parse("01900000-0000-7000-8000-00000000000b"),
            DefinitionVersion.Initial, new string('A', 64));
        var graph = new DagRunGraphV1(binding, "input",
            [new DagRunNodeV1(0, "node", binding, "input", null, [])]);
        var input = new DagRunValueV1(true, "input", "orcacore-json-v1", "null"u8.ToArray(),
            Convert.ToHexString(SHA256.HashData("null"u8)));
        var snapshot = new DagRunSnapshotV1(run, new StreamVersion(1), new DagRunStartKeyV1("key"),
            graph, input, DagRunStateV1.Running,
            [new DagRunNodeStateV1(0, DagNodeStateV1.Ready, null, null, null, null, time, null, null)],
            time, time, null);
        return new DagRunRecordV1(1, DagRunRecordKindV1.Created, run,
            Guid.Parse("01900000-0000-7000-8000-00000000000c"), new StreamVersion(1),
            DagRunStateV1.Running, snapshot, [], time, null);
    }
}
