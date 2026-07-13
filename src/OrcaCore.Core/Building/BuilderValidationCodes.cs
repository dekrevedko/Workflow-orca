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

    /// <summary>
    /// A delay node has a non-positive duration.
    /// </summary>
    public const string NonPositiveDelay = "WF008_NON_POSITIVE_DELAY";

    /// <summary>
    /// A wait timeout has a non-positive duration.
    /// </summary>
    public const string NonPositiveTimeout = "WF009_NON_POSITIVE_TIMEOUT";

    /// <summary>
    /// A retry policy is structurally invalid.
    /// </summary>
    public const string InvalidRetryPolicy = "WF010_INVALID_RETRY_POLICY";

    /// <summary>
    /// A ForEach node has no body.
    /// </summary>
    public const string EmptyForEach = "WF011_EMPTY_FOREACH";

    /// <summary>
    /// A max concurrency value is non-positive.
    /// </summary>
    public const string NonPositiveMaxConcurrency = "WF012_NON_POSITIVE_MAX_CONCURRENCY";

    /// <summary>
    /// A DAG node id is duplicated.
    /// </summary>
    public const string DagDuplicateNode = "WF013_DAG_DUPLICATE_NODE";

    /// <summary>
    /// A DAG edge references a missing node.
    /// </summary>
    public const string DagMissingNode = "WF014_DAG_MISSING_NODE";

    /// <summary>
    /// A DAG contains a cycle.
    /// </summary>
    public const string DagCycle = "WF015_DAG_CYCLE";

    /// <summary>
    /// A saga compensation scope id is duplicated.
    /// </summary>
    public const string SagaDuplicateScope = "WF016_SAGA_DUPLICATE_SCOPE";
}
