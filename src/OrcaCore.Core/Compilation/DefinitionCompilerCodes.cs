namespace OrcaCore.Core.Compilation;

/// <summary>
/// Stable diagnostics emitted by the structured definition compiler.
/// </summary>
internal static class DefinitionCompilerCodes
{
    public const string MissingRootInit = "SFE-AUTH-001_MISSING_ROOT_INIT";
    public const string MissingRootEnd = "SFE-AUTH-002_MISSING_ROOT_END";
    public const string MultipleRootInit = "SFE-AUTH-003_MULTIPLE_ROOT_INIT";
    public const string RootInitNotFirst = "SFE-AUTH-004_ROOT_INIT_NOT_FIRST";
    public const string MultipleRootEnd = "SFE-AUTH-005_MULTIPLE_ROOT_END";
    public const string NodeAfterRootEnd = "SFE-AUTH-006_NODE_AFTER_ROOT_END";
    public const string MissingBranchReturn = "SFE-AUTH-007_MISSING_BRANCH_RETURN";
    public const string MultipleBranchReturn = "SFE-AUTH-008_MULTIPLE_BRANCH_RETURN";
    public const string UnreachableNode = "SFE-AUTH-009_UNREACHABLE_NODE";
    public const string BlankBranchIdentity = "SFE-AUTH-010_BLANK_BRANCH_IDENTITY";
    public const string DuplicateBranchIdentity = "SFE-AUTH-011_DUPLICATE_BRANCH_IDENTITY";
    public const string NestedRootInit = "SFE-AUTH-012_NESTED_ROOT_INIT";
    public const string NestedRootEnd = "SFE-AUTH-013_NESTED_ROOT_END";
    public const string UnsupportedInstruction = "SFE-CAP-001_UNSUPPORTED_INSTRUCTION";
    public const string SerializerUnavailable = "SFE-TYPE-001_SERIALIZER_UNAVAILABLE";
    public const string BranchResultMismatch = "SFE-TYPE-002_BRANCH_RESULT_MISMATCH";
    public const string MaxInternalInstructionsNotPositive =
        "SFE-LIMIT-001_MAX_INTERNAL_INSTRUCTIONS_NOT_POSITIVE";
    public const string MaxScopeDepthNotPositive = "SFE-LIMIT-002_MAX_SCOPE_DEPTH_NOT_POSITIVE";
    public const string MaxSerializedResultBytesNotPositive =
        "SFE-LIMIT-005_MAX_SERIALIZED_RESULT_BYTES_NOT_POSITIVE";
    public const string MaxSerializedEnvelopeBytesNotPositive =
        "SFE-LIMIT-006_MAX_SERIALIZED_ENVELOPE_BYTES_NOT_POSITIVE";
    public const string MaxScopeDepthExceeded = "SFE-LIMIT-007_MAX_SCOPE_DEPTH_EXCEEDED";
    public const string NoProgressLoop = "SFE-PLAN-001_NO_PROGRESS_LOOP";
    public const string ForEachMaxConcurrencyNotPositive =
        "SFE-LIMIT-004_FOREACH_MAX_CONCURRENCY_NOT_POSITIVE";
    public const string ForEachWhenAnyFailurePolicy =
        "SFE-AUTH-014_FOREACH_WHEN_ANY_REQUIRES_FAIL_FAST";
    public const string EmptyStructuredScope = "SFE-AUTH-015_EMPTY_STRUCTURED_SCOPE";
    public const string LeaseAncestryConflict = "SFE-AUTH-016_LEASE_ANCESTRY_CONFLICT";
    public const string LeaseBlocksContinueAsNew = "SFE-AUTH-017_LEASE_BLOCKS_CONTINUE_AS_NEW";
}
