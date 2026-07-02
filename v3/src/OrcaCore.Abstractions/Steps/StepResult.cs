using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Abstractions.Steps;

/// <summary>
/// A step's control intent (CR-011). Closed hierarchy — the runtime switches over the
/// nested variants exhaustively; business-state mutations never travel through results.
/// </summary>
public abstract record StepResult
{
    private StepResult()
    {
    }

    /// <summary>The step finished successfully; the runtime advances to the next step.</summary>
    public sealed record Completed : StepResult;

    /// <summary>The step failed; the instance moves to <c>Failed</c> (CR-014).</summary>
    public sealed record Failed(OrcaCoreException Error) : StepResult;

    /// <summary>The step suspends until a matching event arrives (EV matching rule).</summary>
    public sealed record WaitForEvent(string EventName, CorrelationId CorrelationId) : StepResult;

    /// <summary>Commit progress so far, release the execution lane, resume the same step (CR-017).</summary>
    public sealed record Yield : StepResult;
}
