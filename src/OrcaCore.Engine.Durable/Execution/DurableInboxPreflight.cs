using System.Diagnostics;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Engine.Durable.Execution;

internal static class DurableInboxPreflight
{
    internal static DurableCommandResult? TryCreateResult(Option<InboxRecordState> inboxState)
    {
        if (!inboxState.HasValue || inboxState.Value == InboxRecordState.Received)
        {
            return null;
        }

        return inboxState.Value switch
        {
            InboxRecordState.Applied or
                InboxRecordState.DuplicateIgnored or
                InboxRecordState.DiscardedOnResume => new DurableCommandResult(
                    DurableCommandOutcome.NoOp,
                    "Inbound event was already applied.",
                    StreamVersion.Empty),
            InboxRecordState.Poisoned => new DurableCommandResult(
                DurableCommandOutcome.Poisoned,
                "Inbound event was previously recorded as poisoned.",
                StreamVersion.Empty),
            InboxRecordState.Received => throw new UnreachableException(),
            _ => throw new UnreachableException()
        };
    }
}
