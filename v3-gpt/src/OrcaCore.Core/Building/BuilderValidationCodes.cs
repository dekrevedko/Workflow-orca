namespace OrcaCore.Core.Building;

/// <summary>
/// Stable validation codes produced by workflow definition building.
/// </summary>
public static class BuilderValidationCodes
{
    /// <summary>
    /// The definition does not contain an Init node.
    /// </summary>
    public const string MissingInit = "WF001_MISSING_INIT";

    /// <summary>
    /// The definition does not contain a reachable End node.
    /// </summary>
    public const string MissingEnd = "WF002_MISSING_END";

    /// <summary>
    /// A parallel node has no branches.
    /// </summary>
    public const string EmptyParallel = "WF003_EMPTY_PARALLEL";

    /// <summary>
    /// A parallel branch has no body.
    /// </summary>
    public const string EmptyBranch = "WF004_EMPTY_BRANCH";

    /// <summary>
    /// A parallel node has duplicate branch names.
    /// </summary>
    public const string DuplicateBranchName = "WF005_DUPLICATE_BRANCH";

    /// <summary>
    /// A while node has no body.
    /// </summary>
    public const string EmptyWhile = "WF006_EMPTY_WHILE";

    /// <summary>
    /// A required delegate is null.
    /// </summary>
    public const string NullDelegate = "WF007_NULL_DELEGATE";
}
