namespace OrcaCore.TestSupport;

/// <summary>Canonical xUnit trait names/keys so filters stay typo-proof across test projects.</summary>
public static class Traits
{
    public const string AcceptanceCriterion = "AC";
    public const string Category = "Category";

    public static class Categories
    {
        public const string Certification = "Certification";
        public const string Container = "Container";
    }
}
