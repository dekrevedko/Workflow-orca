using AwesomeAssertions;
using Xunit;

namespace OrcaCore.Core.Tests.Contracts;

public sealed class WorkflowEventContractTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void EventContractVersion_RequiresAPositiveValue(int value)
    {
        var act = () => new EventContractVersion(value);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("value");
    }

    [Fact]
    public void Descriptors_UseOnlyTheApplicationOwnedNameAndVersionAsStableIdentity()
    {
        var name = EventName.Create("OrderApproved");
        var payloadless = WorkflowEventContract.Create(name, new EventContractVersion(2));
        var typed = WorkflowEventContract<ThirdPartyMessage>.Create(
            EventName.Create("OrderApproved"),
            new EventContractVersion(2));
        var anotherTypedBinding = WorkflowEventContract<AnotherThirdPartyMessage>.Create(
            EventName.Create("OrderApproved"),
            new EventContractVersion(2));

        payloadless.Should().Be(typed);
        typed.Should().Be(anotherTypedBinding);
        payloadless.GetHashCode().Should().Be(typed.GetHashCode());
        typed.GetHashCode().Should().Be(anotherTypedBinding.GetHashCode());
        payloadless.Should().NotBe(WorkflowEventContract.Create(name, new EventContractVersion(3)));
        payloadless.Should().NotBe(WorkflowEventContract.Create(
            EventName.Create("OrderRejected"),
            new EventContractVersion(2)));
    }

    [Fact]
    public void Initial_IsValueOneAndEqualAcrossInstances()
    {
        EventContractVersion.Initial.Value.Should().Be(1);
        EventContractVersion.Initial.Should().Be(new EventContractVersion(1));
    }

    private sealed class ThirdPartyMessage
    {
        public string Value { get; init; } = string.Empty;
    }

    private sealed class AnotherThirdPartyMessage
    {
        public string Value { get; init; } = string.Empty;
    }
}
