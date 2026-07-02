namespace OrcaCore.Core.Definitions;

internal readonly record struct BranchId
{
    internal BranchId(int ordinal, string name)
    {
        if (ordinal < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ordinal), ordinal, "Branch ordinal cannot be negative.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Ordinal = ordinal;
        Name = name;
    }

    internal int Ordinal { get; }

    internal string Name { get; }

    public override string ToString()
    {
        return $"{Ordinal}:{Name}";
    }
}
