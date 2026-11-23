using System.Text;
using AwesomeAssertions;
using OrcaCore.Core.Execution;
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

    [Fact]
    [Trait("AC", "AC-328")]
    public void FixedCodec_RejectsLossyValuesUnderTheirDeclaredType()
    {
        var value = PrivateSetter.Create(17);
        Action member = () => CoreWorkflowValueCodec.Serialize(value, typeof(PrivateSetter));
        Action nested = () => CoreWorkflowValueCodec.Serialize(new[] { value }, typeof(PrivateSetter[]));
        Action constructor = () => CoreWorkflowValueCodec.Serialize(new OffByOne(17), typeof(OffByOne));
        Action unstable = () => CoreWorkflowValueCodec.Serialize(new ChangingGetter(), typeof(ChangingGetter));
        Action rejectedConstructor = () => CoreWorkflowValueCodec.Serialize(CancellingConstructor.Create(), typeof(CancellingConstructor));
        member.Should().Throw<NotSupportedException>().WithMessage("*PrivateSetter*round-trip*");
        nested.Should().Throw<NotSupportedException>();
        constructor.Should().Throw<NotSupportedException>();
        unstable.Should().Throw<NotSupportedException>();
        rejectedConstructor.Should().Throw<NotSupportedException>().WithInnerException<OperationCanceledException>();
        var stored = """{"Value":17}"""u8.ToArray();
        Action reopen = () => CoreWorkflowValueCodec.Deserialize(stored, typeof(PrivateSetter));
        reopen.Should().Throw<InvalidDataException>();
    }

    [Fact]
    [Trait("AC", "AC-328")]
    public void FixedCodec_DetachesTheCapturedValueAndPreservesConstructorAndComputedMembers()
    {
        var original = new Dictionary<string, Immutable> { ["β"] = new(17) };
        var bytes = CoreWorkflowValueCodec.Serialize(original, original.GetType(), out var detached);
        original.Clear();
        var copy = Assert.IsType<Dictionary<string, Immutable>>(detached);
        copy["β"].Twice.Should().Be(34);
        CoreWorkflowValueCodec.Serialize(copy, original.GetType()).Should().Equal(bytes);
        CoreWorkflowValueCodec.Serialize(null, typeof(int?), out detached).Should().Equal("null"u8.ToArray());
        detached.Should().BeNull();
        Action nullInteger = () => CoreWorkflowValueCodec.Serialize(null, typeof(int));
        nullInteger.Should().Throw<NotSupportedException>();
    }

    private sealed class PrivateSetter
    {
        public int Value { get; private set; }
        public static PrivateSetter Create(int value) => new() { Value = value };
    }

    [Fact]
    public void ForEach_RejectsOversizeBeforeConstructingTheDetachedItems()
    {
        var value = SizeProbe.Create(new string('x', CoreWorkflowValueCodec.MaxEncodedValueBytes));
        Action capture = () => ForEachSnapshotMaterializer.Materialize(new[] { value }, typeof(SizeProbe));
        capture.Should().Throw<StructuredExecutionLimitException>().WithMessage("*exceeding*");
    }

    [Fact]
    public void BranchResultsEnforceTheConfiguredByteLimitBeforeConstructingADetachedResult()
    {
        const int maximumBytes = 32;
        var value = SizeProbe.Create(new string('x', maximumBytes));
        Action capture = () => BranchResultMaterializer.Serialize(value, typeof(SizeProbe), maximumBytes);
        capture.Should().Throw<StructuredExecutionLimitException>()
            .Which.Code.Should().Be(StructuredExecutionLimitCodes.SerializedResultExceeded);
        BranchResultMaterializer.Serialize("ab", typeof(string), 4).Should().Equal("\"ab\""u8.ToArray());
    }

    private sealed class SizeProbe
    {
        private SizeProbe() { }
        public SizeProbe(string value) => throw new InvalidOperationException("Oversize input must not reach the decoder.");
        public string Value { get; private set; } = string.Empty;
        public static SizeProbe Create(string value) => new() { Value = value };
    }

    private sealed class OffByOne(int value)
    {
        public int Value { get; } = value + 1;
    }

    private sealed class ChangingGetter
    {
        private static int reads;
        public int Value => Interlocked.Increment(ref reads);
    }

    private sealed class CancellingConstructor
    {
        private CancellingConstructor() { }
        public CancellingConstructor(int value) => throw new OperationCanceledException();
        public int Value => 17;
        public static CancellingConstructor Create() => new();
    }

    private sealed record Immutable(int Value)
    {
        public int Twice => 2 * Value;
    }
}
