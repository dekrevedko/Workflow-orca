namespace OrcaCore.Core.Execution;

internal static class FiberReducer
{
    public static FiberRecord Block(
        FiberRecord fiber,
        FiberBlockedReason reason,
        string obligationId)
    {
        ArgumentNullException.ThrowIfNull(fiber);
        ArgumentException.ThrowIfNullOrWhiteSpace(obligationId);
        RequirePhase(fiber, FiberPhase.Runnable);

        return fiber with
        {
            Phase = FiberPhase.Blocked,
            Blocked = new FiberBlock(reason, obligationId)
        };
    }

    public static FiberRecord Resume(FiberRecord fiber)
    {
        ArgumentNullException.ThrowIfNull(fiber);
        RequirePhase(fiber, FiberPhase.Blocked);

        return fiber with
        {
            Phase = FiberPhase.Runnable,
            Blocked = null
        };
    }

    public static FiberRecord Complete(FiberRecord fiber, byte[]? resultPayload = null)
    {
        ArgumentNullException.ThrowIfNull(fiber);
        RequirePhase(fiber, FiberPhase.Runnable);

        return fiber with
        {
            Phase = FiberPhase.Completed,
            ResultPayload = resultPayload?.ToArray(),
            Blocked = null
        };
    }

    public static FiberRecord Fail(FiberRecord fiber, FiberFailure failure)
    {
        ArgumentNullException.ThrowIfNull(fiber);
        ArgumentNullException.ThrowIfNull(failure);
        RequireNonterminal(fiber);

        return fiber with
        {
            Phase = FiberPhase.Failed,
            Blocked = null,
            Failure = failure
        };
    }

    public static FiberRecord Cancel(FiberRecord fiber, string reason)
    {
        ArgumentNullException.ThrowIfNull(fiber);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        RequireNonterminal(fiber);

        return fiber with
        {
            Phase = FiberPhase.Cancelled,
            Blocked = null,
            CancellationReason = reason
        };
    }

    private static void RequireNonterminal(FiberRecord fiber)
    {
        if (fiber.Phase is FiberPhase.Runnable or FiberPhase.Blocked)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Fiber '{fiber.Id}' is terminal in phase '{fiber.Phase}'.");
    }

    private static void RequirePhase(FiberRecord fiber, FiberPhase expected)
    {
        if (fiber.Phase == expected)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Fiber '{fiber.Id}' must be '{expected}' but is '{fiber.Phase}'.");
    }
}
