using OrcaCore.Abstractions.Events;

namespace OrcaCore.Abstractions.Steps;

/// <summary>
/// The execution context handed to a step. Exposes no runtime callback surface (CR-012):
/// steps read/mutate <see cref="State"/> directly and express control intent only through
/// the returned <see cref="StepResult"/>.
/// </summary>
public sealed class StepContext<TState>
{
    /// <summary>The workflow-owned business state. The only channel through which a step may mutate state.</summary>
    public required TState State { get; init; }

    /// <summary>The event that resumed this instance, set only on the first step after a wait resume (EV-022).</summary>
    public EventEnvelope? ResumedEvent { get; init; }

    /// <summary>The injected time source; steps MUST NOT read wall-clock time directly (NF-020).</summary>
    public required TimeProvider TimeProvider { get; init; }
}
