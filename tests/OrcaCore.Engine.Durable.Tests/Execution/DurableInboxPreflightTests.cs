using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Execution;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Execution;

public sealed class DurableInboxPreflightTests
{
    [Fact]
    public void TryCreateResult_WhenInboxStateIsAbsent_Continues()
    {
        var result = DurableInboxPreflight.TryCreateResult(Option<InboxRecordState>.None);

        result.Should().BeNull();
    }

    [Fact]
    public void TryCreateResult_WhenInboxStateIsReceived_Continues()
    {
        var result = DurableInboxPreflight.TryCreateResult(Option<InboxRecordState>.Some(InboxRecordState.Received));

        result.Should().BeNull();
    }

    [Theory]
    [InlineData(InboxRecordState.Applied)]
    [InlineData(InboxRecordState.DuplicateIgnored)]
    [InlineData(InboxRecordState.DiscardedOnResume)]
    public void TryCreateResult_WhenInboxStateAlreadyResolved_ReturnsNoOp(InboxRecordState state)
    {
        var result = DurableInboxPreflight.TryCreateResult(Option<InboxRecordState>.Some(state));

        result.Should().NotBeNull();
        result!.Outcome.Should().Be(DurableCommandOutcome.NoOp);
        result.Message.Should().Be("Inbound event was already applied.");
        result.StreamVersion.Should().Be(StreamVersion.Empty);
    }

    [Fact]
    public void TryCreateResult_WhenInboxStateIsPoisoned_ReturnsPoisoned()
    {
        var result = DurableInboxPreflight.TryCreateResult(Option<InboxRecordState>.Some(InboxRecordState.Poisoned));

        result.Should().NotBeNull();
        result!.Outcome.Should().Be(DurableCommandOutcome.Poisoned);
        result.Message.Should().Be("Inbound event was previously recorded as poisoned.");
        result.StreamVersion.Should().Be(StreamVersion.Empty);
    }
}
