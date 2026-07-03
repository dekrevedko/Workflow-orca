using OrcaCore.Abstractions.Steps;

namespace OrcaCore.Core.Definitions;

/// <summary>
/// A business step reached by factory, never by captured instance (CR-003): steps are
/// passive and constructed fresh per activation so no execution state leaks across runs.
/// </summary>
/// <param name="CreateStep">Produces a fresh step instance. MUST be pure/deterministic (CR-012).</param>
internal sealed record BusinessStepNode<TState>(Func<IStep<TState>> CreateStep) : DefinitionNode;
