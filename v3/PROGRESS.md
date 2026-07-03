# v3 Implementation Progress

Tracks task completion for the `v3/` workspace specifically. The shared
`docs/implementation/phases/*/PROGRESS.md` files are also written to by an
independent parallel workspace (`v3-gpt/`) — this file is the authoritative,
conflict-free log for `v3/` and is appended to instead going forward.

## Phase 0 — skeleton

T0-01 | done | 2026-07-02 | deviations: v3/global.json pins installed SDK 10.0.301 (rollForward latestFeature) because root global.json requests unavailable 10.0.200; xUnit v3 test projects scaffolded via the official `xunit.v3.templates` (xunit3, mtp-v2 runner) rather than the stock `xunit` template, which still emits xUnit v2; OrcaCore.ProviderCertification uses `xunit.v3.extensibility.core` (plain classlib, not executable) since `xunit.v3`/`xunit.v3.mtp-v2` require an executable test project
T0-02 | done | 2026-07-02 | deviations: workflow file named `ci-v3-workspace.yml` (not `ci-v3.yml`) because that name is already taken by the parallel v3-gpt workspace's workflow; remote green/red GitHub Actions verification deferred — requires a pushed branch, not done without explicit push authorization; local `dotnet build`/`dotnet test` equivalents pass
T0-03 | done | 2026-07-02 | deviations: none
T0-04 | done | 2026-07-02 | deviations: RaceCoordinator's timeout is a real wall-clock CancellationTokenSource (not FakeTimeProvider-driven) because its purpose is to bound actual test-run wall time and fail fast rather than hang; this is a test-harness utility, not production src/ code, so the TimeProvider-everywhere rule doesn't apply to it

## Phase 1 — ephemeral engine core

T1-01 | done | 2026-07-02 | deviations: DefinitionId/DefinitionVersion/CorrelationId are NOT Guid.CreateVersion7-based (only InstanceId/EventId/WaitId are, matching 00-stack-decisions §2's explicit list); DefinitionId and CorrelationId wrap validated non-empty strings, DefinitionVersion wraps a validated positive int; "V7 ordered" verified via ordinal string comparison of two New() ids rather than IComparable (kept surface minimal per T0-03 precedent). CORRECTION 2026-07-02: the ordinal-string-ordering assertion in `Ids_New_AreUniqueAndVersion7Ordered` was flaky (~47% failure rate) — .NET's `Guid.CreateVersion7()` uses random, not monotonic, sub-millisecond bits, so two rapid calls are not guaranteed to sort by string order. Replaced with a deterministic check of the UUIDv7 version/variant marker bits (RFC 9562, via `ToByteArray(bigEndian: true)`), discovered while reviewing T1-03's full-suite test run.
T1-02 | done | 2026-07-02 | deviations: Theory+MemberData over the internal LifecycleTrigger enum hits CS0051/CS0053 (public theory method/property can't expose an internal parameter type even with InternalsVisibleTo, and xUnit requires public test classes) — rewrote as two [Fact] tests looping over private (tuple) fixtures instead, keeping LifecycleTrigger internal per the task's explicit requirement
T1-03 | done | 2026-07-02 | deviations: task says "internal except the definition handle", but WorkflowDefinition<TState>.Root is a SequenceNode of DefinitionNode variants, and the spec-required-public ExecutionPointer/Frame reference BranchId - CS0051/CS0053 inconsistent-accessibility forces the whole closed node hierarchy (DefinitionNode + variants, SequenceNode, ParallelBranch, BranchId) to be public sealed/readonly records rather than internal, since a public type cannot expose an internal one through its public surface; IfNode/WhileNode/WaitNode condition and correlation-selector delegates are typed over object? state (non-generic) rather than TState, since the node tree is untyped data at this layer and typed access belongs to the builder (T1-04)
