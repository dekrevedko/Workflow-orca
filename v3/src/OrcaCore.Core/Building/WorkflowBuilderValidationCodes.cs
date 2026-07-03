namespace OrcaCore.Core.Building;

/// <summary>Stable <see cref="Abstractions.Primitives.ValidationError.Code"/> values for builder validation (CR-002).</summary>
public static class WorkflowBuilderValidationCodes
{
    public const string MissingInit = "ORCA-BUILD-001";
    public const string MissingReachableEnd = "ORCA-BUILD-002";
    public const string EmptyParallel = "ORCA-BUILD-003";
    public const string DuplicateBranchName = "ORCA-BUILD-004";
    public const string WhileWithoutBody = "ORCA-BUILD-005";
    public const string NullCondition = "ORCA-BUILD-006";
    public const string NullDelegate = "ORCA-BUILD-007";
}
