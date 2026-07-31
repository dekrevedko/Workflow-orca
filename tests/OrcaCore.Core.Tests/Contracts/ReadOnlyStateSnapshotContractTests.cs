using System.Reflection;
using AwesomeAssertions;
using Xunit;

namespace OrcaCore.Core.Tests.Contracts;

public sealed class ReadOnlyStateSnapshotContractTests
{
    [Fact]
    public void Snapshot_IsRuntimeCreatedNonPositionalAndReadOnly()
    {
        var type = typeof(ReadOnlyStateSnapshot<MutableState>);

        type.IsClass.Should().BeTrue();
        type.IsSealed.Should().BeTrue();
        type.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Should().BeEmpty();
        type.GetMethod("Deconstruct", BindingFlags.Public | BindingFlags.Instance).Should().BeNull();
        type.GetProperty(nameof(ReadOnlyStateSnapshot<MutableState>.Value))!.SetMethod.Should().BeNull();

        var constructor = type.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).Should()
            .ContainSingle().Subject;
        var state = new MutableState { Value = 7 };
        var snapshot = (ReadOnlyStateSnapshot<MutableState>)constructor.Invoke([state]);

        snapshot.Value.Should().BeSameAs(state);
    }

    private sealed class MutableState
    {
        public int Value { get; init; }
    }
}
