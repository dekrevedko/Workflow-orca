namespace OrcaCore.Core.Definitions;

/// <summary>
/// The single owner of input-to-state construction (CR-005). No parallel initialization
/// path exists on the definition root.
/// </summary>
/// <param name="CreateState">
/// Converts start input into initial business state. MUST be pure/deterministic (CR-012,
/// NF-020): no I/O, no wall-clock reads, no randomness outside injected seams.
/// </param>
public sealed record InitNode<TState, TInput>(Func<TInput, TState> CreateState) : DefinitionNode;
