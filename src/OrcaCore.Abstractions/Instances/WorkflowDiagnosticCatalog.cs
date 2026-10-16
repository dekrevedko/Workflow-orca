namespace OrcaCore;

internal static class WorkflowDiagnosticCatalog
{
    internal static IReadOnlyDictionary<string, WorkflowDiagnosticDescriptor> All { get; } =
        new Dictionary<string, WorkflowDiagnosticDescriptor>(StringComparer.Ordinal)
        {
            ["SFE-AUTH-ROOT-001"] = Error("MissingRootInit", "no root initialization exists."),
            ["SFE-AUTH-ROOT-002"] = Error("MultipleRootInit", "more than one root initialization exists."),
            ["SFE-AUTH-ROOT-003"] = Error("MissingRootTerminal", "no root `End` or approved terminal `ContinueAsNew` exists."),
            ["SFE-AUTH-ROOT-004"] = Error("MultipleRootTerminal", "more than one root generation terminal exists."),
            ["SFE-AUTH-PATH-001"] = Error("IncompleteSuccessfulPath", "a reachable successful path does not converge on the generation terminal."),
            ["SFE-AUTH-CAP-001"] = Error("CapabilityNotAvailable", "a hand-built/stale graph uses a member outside its selected mode/location."),
            ["SFE-AUTH-BRANCH-001"] = Error("DuplicateBranchIdentity", "fixed branch identities collide."),
            ["SFE-AUTH-BRANCH-002"] = Error("MissingBranchReturn", "a reachable successful branch/item path has no typed return."),
            ["SFE-AUTH-BRANCH-003"] = Error("MultipleBranchReturn", "a branch/item has more than one return terminal."),
            ["SFE-AUTH-BRANCH-004"] = Error("EmptyParallelScope", "a fixed root `Parallel` contains no authored branch."),
            ["SFE-AUTH-JOIN-001"] = Error("MissingJoin", "a fan-out has no selected join."),
            ["SFE-AUTH-JOIN-002"] = Error("InvalidMergeContract", "result/state/merge contracts do not agree."),
            ["SFE-AUTH-DECORATOR-001"] = Error("MisplacedDecorator", "a retry/timeout/transient decorator is repeated or has no eligible immediately preceding business step."),
            ["SFE-AUTH-DEADLINE-001"] = Error("DuplicateWorkflowDeadline", "`CompleteWithin` is selected more than once; the second call is primary and the first is related."),
            ["SFE-AUTH-LIFECYCLE-001"] = Error("SupersededBuilderHandle", "a root builder handle belongs to an earlier authoring epoch or has a required join pending."),
            ["SFE-AUTH-LIFECYCLE-002"] = Error("JoinAlreadySelected", "the required join stage has already selected a terminal join operation."),
            ["SFE-AUTH-LIFECYCLE-003"] = Error("FrozenAuthoringSession", "the root authoring session has already selected its generation terminal and is immutable."),
            ["SFE-AUTH-LIFECYCLE-004"] = Error("ExpiredLexicalBuilderHandle", "a callback-local nested, branch, item, leased, or scope builder escaped its lexical callback."),
            ["SFE-AUTH-LIFECYCLE-005"] = Error("ConcurrentAuthoringConflict", "another authoring operation owns the session operation gate."),
            ["SFE-AUTH-LOOP-001"] = Error("NonProgressingLoop", "a root loop cycle can repeat without business work, suspension, failure, rollover, or exit."),
            ["SFE-AUTH-LEASE-001"] = Error("LeaseAncestryConflict", "acquisition is reachable under a live capacity-reserving lexical ancestor; primary/related locations identify both scopes."),
            ["SFE-AUTH-LEASE-003"] = Error("LeaseBlocksContinueAsNew", "rollover is reached before a lexical lease scope exits."),
            ["SFE-TYPE-001"] = Error("IncompatibleStateOrResultType", "a hand-built graph has incompatible state/result/output contracts."),
            ["SFE-TYPE-002"] = Error("CodecUnsupportedShape", "a required persisted/detached value shape cannot use `orcacore-json-v1`."),
            ["SFE-LIMIT-001"] = Error("InvalidForEachLimit", "a hand-built fan-out bypasses positive item/concurrency bounds."),
            ["SFE-RUN-001"] = Error("NonQuiescentContinueAsNew", "runtime defense rejects rollover while a descendant or owned obligation remains."),
            ["SFE-RUN-002"] = Error("LeaseAncestryViolation", "runtime defense terminally fails before queue/pool mutation and suppresses merges/restart loops."),
            ["DAG-AUTH-NODE-001"] = Error("DuplicateNodeIdentity", "node identities collide."),
            ["DAG-AUTH-DEPENDENCY-001"] = Error("DuplicateDependency", "one node repeats a declared dependency."),
            ["DAG-AUTH-DEPENDENCY-002"] = Error("SelfDependency", "a node depends on itself."),
            ["DAG-AUTH-DEPENDENCY-003"] = Error("Cycle", "declared edges contain a cycle."),
            ["DAG-AUTH-DEPENDENCY-004"] = Error("ForeignPlanReference", "a declared node/dependency reference belongs to another plan."),
            ["DAG-AUTH-MAP-001"] = Error("MissingMapInput", "a node has no mapping delegate."),
            ["DAG-AUTH-MAP-002"] = Error("DuplicateMapInput", "a node assigns mapping more than once.")
        };

    internal static WorkflowDiagnosticDescriptor Require(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        return All.TryGetValue(code, out var descriptor)
            ? descriptor
            : throw new ArgumentException($"Undocumented workflow diagnostic code '{code}'.", nameof(code));
    }

    private static WorkflowDiagnosticDescriptor Error(string name, string meaning) =>
        new(name, meaning, WorkflowDiagnosticSeverity.Error);
}

internal sealed record WorkflowDiagnosticDescriptor(
    string Name,
    string Meaning,
    WorkflowDiagnosticSeverity Severity);
