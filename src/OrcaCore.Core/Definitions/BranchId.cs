namespace OrcaCore.Core.Definitions;

public readonly record struct BranchId
{
    public BranchId(int ordinal, string name)
    {
        if (ordinal < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ordinal), ordinal, "Branch ordinal cannot be negative.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Ordinal = ordinal;
        Name = name;
    }

    public int Ordinal { get; }

    public string Name { get; }

    public override string ToString()
    {
        return $"{Ordinal}:{Name}";
    }
}
