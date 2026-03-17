
namespace OrcaCore.Runtime.Durable.Persistence;

public enum CorrelationMatchType
{
    NoMatch,
    SingleMatch,
    Ambiguous
}

public sealed record CorrelationMatch(
    string InstanceId,
    string DefinitionId,
    string DefinitionVersion,
    WaitMode Mode);

public sealed record CorrelationLookupResult(
    CorrelationMatchType MatchType,
    IReadOnlyList<CorrelationMatch> Matches);
