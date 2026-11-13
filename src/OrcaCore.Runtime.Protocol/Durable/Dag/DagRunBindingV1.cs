namespace OrcaCore.Abstractions.Durable.Dag;

/// <summary>An exact installed definition binding; storage never constructs opaque application fingerprints.</summary>
public sealed record DagRunBindingV1(
    DefinitionId DefinitionId,
    DefinitionVersion DefinitionVersion,
    string Fingerprint)
{
    public DefinitionId DefinitionId { get; init; } = DagRunValidation.Required(DefinitionId);
    public DefinitionVersion DefinitionVersion { get; init; } = DagRunValidation.Required(DefinitionVersion);
    public string Fingerprint { get; init; } = DagRunValidation.Fingerprint(Fingerprint);
}
