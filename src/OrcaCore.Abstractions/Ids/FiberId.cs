namespace OrcaCore.Abstractions.Ids;

/// <summary>
/// Identifies one logical execution fiber inside a workflow instance.
/// </summary>
public readonly record struct FiberId(string Value)
{
    public override string ToString() => Value;
}

/// <summary>
/// Identifies one structured execution scope inside a workflow instance.
/// </summary>
public readonly record struct ScopeId(string Value)
{
    public override string ToString() => Value;
}
