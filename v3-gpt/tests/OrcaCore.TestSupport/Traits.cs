namespace OrcaCore.TestSupport;

/// <summary>
/// Provides canonical trait names and values used by test filters.
/// </summary>
public static class Traits
{
    /// <summary>
    /// Trait key for acceptance criteria, with values such as AC-001.
    /// </summary>
    public const string AcceptanceCriteria = "AC";

    /// <summary>
    /// Trait key for broad test categories.
    /// </summary>
    public const string Category = "Category";

    /// <summary>
    /// Category value for provider certification tests.
    /// </summary>
    public const string Certification = "Certification";

    /// <summary>
    /// Category value for tests that require container infrastructure.
    /// </summary>
    public const string Container = "Container";

    /// <summary>
    /// Creates an acceptance-criteria trait pair.
    /// </summary>
    public static (string Key, string Value) AcceptanceCriterion(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return (AcceptanceCriteria, id);
    }
}
