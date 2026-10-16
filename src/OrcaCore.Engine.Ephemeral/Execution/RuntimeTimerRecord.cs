using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Definitions;

namespace OrcaCore.Engine.Ephemeral.Execution;

internal sealed class RuntimeTimerRecord
{
    private Action? cancel;

    internal RuntimeTimerRecord(BranchId? branchId, DateTimeOffset registeredAt)
    {
        TimerId = TimerId.New();
        BranchId = branchId;
        RegisteredAt = registeredAt;
    }

    internal TimerId TimerId { get; }

    internal BranchId? BranchId { get; }

    internal DateTimeOffset RegisteredAt { get; }

    internal void SetCancel(Action cancel)
    {
        ArgumentNullException.ThrowIfNull(cancel);

        this.cancel = cancel;
    }

    internal void Cancel()
    {
        cancel?.Invoke();
    }
}
