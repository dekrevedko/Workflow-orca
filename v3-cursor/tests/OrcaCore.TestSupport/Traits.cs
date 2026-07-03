namespace OrcaCore.TestSupport;

/// <summary>
/// Canonical xUnit trait names and values for typo-proof filtering.
/// </summary>
public static class Traits
{
    public const string AcceptanceCriteria = "AC";

    public const string Category = "Category";

    public const string Certification = "Certification";

    public const string Container = "Container";

    public static string AcceptanceCriterion(string id) => id;
}
