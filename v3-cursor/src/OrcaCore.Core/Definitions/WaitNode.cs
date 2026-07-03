using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Core.Definitions;

/// <summary>
/// Suspends until a matching named event arrives (EV matching rule).
/// <paramref name="SelectCorrelationId"/> MUST be pure/deterministic (CR-012, NF-020).
/// </summary>
internal sealed record WaitNode(string EventName, Func<object?, CorrelationId> SelectCorrelationId) : DefinitionNode;
