using System.Text;
using AwesomeAssertions;
using OrcaCore.Core.Internal;
using Xunit;

namespace OrcaCore.Core.Tests.Serialization;

public sealed class FixedWorkflowValueCodecCollectionTests
{
    [Fact]
    public void FixedCodec_UsesOneOrderedSequenceAndOneStringKeyedMapRepresentation()
    {
        var sequence = new List<string> { "second", "first" };
        var sequenceBytes = CoreWorkflowValueCodec.Serialize(
            sequence,
            typeof(IReadOnlyList<string>));

        Encoding.UTF8.GetString(sequenceBytes).Should().Be("[\"second\",\"first\"]");
        CoreWorkflowValueCodec.Deserialize(sequenceBytes, typeof(IReadOnlyList<string>))
            .Should().BeAssignableTo<IReadOnlyList<string>>()
            .Which.Should().Equal("second", "first");

        var map = new Dictionary<string, int>
        {
            ["second"] = 2,
            ["first"] = 1
        };
        var mapBytes = CoreWorkflowValueCodec.Serialize(
            map,
            typeof(IReadOnlyDictionary<string, int>));

        Encoding.UTF8.GetString(mapBytes).Should().Be("{\"second\":2,\"first\":1}");
        CoreWorkflowValueCodec.Deserialize(mapBytes, typeof(IReadOnlyDictionary<string, int>))
            .Should().BeAssignableTo<IReadOnlyDictionary<string, int>>()
            .Which.Select(pair => pair.Key).Should().Equal("second", "first");
    }

    [Fact]
    public void FixedCodec_RejectsEveryCollectionShapeOutsideTheClosedAllowlist()
    {
        var unsupportedDeclaredTypes = new[]
        {
            typeof(HashSet<string>),
            typeof(Queue<string>),
            typeof(LinkedList<string>),
            typeof(IReadOnlyCollection<string>),
            typeof(SortedDictionary<string, int>),
            typeof(Dictionary<int, string>),
            typeof(string[,])
        };
        unsupportedDeclaredTypes.Should().OnlyContain(type =>
            !CoreWorkflowValueCodec.IsSupportedDeclaredType(type));

        Action customSequence = () => CoreWorkflowValueCodec.Serialize(
            new CustomStringList { "value" },
            typeof(IReadOnlyList<string>));
        customSequence.Should().Throw<NotSupportedException>().WithMessage("*sequence/map allowlist*");

        Action customMap = () => CoreWorkflowValueCodec.Serialize(
            new SortedDictionary<string, int> { ["value"] = 1 },
            typeof(IReadOnlyDictionary<string, int>));
        customMap.Should().Throw<NotSupportedException>().WithMessage("*sequence/map allowlist*");
    }

    private sealed class CustomStringList : List<string>
    {
    }
}
