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

    /// <summary>
    /// The definition contains more than one root Init node.
    /// </summary>
    public const string MultipleInit = "WF017_MULTIPLE_INIT";

    /// <summary>
    /// The root Init node is not the first authored node.
    /// </summary>
    public const string InitNotFirst = "WF018_INIT_NOT_FIRST";

    /// <summary>
    /// An Init node appears inside nested control flow.
    /// </summary>
    public const string NestedInit = "WF019_NESTED_INIT";

    /// <summary>
    /// The definition contains more than one root End node.
    /// </summary>
    public const string MultipleEnd = "WF020_MULTIPLE_END";

    /// <summary>
    /// An End node appears inside nested control flow.
    /// </summary>
    public const string NestedEnd = "WF021_NESTED_END";

    /// <summary>
    /// An executable node appears after the root End node.
    /// </summary>
    public const string NodeAfterEnd = "WF022_NODE_AFTER_END";

    /// <summary>
    /// A branch identity is empty or whitespace.
    /// </summary>
    public const string BlankBranchIdentity = "WF023_BLANK_BRANCH_IDENTITY";

    /// <summary>
    /// A structured branch does not end in BranchReturn.
    /// </summary>
    public const string MissingBranchReturn = "WF024_MISSING_BRANCH_RETURN";

    /// <summary>
    /// A structured branch contains more than one BranchReturn.
    /// </summary>
    public const string MultipleBranchReturn = "WF025_MULTIPLE_BRANCH_RETURN";

    /// <summary>
    /// BranchReturn is outside the direct exit of an owned branch sequence.
    /// </summary>
    public const string BranchReturnNotAtScopeExit = "WF026_BRANCH_RETURN_NOT_AT_SCOPE_EXIT";

    /// <summary>
    /// ContinueAsNew appears inside a local branch or item fiber.
    /// </summary>
    public const string ContinueAsNewNotRoot = "WF027_CONTINUE_AS_NEW_NOT_ROOT";

    /// <summary>
    /// A node follows an unconditional terminal in the same sequence.
    /// </summary>
    public const string UnreachableNode = "WF028_UNREACHABLE_NODE";
}
