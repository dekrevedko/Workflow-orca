# 17. Selected-Mode Capability and Signature Matrix

Status: **normative first-release planning baseline with a Section 7B amendment pending independent
approval**, revised 2026-08-01. Product tasks 7.24–7.34 remain blocked until task 7.23 records that
approval. The live remediation and Phase 0 route is
[Review E remediation and Phase 00 status](../review/developer-facing-interface-v1-simplification-review-e-remediation-and-phase-00-status-2026-07-19.md).
The approved root-only fan-out boundary remains recorded in the
[root-only fan-out decision and final revalidation](../review/developer-facing-interface-v1-root-only-fan-out-decision-and-revalidation-2026-07-19.md).

This document is the single public authoring, compiler, package, and concurrency contract
shared by `reshape-developer-facing-interfaces` and `add-runtime-concurrency-limits`. Source,
tests, samples, task plans, or review fixtures that conflict with it are non-conforming. OrcaCore
has not shipped; superseded provisional members are deleted rather than aliased, deprecated, or
left as placeholders.

## 17.1 Selected-mode capability matrix

`Portable` means the authored business meaning is the same in both engines. Durable mode may
persist state, timers, waits, and admission obligations without introducing another authoring
member. A mode-first builder exposes only capabilities implemented by every supported host for
that mode.

| Capability | Ephemeral workflow | Durable workflow | First-release contract |
|---|---|---|---|
| Typed input, state, and output | Portable | Portable | `Init<TInput>` creates private state; `End<TOutput>` atomically commits typed output plus optional fixed `WorkflowOutcomeName`. Resultless workflows use one-arity definitions/references. |
| Named business step | Portable | Portable | `Then<TStep>()`; host DI creates the step. `StepExecutionContext` supplies stable logical-operation identity. |
| Lambda step body | Ephemeral only | Absent | Both overloads return `ValueTask`; synchronous work returns `ValueTask.CompletedTask`. No public void-return `Action<StepContext<...>>` can bind an `async void` body. Durable definitions require named step types. |
| Root `If` and root `While` | Portable | Portable | Structured, deterministic control flow. |
| Nested `If` | Portable | Portable | Available in branch, item, conditional, loop, and leased-scope bodies. |
| Nested `While` | Absent | Absent | Deferred; `While` is root-sequence only in v1. |
| Root `Parallel(...).WhenAll(...)` | Portable | Portable | Fixed isolated root branches; waits for every terminal branch; success merge runs only if all succeed. |
| Root `Parallel(...).WhenAllOutcomes(...)` | Portable | Portable | Waits for every terminal root branch and merges ordered success/failure outcomes; a following root `If` decides business acceptance. Ancestor cancellation/deadline suppresses the merge. |
| Nested `Parallel` | Absent | Absent | Deferred with every other nested fan-out shape; no conditional/loop, branch, item, or leased builder exposes it in v1. |
| `WhenFirst` | Absent | Absent | Deferred until winner, loser cancellation, residual-work, and lease semantics are approved together. |
| Root `ForEach(...).WhenAll*` | Portable | Portable | One finite item snapshot commits per scope; explicit item bound and optional node concurrency cap; stable item-index ordering. Durable mode commits before item admission. |
| Nested `ForEach` | Absent | Absent | Deferred to keep the first-release state/limit model bounded. |
| Structural `Wait` | Portable | Portable | Payloadless/typed `WorkflowEventContract` descriptor plus correlation selector. Durable mode may buffer before registration, evict a parked wait, and later rehydrate it; residency is host policy, not workflow meaning. |
| Dynamic `StepResult.WaitForEvent` | Portable | Portable | Payloadless/typed descriptor form retained only when a business step must select the event after it runs. Structural `Wait` remains preferred. |
| Workflow-authored `Publish` | Absent | Durable sequential root, nested, branch, item, and leased builders | Payloadless/typed descriptor plus correlation/payload selectors; workflow progress and the transport-neutral external outbox event commit atomically. |
| Durable event ingress | Absent | Durable hosting only | `IWorkflowEventIngress` accepts one self-routing envelope using direct, correlation, definition-fanout, or exact-definition start-or-deliver routing and returns a closed acknowledgement-safe result. |
| `Delay` | Portable | Portable | Durable mode persists the timer. |
| `WaitLong` | Removed | Removed | No alias or tombstone. Durable `Wait` is cold-capable automatically. |
| `Yield` | Removed | Removed | No author control intent. The runtime owns internal execution quanta and checkpoint scheduling. |
| `CompleteWithin` | Portable | Portable | Bounds the whole workflow from start, including queueing, retries, delays, waits, and cleanup decision; durable deadline is persisted. |
| `WithStepTimeout` | Portable | Portable | Bounds one step attempt. It is runtime orchestration, not a Polly policy. |
| `ContinueAsNew` | Absent | Durable root only | Unconditional/perpetual generation terminal returning `DurableWorkflowCompletionBuilder<TInput>`; valid only at a quiescent root after every lexical resource scope has exited. Conditional finite rollover is deferred. |
| Public child-workflow members | Absent | Absent | `RunChild`/`RunChildren` are deferred public APIs. `OrcaCore.Dag` uses an internal child-start/join protocol. |
| Public external-job composite | Absent | Absent | `RunExternalJob` is deferred. A typed durable step may make a bounded idempotent create-or-observe call using `StepOperationId`, then use normal `Wait`. |
| Durable resource lease | Absent | Durable root, root-nested `If`/`While`, and independent durable root-`Parallel` branch/root-`ForEach` item bodies | Scoped-only `AcquireResources(request, body)` when no live ancestor lease exists. A leased body exposes sequencing, nested `If`, `Wait`, and `Delay`, but no fan-out, nested acquisition, or `ContinueAsNew`. Retryable ambiguity remains capacity-reserving `AmbiguousHeld`; quarantine occurs only before progress from an ambiguous exit/terminal path. |
| Named transient pool | Ephemeral only | Absent | Host-local capacity identified by `TransientPoolName`; never confused with a durable lease. |
| Saga | Absent | Absent | Deferred and documented as future work; no public builder, definition, adapter, or placeholder member ships in v1. |
| DAG planning | Separate `OrcaCore.Dag` project | Separate `OrcaCore.Dag` project | Typed immutable DAG input, node references, dependency mapping, and validation. V1 makes no visualization promise. |
| DAG execution | Absent | Durable child instance per node | Runtime-owned progression; no caller-owned ready/completed sets and no public `RunChildren`. |
| Definition-wide retry | Absent | Absent | Deferred. Step retry remains structured and bounded. |
| Management and typed state | In-memory implementation | Durable implementation | Typed snapshot/state/output queries, notification-driven output wait, cancellation request, and termination only. Instance enumeration/bulk query plus pause/resume/retry/archive/purge are deferred public operations. |
| Persistence/restart recovery | Absent | Durable only | Requires a durable store. In-memory durable hosting is development/test-only. |

V1 concurrency is bulk-synchronous fork-join. Root fan-out scopes compose sequentially without
bound: each join returns the root builder and each merge replaces `TState`. Data-dependent fan-out
is authored as successive stages separated by one barrier per stage, with intermediate results
materialized in parent state and therefore subject to state and payload budgets.

Heterogeneous per-group work is authored by emitting one tagged item per `(group, unit)` pair and
dispatching on the tag in the item body. This encoding is sanctioned only where flat item identity,
an authored item bound plus fixed encoded-value budgets, flat failure aggregation, a single
`MaxConcurrency`, and the absence of sequential dependency between groups are all acceptable. It
is not equivalent to nesting.

## 17.2 Approved public authoring signatures

The member names, parameters, staged return families, value/result shapes, and availability in
this section are the implementation and compile-fixture baseline. The normative companion
[`17-public-authoring-contract.cs`](17-public-authoring-contract.cs) expands every workflow
authoring family to valid C# with concrete receiver names and generic arities; it contains no
product implementation. A rename, additional overload, or equivalent provisional path requires
an explicit amendment to both artifacts first. Inline `TRootBuilder`, `TBranchScopeBuilder`, and
generic names beginning with `TSelected` or `TBuilder` are only compact semantic indexes into that
companion, never extra public types or permission to invent another member. In particular,
`TRootBuilder` in the fan-out signatures means a root receiver and never a nested-builder alias. If
an inline family and its concrete declaration ever differ, validation fails and neither may be
implemented until this baseline is reconciled and re-reviewed.

### 17.2.1 Strong values and execution context

```csharp
public sealed class DefinitionId : IEquatable<DefinitionId>
{
    private DefinitionId(Guid value);
    public Guid Value { get; }
    public static DefinitionId New();
    public static DefinitionId Parse(string value);
    public static bool TryParse(string? value, out DefinitionId? definitionId);
}

public sealed class DefinitionVersion : IEquatable<DefinitionVersion>
{
    public DefinitionVersion(int value);
    public int Value { get; }
    public static DefinitionVersion Initial { get; }
}

public sealed class EventContractVersion : IEquatable<EventContractVersion>
{
    public EventContractVersion(int value);
    public int Value { get; }
    public static EventContractVersion Initial { get; }
}

public sealed class EventName : IEquatable<EventName>
{
    private EventName(string value);
    public string Value { get; }
    public static EventName Create(string value);
}

public sealed class WorkflowOutcomeName : IEquatable<WorkflowOutcomeName>
{
    private WorkflowOutcomeName(string value);
    public string Value { get; }
    public static WorkflowOutcomeName Create(string value);
}

public sealed class AuthoredBranchId : IEquatable<AuthoredBranchId>
{
    private AuthoredBranchId(string value);
    public string Value { get; }
    public static AuthoredBranchId Create(string value);
}

public sealed class DagNodeId : IEquatable<DagNodeId>
{
    private DagNodeId(string value);
    public string Value { get; }
    public static DagNodeId Create(string value);
}

public sealed class ResourcePoolName : IEquatable<ResourcePoolName>
{
    private ResourcePoolName(string value);
    public string Value { get; }
    public static ResourcePoolName Create(string value);
}

public sealed class TransientPoolName : IEquatable<TransientPoolName>
{
    private TransientPoolName(string value);
    public string Value { get; }
    public static TransientPoolName Create(string value);
}

public sealed class StartIdempotencyKey : IEquatable<StartIdempotencyKey>
{
    private StartIdempotencyKey(string value);
    public string Value { get; }
    public static StartIdempotencyKey Create(string value);
}

public sealed class InstanceId : IEquatable<InstanceId>
{
    private InstanceId(Guid value);
    public Guid Value { get; }
    public static InstanceId Parse(string value);
    public static bool TryParse(string? value, out InstanceId? instanceId);
}

public sealed class CorrelationId : IEquatable<CorrelationId>
{
    private CorrelationId(string value);
    public string Value { get; }
    public static CorrelationId Create(string value);
}

public sealed class EventId : IEquatable<EventId>
{
    private EventId(string value);
    public string Value { get; }
    public static EventId Create(string value);
}

public sealed class WaitId : IEquatable<WaitId>
{
    private WaitId(Guid value);
    public Guid Value { get; }
    public static WaitId Parse(string value);
    public static bool TryParse(string? value, out WaitId? waitId);
}

public sealed class AuthoredLocation : IEquatable<AuthoredLocation>
{
    internal AuthoredLocation(string value);
    public string Value { get; }
}

public sealed class DefinitionFingerprint : IEquatable<DefinitionFingerprint>
{
    internal DefinitionFingerprint(string value);
    public string Value { get; }
}

public sealed class PayloadFingerprint : IEquatable<PayloadFingerprint>
{
    internal PayloadFingerprint(string value);
    public string Value { get; }
}

public sealed class StepOperationId : IEquatable<StepOperationId>
{
    private StepOperationId(string value);
    public string Value { get; }
    public static StepOperationId Parse(string value);
    public static bool TryParse(string? value, out StepOperationId? operationId);
}

public sealed class LeaseProtectionToken : IEquatable<LeaseProtectionToken>
{
    private LeaseProtectionToken(string value);
    public string Value { get; }
    public static LeaseProtectionToken Parse(string value);
    public static bool TryParse(string? value, out LeaseProtectionToken? token);
}

public sealed class StopConfirmationId : IEquatable<StopConfirmationId>
{
    private StopConfirmationId(string value);
    public string Value { get; }
    public static StopConfirmationId Create(string value);
}

public sealed class ResourcePoolOperationId : IEquatable<ResourcePoolOperationId>
{
    private ResourcePoolOperationId(string value);
    public string Value { get; }
    public static ResourcePoolOperationId Create(string value);
}

public sealed class ResourceGovernancePartitionId : IEquatable<ResourceGovernancePartitionId>
{
    private ResourceGovernancePartitionId(string value);
    public string Value { get; }
    public static ResourceGovernancePartitionId Create(string value);
}

public sealed class ReadOnlyStateSnapshot<TState>
{
    internal ReadOnlyStateSnapshot(TState value);
    public TState Value { get; }
}

public sealed record ForEachItemContext(int Index);

public sealed class EventEnvelope
{
    internal EventEnvelope(
        EventId eventId,
        WorkflowEventContract eventContract,
        CorrelationId correlationId,
        DateTimeOffset occurredAt,
        ReadOnlyMemory<byte> payload);
    public EventId EventId { get; }
    public WorkflowEventContract EventContract { get; }
    public CorrelationId CorrelationId { get; }
    public DateTimeOffset OccurredAt { get; }
    public TPayload GetPayload<TPayload>(WorkflowEventContract<TPayload> eventContract);
}

public sealed class StepExecutionContext
{
    internal StepExecutionContext(
        InstanceId workflowInstanceId,
        StepOperationId operationId,
        int attemptNumber);
    public InstanceId WorkflowInstanceId { get; }
    public StepOperationId OperationId { get; }
    public int AttemptNumber { get; }
}

public sealed class StepContext<TState>
{
    internal StepContext();
    public TState State { get; }
    public void ReplaceState(TState replacement);
    public StepExecutionContext Execution { get; }
    public EventEnvelope? ResumedEvent { get; }
    public TimeProvider TimeProvider { get; }
    public ForEachItemContext? ForEachItem { get; }
    public ResourceLeaseExecutionContext? ResourceLease { get; }
}

public enum WorkflowDiagnosticSeverity
{
    Warning,
    Error
}

public sealed class WorkflowDiagnostic
{
    internal WorkflowDiagnostic(
        string code,
        WorkflowDiagnosticSeverity severity,
        AuthoredLocation location,
        IReadOnlyList<AuthoredLocation> relatedLocations,
        string message);
    public string Code { get; }
    public WorkflowDiagnosticSeverity Severity { get; }
    public AuthoredLocation Location { get; }
    public IReadOnlyList<AuthoredLocation> RelatedLocations { get; }
    public string Message { get; }
}

public sealed class Validation<T>
{
    internal Validation(
        T? value,
        IReadOnlyList<WorkflowDiagnostic> diagnostics);
    public bool IsValid { get; }
    public IReadOnlyList<WorkflowDiagnostic> Diagnostics { get; }
    public bool TryGetValue(out T? value);
}

public abstract class OrcaCoreException : Exception
{
    protected OrcaCoreException(
        string code,
        string message,
        Exception? innerException = null);
    public string Code { get; }
}

public sealed class WorkflowDefinitionException : OrcaCoreException
{
    internal WorkflowDefinitionException(
        IReadOnlyList<WorkflowDiagnostic> diagnostics);
    public IReadOnlyList<WorkflowDiagnostic> Diagnostics { get; }
}

public interface IStep<TState>
{
    ValueTask<StepResult> ExecuteAsync(
        StepContext<TState> context,
        CancellationToken cancellationToken);
}

```

Caller-created string-backed scalar names and IDs have private constructors and one public
`Create(string)` factory. `Create` rejects null, empty, whitespace, and leading/trailing
whitespace; it never trims or normalizes. Those types expose no public constructor, `New`,
`Parse`/`TryParse`, implicit conversion, raw-string overload, or construction alias.
`DefinitionVersion` rejects non-positive values. `DefinitionId.New()` never returns
`Guid.Empty`; `DefinitionId.Parse` rejects the canonical empty GUID with `ArgumentException`,
and `TryParse` returns `false` with a null result. `InstanceId`, `WaitId`, and `DagRunId` use the
same nonempty GUID parser rule. V1 does not invent different
per-name length constants and never silently truncates a value; a storage/transport adapter
rejects input that it cannot represent before committing it.
Equality and persistence are exact ordinal and case-sensitive where the underlying value is a
string. These contracts are immutable non-positional reference values with no `init`, `with`,
implicit primitive conversion, or parallel primitive overload. Consequently, `default` cannot
produce a non-null value that bypasses construction; every operation boundary still rejects
null. `InstanceId`, `WaitId`, `StepOperationId`, `LeaseProtectionToken`, and `DagRunId` are
runtime-created; they have canonical formatting and round-trip parsers/converters but are never
accepted as author-selected execution identity. `AuthoredLocation` and
`DefinitionFingerprint` and `PayloadFingerprint` are runtime/compiler-created opaque projections:
applications can compare,
log, and persist their `Value`, but cannot construct one or contribute arbitrary bytes.
`EventName`, `WorkflowOutcomeName`, `AuthoredBranchId`, `DagNodeId`, `ResourcePoolName`,
`TransientPoolName`, `StartIdempotencyKey`, `CorrelationId`, `EventId`, `StopConfirmationId`,
`ResourcePoolOperationId`, and `ResourceGovernancePartitionId` are caller-created through
their sole `Create(string)` factory.

Snapshots are detached by round-tripping through v1's fixed certified `System.Text.Json` codec
before an author selector sees them. The codec is not replaceable in v1 and has persisted format
ID `orcacore-json-v1`. Registration rejects unsupported/cyclic/polymorphic shapes that lack an
approved static contract. Certification proves that the same supported value graph, type,
member order, and collection enumeration order produce the same bytes, plus round-trip type
fidelity, detached copies, null behavior, and failure-before-commit. The closed sequence
representation is a JSON array declared as one-dimensional `T[]`, `List<T>`, `IList<T>`, or
`IReadOnlyList<T>` and materialized only as an array or exact `List<T>`. The closed map
representation is a JSON object declared as `Dictionary<string,T>`, `IDictionary<string,T>`, or
`IReadOnlyDictionary<string,T>` and materialized only as exact `Dictionary<string,T>`.
Enumeration/insertion order is semantic; authors normalize it when order is not business data.
All other declared/runtime collection shapes reject before commit. `ReadOnlyStateSnapshot<TState>` is a
runtime-created non-positional sealed wrapper with no public constructor or deconstructor. The
wrapper prevents replacing the runtime-owned reference; authors must still treat its `Value` as
immutable.

Each business-step attempt receives a codec-detached attempt-local copy of the last committed
root/branch/item state. Mutable state may be changed through `State`; immutable/value state is
replaced through `ReplaceState`. A successful winning attempt atomically commits its final copy.
A failed or timed-out attempt, including a token-ignoring late body, can mutate only its discarded
copy. A retry starts from the same last committed state. An ordinary timeout may therefore allow
a retry to physically overlap a token-ignoring late attempt, but only one attempt retains logical
commit authority. The late body's per-step throttle and transient-pool capacity remain occupied
until the physical body actually returns. A leased attempt is stricter: no overlapping in-process
retry starts while its prior body remains active; host-loss recovery may retry under the same
persisted lease obligation. Compile/build guards cover both mutable reference state and
immutable/value-state replacement.

One `StepOperationId` identifies one logical visit to one step. It remains unchanged across
attempt retry, step timeout reconciliation, replay, process replacement, expected-version
conflict, and competing drivers. A loop re-entry, another `ForEach` item, another branch,
or a new continue-as-new generation receives a new ID. `AttemptNumber` is the positive
retry-policy attempt coordinate, starts at one, and counts against `maxAttempts`; it is not a
physical CLR-dispatch count. Durable execution commits the operation ID, attempt coordinate,
optional absolute attempt deadline, and in-flight dispatch marker before dispatch. An uncertain
host-loss replay reuses the full coordinate and may redispatch physically without consuming another attempt, whether the host failed before
the dispatch was observed to start or after an external effect/lost response but before outcome
commit. Only a committed retry transition increments the coordinate. If recovery observes that
the persisted attempt deadline
already expired, it records that same attempt as timed out without dispatch and then either commits
the next retry coordinate or exhausts the existing budget. With `maxAttempts == 1`, uncertain
redispatch therefore remains attempt one and never invents attempt two. `AttemptNumber` is
diagnostic and never an external idempotency key. Named external-effect adapters are certified to
use the current `StepOperationId`, store a request
fingerprint, and create-or-observe at most one logical effect. Side-effect-free structural
delegates, effect-driving closures, and truthful stop confirmation remain explicit author or
trusted-integration obligations; v1 does not claim the CLR can prove arbitrary code pure.

### 17.2.2 Staged workflow construction and typed completion

```csharp
EphemeralWorkflowInitBuilder<TState> Workflow.Ephemeral<TState>(
    DefinitionId definitionId,
    DefinitionVersion definitionVersion);

DurableWorkflowInitBuilder<TState> Workflow.Durable<TState>(
    DefinitionId definitionId,
    DefinitionVersion definitionVersion);

EphemeralWorkflowBuilder<TInput, TState>
    EphemeralWorkflowInitBuilder<TState>.Init<TInput>(Func<TInput, TState> createState);

DurableWorkflowBuilder<TInput, TState>
    DurableWorkflowInitBuilder<TState>.Init<TInput>(Func<TInput, TState> createState);

EphemeralWorkflowCompletionBuilder<TInput>
    EphemeralWorkflowBuilder<TInput, TState>.End();

EphemeralWorkflowCompletionBuilder<TInput>
    EphemeralWorkflowBuilder<TInput, TState>.End(WorkflowOutcomeName outcome);

EphemeralWorkflowCompletionBuilder<TInput, TOutput>
    EphemeralWorkflowBuilder<TInput, TState>.End<TOutput>(
        Func<ReadOnlyStateSnapshot<TState>, TOutput> output);

EphemeralWorkflowCompletionBuilder<TInput, TOutput>
    EphemeralWorkflowBuilder<TInput, TState>.End<TOutput>(
        Func<ReadOnlyStateSnapshot<TState>, TOutput> output,
        WorkflowOutcomeName outcome);

DurableWorkflowCompletionBuilder<TInput>
    DurableWorkflowBuilder<TInput, TState>.End();

DurableWorkflowCompletionBuilder<TInput>
    DurableWorkflowBuilder<TInput, TState>.End(WorkflowOutcomeName outcome);

DurableWorkflowCompletionBuilder<TInput, TOutput>
    DurableWorkflowBuilder<TInput, TState>.End<TOutput>(
        Func<ReadOnlyStateSnapshot<TState>, TOutput> output);

DurableWorkflowCompletionBuilder<TInput, TOutput>
    DurableWorkflowBuilder<TInput, TState>.End<TOutput>(
        Func<ReadOnlyStateSnapshot<TState>, TOutput> output,
        WorkflowOutcomeName outcome);

EphemeralWorkflowDefinition<TInput>
    EphemeralWorkflowCompletionBuilder<TInput>.Build();
Validation<EphemeralWorkflowDefinition<TInput>>
    EphemeralWorkflowCompletionBuilder<TInput>.TryBuild();

EphemeralWorkflowDefinition<TInput, TOutput>
    EphemeralWorkflowCompletionBuilder<TInput, TOutput>.Build();
Validation<EphemeralWorkflowDefinition<TInput, TOutput>>
    EphemeralWorkflowCompletionBuilder<TInput, TOutput>.TryBuild();

DurableWorkflowDefinition<TInput>
    DurableWorkflowCompletionBuilder<TInput>.Build();
Validation<DurableWorkflowDefinition<TInput>>
    DurableWorkflowCompletionBuilder<TInput>.TryBuild();

DurableWorkflowDefinition<TInput, TOutput>
    DurableWorkflowCompletionBuilder<TInput, TOutput>.Build();
Validation<DurableWorkflowDefinition<TInput, TOutput>>
    DurableWorkflowCompletionBuilder<TInput, TOutput>.TryBuild();

EphemeralWorkflowRef<TInput> EphemeralWorkflowDefinition<TInput>.Reference { get; }
EphemeralWorkflowRef<TInput, TOutput>
    EphemeralWorkflowDefinition<TInput, TOutput>.Reference { get; }
DurableWorkflowRef<TInput> DurableWorkflowDefinition<TInput>.Reference { get; }
DurableWorkflowRef<TInput, TOutput>
    DurableWorkflowDefinition<TInput, TOutput>.Reference { get; }
```

The init builder exposes only `Init`. The completion builder exposes only `Build` and
`TryBuild`. `Build` throws one `WorkflowDefinitionException` containing the same accumulated
diagnostics returned by `TryBuild`. A workflow output and its optional fixed outcome metadata
commit atomically with `End`. There is no dynamic outcome-name selector in v1; dynamic
business classification belongs in `TOutput`. Only the parameterless/selector-only overloads mean
unnamed completion; an explicit null output selector or outcome throws `ArgumentNullException` at
the fluent call.

Every `(DefinitionId, DefinitionVersion)` is immutable and fingerprint-bound. Registration or
rehydration with the same identity/version and a different compiled fingerprint fails with a
typed conflict. The fingerprint covers only inspectable authored structure: node/member kinds,
ordering, strong values, referenced step/workflow types, static request values, and codec format.
It does not pretend to hash delegate IL, DI configuration, or external adapter behavior.
Changing a selector/projector/merge/output delegate body, step construction/configuration, DAG
mapping logic, or external-request construction therefore requires a new
`DefinitionVersion`; the version bump is the sole v1 contract for opaque code changes.

`Validation<T>` is valid only when it has a value and no error diagnostic; warnings may accompany
a value. Invalid results have no value. `Build` throws `WorkflowDefinitionException` containing the
same immutable diagnostic sequence returned by `TryBuild`; it does not wrap just the first error.
Diagnostics are ordered by `AuthoredLocation.Value`, then ordinal `Code`; related locations are
ordered the same way. Codes/severity/locations are contract data, while message wording may improve.

### 17.2.3 Steps, policies, waits, and structural control

```csharp
TBuilder Then<TStep>() where TStep : IStep<TState>;

TSelectedEphemeralBuilder Then(
    Func<StepContext<TState>, ValueTask> body);

TSelectedEphemeralBuilder Then(
    Func<StepContext<TState>, CancellationToken, ValueTask> body);

TBuilder WithRetry(int maxAttempts, TimeSpan? fixedDelay = null);
TBuilder WithStepTimeout(TimeSpan timeout);

TRootBuilder CompleteWithin(TimeSpan timeout);

TBuilder If(
    Func<ReadOnlyStateSnapshot<TState>, bool> condition,
    Action<TSelectedNestedBuilder> then,
    Action<TSelectedNestedBuilder>? otherwise = null);

TRootBuilder While(
    Func<ReadOnlyStateSnapshot<TState>, bool> condition,
    Action<TSelectedNestedBuilder> body);

TBuilder Wait(
    WorkflowEventContract eventContract,
    Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation);

TBuilder Wait(
    WorkflowEventContract eventContract,
    Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
    TimeSpan timeout);

TBuilder Wait<TPayload>(
    WorkflowEventContract<TPayload> eventContract,
    Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation);

TBuilder Wait<TPayload>(
    WorkflowEventContract<TPayload> eventContract,
    Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
    TimeSpan timeout);

TSelectedDurableBuilder Publish(
    WorkflowEventContract eventContract,
    Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation);

TSelectedDurableBuilder Publish<TPayload>(
    WorkflowEventContract<TPayload> eventContract,
    Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
    Func<ReadOnlyStateSnapshot<TState>, TPayload> payload);

TBuilder Delay(TimeSpan duration);

DurableWorkflowCompletionBuilder<TInput> ContinueAsNew(
    Func<ReadOnlyStateSnapshot<TState>, TState> replacementState);

TSelectedEphemeralBuilder WithTransientPool(TransientPoolName pool);

public abstract record StepResult
{
    private protected StepResult();
    public sealed record Completed : StepResult;
    public sealed record Failed(OrcaCoreException Error) : StepResult;
    public sealed record WaitForEvent(
        WorkflowEventContract EventContract,
        CorrelationId CorrelationId) : StepResult;
    public sealed record WaitForEvent<TPayload>(
        WorkflowEventContract<TPayload> EventContract,
        CorrelationId CorrelationId) : StepResult;
}

public sealed class WorkflowWaitTimeoutException : OrcaCoreException
{
    internal WorkflowWaitTimeoutException(
        WorkflowEventContract eventContract,
        CorrelationId correlationId);
    public WorkflowEventContract EventContract { get; }
    public CorrelationId CorrelationId { get; }
}

public sealed class StepAttemptTimeoutException : OrcaCoreException
{
    internal StepAttemptTimeoutException(
        StepOperationId operationId,
        int attemptNumber,
        TimeSpan timeout);
    public StepOperationId OperationId { get; }
    public int AttemptNumber { get; }
    public TimeSpan Timeout { get; }
}

public sealed class WorkflowDeadlineExceededException : OrcaCoreException
{
    internal WorkflowDeadlineExceededException(DateTimeOffset deadline);
    public DateTimeOffset Deadline { get; }
}

public sealed class AmbiguousWaitRegistrationException : OrcaCoreException
{
    internal AmbiguousWaitRegistrationException(
        DefinitionId definitionId,
        WorkflowEventContract eventContract,
        CorrelationId correlationId);
    public DefinitionId DefinitionId { get; }
    public WorkflowEventContract EventContract { get; }
    public CorrelationId CorrelationId { get; }
}

public sealed class WorkflowStateTypeMismatchException : OrcaCoreException
{
    internal WorkflowStateTypeMismatchException(
        Type requestedType,
        Type registeredType);
    public Type RequestedType { get; }
    public Type RegisteredType { get; }
}
```

`TBuilder` denotes the selected-mode builder on which a non-fan-out member appears;
`TRootBuilder` is the selected root builder and is the only receiver for `Parallel` or `ForEach`;
and `TSelectedEphemeralBuilder` is the applicable ephemeral root or nested builder.
`TSelectedNestedBuilder` retains mode and any enclosing lease restriction. The concrete families
are `EphemeralWorkflowBuilder<TInput,TState>`,
`DurableWorkflowBuilder<TInput,TState>`, `DurableNestedBuilder<TInput,TState>`, the selected
ephemeral/durable branch and item builders, and the dedicated `DurableLeaseWorkflowBuilder`,
`DurableLeaseNestedBuilder`, `DurableLeaseBranchBuilder`, and `DurableLeaseItemBuilder`
families. The exact
availability table is:

| Member | Root | Nested conditional/loop | Root-`Parallel` branch/root-`ForEach` item | Leased body |
|---|---:|---:|---:|---:|
| `Then<TStep>`, retry, step timeout, `Wait`, `Delay`, nested `If` | yes | yes | yes | yes |
| Ephemeral `ValueTask` lambda `Then` | ephemeral | ephemeral | ephemeral | n/a |
| Root `While` | yes | no | no | no |
| Root `Parallel` | yes | no | no | no |
| Root `ForEach` | yes | no | no | no |
| `Return` | no | no | branch/item only | leased branch/item only |
| `WithTransientPool` | ephemeral | ephemeral | ephemeral | n/a |
| `AcquireResources` | durable | durable when no lease is active | durable branch/item when no lease is active | no |
| `Publish` | durable | durable | durable | durable |
| `Init` | init builder only | no | no | no |
| `CompleteWithin`, `End` | root sequence only | no | no | no |
| terminal `ContinueAsNew` | durable root sequence only | no | no | no |

Ordinary nested conditional/loop builders and root-`Parallel` branch/root-`ForEach` item builders
retain sequencing and nested `If`, but expose no `Parallel`, `ForEach`, or `While`. Dedicated
leased builders additionally expose no `AcquireResources` or `ContinueAsNew`; therefore every
fan-out entry point is root-only and no fan-out can begin while a lexical lease is live.

Ephemeral lambda bodies always return `ValueTask`; synchronous work returns
`ValueTask.CompletedTask`. A lambda mutates/replaces state and cannot return `StepResult`.
Exact-type `StepExecutionThrottle` configuration targets named `Then<TStep>()` steps only and
does not infer a throttle identity for a lambda.

Retry/timeout/transient-pool decorators attach to the immediately preceding business step, are
order-independent, and may each appear at most once for that step. A duplicate or attachment at
any other location is rejected eagerly at the fluent call with `WorkflowDefinitionException`
containing the one `SFE-AUTH-DECORATOR-001` diagnostic; this is not a C# compile-time error.
`maxAttempts` includes the initial policy attempt.
`WithStepTimeout` establishes one absolute deadline before dispatch. Durable mode commits it with
the attempt coordinate; ephemeral mode retains it only for the in-memory run. If that deadline is
reached before a winning result, the attempt's copy is fenced and discarded. A committed retry
transition gets a new `AttemptNumber` and deadline while retaining `StepOperationId`; uncertain
host-loss redispatch retains the existing number/deadline and does not consume retry budget. After
the final allowed attempt, the step fails with `StepAttemptTimeoutException`. Durable attempt
coordinates, deadlines, and late-result fencing survive host replacement.

V1 retry has no predicate plug-in: an author `StepResult.Failed`, an unhandled step exception
normalized to failure, or `StepAttemptTimeoutException` consumes an attempt and retries when the
configured attempt budget remains. Cancellation, workflow deadline, termination, definition
conflict, and runtime invariant violations never retry. `fixedDelay` is the same before every
retry. A failed attempt discards its state copy; `Completed` commits it, and dynamic
`WaitForEvent` atomically commits it together with the validated wait registration before parking.

`CompleteWithin` begins at instance start and includes admission waits, retries, delays, event
waits, lease queueing, and every continue-as-new generation. Continue-as-new inherits the original
absolute deadline; it never resets the budget. When the deadline wins, the instance commits
terminal `TimedOut` with `WorkflowDeadlineExceededException`, prevents further admission, cancels
outstanding wait/timer obligations, signals active attempt tokens, suppresses branch/item merges,
and performs definite cleanup or resource quarantine. It does not wait for token-ignoring bodies
or claim that external protected work stopped.

`ContinueAsNew` is the unconditional terminal of a perpetual durable generation. It returns the
resultless durable completion builder, from which only `Build`/`TryBuild` remain available; no
`End`, step, decorator, or structural node can follow. Conditional/finite rollover is deferred.
At runtime the root must be the sole nonterminal fiber with no descendant scope or owned
obligation; a valid rollover installs replacement state atomically, increments generation, and
inherits the original absolute workflow deadline.

`maxAttempts`, workflow/step/wait timeouts, and `Delay` must be positive and finite; retry
`fixedDelay` may be zero but not negative. A zero `Delay` is rejected rather than becoming a
back-door author `Yield`. `CompleteWithin` may be authored at most once on the root builder. A
second call eagerly throws one `WorkflowDefinitionException` containing
`SFE-AUTH-DEADLINE-001`; the first deadline remains selected and is the related authored location.
The runtime serializes one winner among an event match, a wait timeout, and the workflow deadline.
If a structural wait's event loses to its wait timeout, the current root/branch/item fails with
`WorkflowWaitTimeoutException`; the event obligation is cancelled before progression. If the
workflow deadline wins, neither the event nor wait timeout may progress the now-terminal instance.
In a `WhenAllOutcomes` scope that failure is data for the parent merge; v1 does not add a separate
timeout callback builder.

The orchestration runtime owns timers, attempt fencing, persisted deadlines, and deterministic
time. Polly or another resilience library may be used inside application/provider code, but it
does not define workflow retry, timeout, replay, or terminal semantics.

Every step receives a runtime-owned `CancellationToken`. Workflow/branch/item cancellation and
deadline transitions signal it and fence any late result; authors do not opt individual steps in
or out through a fluent cancellation decorator.

### 17.2.4 Root-only `Parallel` and bounded root `ForEach`

```csharp
public sealed record BranchResult<TResult>(
    AuthoredBranchId BranchId,
    TResult Result);

public sealed class WorkflowFailure
{
    internal WorkflowFailure(
        string code,
        string message,
        IReadOnlyList<WorkflowFailure> causes);
    public string Code { get; }
    public string Message { get; }
    public IReadOnlyList<WorkflowFailure> Causes { get; }
}

public abstract record BranchOutcome<TResult>
{
    private protected BranchOutcome(AuthoredBranchId branchId);
    public AuthoredBranchId BranchId { get; }

    public sealed record Succeeded : BranchOutcome<TResult>
    {
        internal Succeeded(AuthoredBranchId branchId, TResult result);
        public TResult Result { get; }
    }

    public sealed record Failed : BranchOutcome<TResult>
    {
        internal Failed(AuthoredBranchId branchId, WorkflowFailure failure);
        public WorkflowFailure Failure { get; }
    }
}

public sealed record ForEachItemInput<TItem>(int Index, TItem Item);

public sealed record ForEachItemResult<TResult>(int Index, TResult Result);

public abstract record ForEachItemOutcome<TResult>
{
    private protected ForEachItemOutcome(int index);
    public int Index { get; }

    public sealed record Succeeded : ForEachItemOutcome<TResult>
    {
        internal Succeeded(int index, TResult result);
        public TResult Result { get; }
    }

    public sealed record Failed : ForEachItemOutcome<TResult>
    {
        internal Failed(int index, WorkflowFailure failure);
        public WorkflowFailure Failure { get; }
    }
}

ParallelJoinBuilder<TRootBuilder, TRootState, TResult>
    TRootBuilder.Parallel<TResult>(
        Action<TBranchScopeBuilder> branches);

TRootBuilder ParallelJoinBuilder<TRootBuilder, TRootState, TResult>.WhenAll(
    Func<ReadOnlyStateSnapshot<TRootState>,
         IReadOnlyList<BranchResult<TResult>>,
         TRootState> merge);

TRootBuilder ParallelJoinBuilder<TRootBuilder, TRootState, TResult>.WhenAllOutcomes(
    Func<ReadOnlyStateSnapshot<TRootState>,
         IReadOnlyList<BranchOutcome<TResult>>,
         TRootState> merge);

TBranchScopeBuilder Branch<TBranchState>(
    AuthoredBranchId branchId,
    Func<ReadOnlyStateSnapshot<TRootState>, TBranchState> input,
    Action<TSelectedBranchBuilder<TBranchState, TResult>> body);

TSelectedBranchBuilder<TBranchState, TResult> Return(
    Func<ReadOnlyStateSnapshot<TBranchState>, TResult> result);

TSelectedItemBuilder<TItemState, TResult> Return(
    Func<ReadOnlyStateSnapshot<TItemState>, TResult> result);

public sealed class ForEachOptions
{
    private ForEachOptions(int maxItems, int? maxConcurrency);
    public int MaxItems { get; }
    public int? MaxConcurrency { get; }
    public static ForEachOptions Create(int maxItems, int? maxConcurrency = null);
}

ForEachJoinBuilder<TRootBuilder, TRootState, TResult>
    TRootBuilder.ForEach<TItem, TItemState, TResult>(
        Func<ReadOnlyStateSnapshot<TRootState>, IReadOnlyList<TItem>> items,
        ForEachOptions options,
        Func<ForEachItemInput<TItem>, TItemState> input,
        Action<TSelectedItemBuilder<TItemState, TResult>> body);

TRootBuilder ForEachJoinBuilder<TRootBuilder, TRootState, TResult>.WhenAll(
    Func<ReadOnlyStateSnapshot<TRootState>,
         IReadOnlyList<ForEachItemResult<TResult>>,
         TRootState> merge);

TRootBuilder ForEachJoinBuilder<TRootBuilder, TRootState, TResult>.WhenAllOutcomes(
    Func<ReadOnlyStateSnapshot<TRootState>,
         IReadOnlyList<ForEachItemOutcome<TResult>>,
         TRootState> merge);
```

The compact `TRootBuilder` notation above is deliberately root-only. The companion contract
contains exactly one ephemeral and one durable `Parallel` scope/join family plus one ephemeral
and one durable `ForEach` join family; it does not imply non-root fan-out types. Root fan-out
branch/item bodies may sequence steps and use nested `If`; durable branch/item bodies may also
open a scoped resource lease when no ancestor lease is live.

The `Parallel` branch action must author at least one branch. A zero-branch scope never invokes a
merge with an empty list; `TryBuild` returns `SFE-AUTH-BRANCH-004` and `Build` throws a
`WorkflowDefinitionException` containing that diagnostic at the `Parallel` location. Empty
`ForEach` remains the separately approved valid empty-merge case.

Branch and item state is copied from a read-only parent snapshot and remains private. The
single merge returns the complete replacement parent state. In durable mode its pure delegate may
be re-invoked after a crash before commit, but exactly one normalized replacement state is
committed and replay never invokes it after that commit. Results/outcomes are ordered by authored
branch ordinal or item index, never by completion order.

`WorkflowFailure` is the detached, persistable, runtime-created failure projection exposed to a
merge; it never contains a live exception object or runtime stack. Its constructor and every
branch/item outcome constructor are internal, and the abstract outcome bases cannot be derived
outside OrcaCore. OrcaCore deterministically maps an `OrcaCoreException`, including one supplied
through `StepResult.Failed`, to its stable non-empty `Code`; a non-OrcaCore unhandled exception
normalizes to `WF-STEP-UNHANDLED`. Raw CLR type names and exception text are diagnostic message
material, not protocol identity. The mapping occurs before committing the terminal branch/item
outcome.

`WhenAll` waits for every branch/item to become terminal. If any terminal outcome is not
success, it does not invoke the merge and the scope fails after all have finished. One failure
produces that `WorkflowFailure`; multiple failures produce code `SFE-JOIN-FAILED` whose `Causes`
contain the original failures in authored branch/item-index order, never completion order.
`WhenAllOutcomes` also waits for all, commits one merge result from typed success and failure
outcomes, and completes the scope successfully. Its pure delegate may be retried
before that commit. Authors store the
summary in parent state and use a following `If` to accept it or fail through a named step that
returns `StepResult.Failed`; an ephemeral lambda may instead throw an `OrcaCoreException` from its
`ValueTask`. A structural `If` does not itself expose an `End` or failure terminal and cannot catch
an already terminal failed branch. Neither join automatically cancels a sibling.

Instance cancellation, operator termination, or `CompleteWithin` winning its commit race is an
ancestor terminal transition: it signals/fences every active branch/item and suppresses both
author merges. A merge cannot turn that terminal transition into success, so v1 branch/item
outcome types deliberately have no `Cancelled` variant. A scope-local wait or step timeout is a
`Failed` outcome and remains available to `WhenAllOutcomes`. This makes the workflow deadline a
real upper bound even when a branch is parked indefinitely.

`ForEachOptions.Create` requires positive `MaxItems` and, when present, positive
`MaxConcurrency`. Both modes synchronously validate `IReadOnlyList.Count`, copy, codec-round-trip,
and detach one finite logical item snapshot before admitting any item, so later mutation of a caller-owned list
cannot affect execution. Durable mode may re-invoke the pure selector before a winning commit,
but commits exactly one snapshot and replay reuses it.
Stable item identity includes the scope occurrence and item index. The effective item
concurrency is the lower of the optional node cap and host
`MaxConcurrentExecutionPathsPerInstance`. Exceeding the authored item bound or failing codec
round-trip validation fails deterministically before partial admission.
An oversized `Count` fails before per-item copying or admission.
An empty snapshot is valid: no item is admitted and the selected merge is invoked once with an
empty ordered list. `MaxItems` is an upper bound, not a non-empty assertion.
The item-state projector receives only `ForEachItemInput<TItem>`: the detached item plus its
zero-based index. Shared parent/run data needed by item work is placed explicitly in `TItem` by
the finite-list selector; the projector has no hidden parent-state parameter.

### 17.2.5 Scoped durable resource leasing

```csharp
public sealed class ResourceLeaseRequirement
{
    private ResourceLeaseRequirement(ResourcePoolName pool, int units);
    public ResourcePoolName Pool { get; }
    public int Units { get; }

    public static ResourceLeaseRequirement Require(
        ResourcePoolName pool,
        int units = 1);
}

public sealed class ResourceLeaseRequest
{
    private ResourceLeaseRequest(IReadOnlyList<ResourceLeaseRequirement> requirements);
    public IReadOnlyList<ResourceLeaseRequirement> Requirements { get; }

    public static ResourceLeaseRequest Create(
        ResourceLeaseRequirement first,
        params ResourceLeaseRequirement[] additional);
}

DurableWorkflowBuilder<TInput, TState> AcquireResources(
    ResourceLeaseRequest request,
    Action<DurableLeaseWorkflowBuilder<TInput, TState>> body);

DurableWorkflowBuilder<TInput, TState> AcquireResources(
    Func<ReadOnlyStateSnapshot<TState>, ResourceLeaseRequest> request,
    Action<DurableLeaseWorkflowBuilder<TInput, TState>> body);

DurableNestedBuilder<TInput, TState> AcquireResources(
    ResourceLeaseRequest request,
    Action<DurableLeaseNestedBuilder<TInput, TState>> body);

DurableNestedBuilder<TInput, TState> AcquireResources(
    Func<ReadOnlyStateSnapshot<TState>, ResourceLeaseRequest> request,
    Action<DurableLeaseNestedBuilder<TInput, TState>> body);

DurableBranchBuilder<TBranchState, TResult> AcquireResources(
    ResourceLeaseRequest request,
    Action<DurableLeaseBranchBuilder<TBranchState, TResult>> body);

DurableBranchBuilder<TBranchState, TResult> AcquireResources(
    Func<ReadOnlyStateSnapshot<TBranchState>, ResourceLeaseRequest> request,
    Action<DurableLeaseBranchBuilder<TBranchState, TResult>> body);

DurableItemBuilder<TItemState, TResult> AcquireResources(
    ResourceLeaseRequest request,
    Action<DurableLeaseItemBuilder<TItemState, TResult>> body);

DurableItemBuilder<TItemState, TResult> AcquireResources(
    Func<ReadOnlyStateSnapshot<TItemState>, ResourceLeaseRequest> request,
    Action<DurableLeaseItemBuilder<TItemState, TResult>> body);

public sealed class ResourceLeaseExecutionContext
{
    internal ResourceLeaseExecutionContext(LeaseProtectionToken protectionToken);
    public LeaseProtectionToken ProtectionToken { get; }
}

public sealed class LeaseLostException : OrcaCoreException
{
    internal LeaseLostException(
        LeaseProtectionToken protectionToken,
        IReadOnlyList<ResourcePoolName> missingPools);
    public LeaseProtectionToken ProtectionToken { get; }
    public IReadOnlyList<ResourcePoolName> MissingPools { get; }
}

public sealed class ResourcePoolNotConfiguredException : OrcaCoreException
{
    internal ResourcePoolNotConfiguredException(
        IReadOnlyList<ResourcePoolName> missingPools);
    public IReadOnlyList<ResourcePoolName> MissingPools { get; }
}

public enum ProtectedWorkStopConfirmationStatus
{
    Released,
    AlreadyConfirmed,
    NotConfirmable,
    TokenNotFound,
    ConfirmationConflict
}

public interface IDurableResourceLeaseRecovery
{
    ValueTask<ProtectedWorkStopConfirmationStatus> ConfirmProtectedWorkStoppedAsync(
        LeaseProtectionToken protectionToken,
        StopConfirmationId confirmationId,
        CancellationToken cancellationToken = default);
}

public enum DurableResourceLeaseObligationStatus
{
    Queued,
    PendingCommit,
    Held,
    ReviewMarked,
    AmbiguousHeld,
    Quarantined,
    Released,
    LeaseLost
}

public sealed record DurableResourceLeaseTicketSnapshot(
    string TicketId,
    ResourcePoolName Pool,
    int Units,
    long ProviderGeneration,
    DateTimeOffset ReviewDeadline,
    bool ReviewMarked);

public sealed class DurableResourceLeaseObligationSnapshot
{
    internal DurableResourceLeaseObligationSnapshot();
    public string ObligationId { get; }
    public InstanceId InstanceId { get; }
    public DefinitionId DefinitionId { get; }
    public DefinitionVersion DefinitionVersion { get; }
    public int Generation { get; }
    public string FiberOccurrence { get; }
    public string ScopeOccurrence { get; }
    public AuthoredLocation AuthoredLocation { get; }
    public LeaseProtectionToken ProtectionToken { get; }
    public DurableResourceLeaseObligationStatus Status { get; }
    public IReadOnlyList<DurableResourceLeaseTicketSnapshot> Tickets { get; }
    public DateTimeOffset? QuarantinedAt { get; }
    public StopConfirmationId? AcceptedConfirmationId { get; }
}

public interface IDurableResourceLeaseDiagnostics
{
    IAsyncEnumerable<DurableResourceLeaseObligationSnapshot>
        EnumerateOutstandingAsync(CancellationToken cancellationToken = default);

    ValueTask<DurableResourceLeaseObligationSnapshot?> GetAsync(
        LeaseProtectionToken protectionToken,
        CancellationToken cancellationToken = default);
}
```

There is no empty-call-capable `params` acquisition overload, point acquisition, fiber-lifetime
acquisition, author duration, expiry backstop, holder ID, renewal token, or mutable request.
`Require` rejects a null/default pool and non-positive units. `Create` requires its non-null
first item, copies the additional array, and rejects null items or duplicate pool names.
A selector is deterministic and side-effect-free; durable execution normalizes and commits its
non-empty request before any provider mutation and reuses it for that scope occurrence.
Statically authored requests are checked against the selected durable host during definition
registration. A selector-produced request is checked after normalization; any well-formed but
unconfigured names fail the requesting root/branch/item with non-retryable
`ResourcePoolNotConfiguredException`
before queue or pool mutation. `MissingPools` is a non-empty immutable distinct list in ordinal
name order, so an unknown pool can never park indefinitely.

The root, root-nested, branch, and item overload pairs above are exhaustive. Branch/item
conditional bodies retain their enclosing `DurableBranchBuilder`/`DurableItemBuilder` family;
root conditional/loop bodies use `DurableNestedBuilder`. The four leased builder families expose
the ordinary step/`If`/wait/`Publish`/delay/return capabilities appropriate to their parent but omit every
fan-out member (`Parallel` and `ForEach`), `AcquireResources`, and `ContinueAsNew`.

The complete request is granted atomically. If unavailable, only the requesting fiber parks;
unrelated siblings may continue. The lease remains held while its lexical body is running or
parked in `Wait`, and releases before the parent builder resumes after normal body completion.
A `Wait` authored after the scope observes an already-released lease. Sequential scopes and
per-root-`While`-iteration scopes are legal because the earlier lexical scope has exited.
OrcaCore never silently releases and later reacquires at a wait: that could resume with different
capacity after external work has escaped the bracket. Authors place a long wait after the scope
when the wait is unrelated to proving the protected work stopped, then open a new scope only if
later steps need capacity again. When a terminal watcher report is the proof required to release
protected external work, the `Wait` and its immediate identity/terminality validation remain
inside the same lexical lease. The scope is no wider than that protected obligation.

Cancellation, failure, workflow deadline, and termination race with grant through the persisted
handoff protocol. If cancellation commits while the obligation is still `Queued` and before
governance reservation, the request transitions to `CancelledBeforeGrant`, creates no ticket,
reserves zero units, and cannot later be granted. If the atomic governance reservation commits
first, grant wins that boundary and cancellation cannot erase its exact tickets. Cancellation
before `WorkflowActivationCommitted` performs one exact compensating release because author code
was never admitted; cancellation after activation follows normal causally proven release or the
ambiguity-preserving quarantine path. Delayed or duplicate loser commands are idempotent stale
no-ops. No interleaving may create a partial grant, ghost ticket, or both a cancelled-before-grant
and granted outcome.
Independent siblings may acquire independently. A descendant cannot acquire while an ancestor
scope is queued, pending-commit, held, review-marked, or ambiguous-held, and concurrently needed
resources belong in one request. A root `Parallel` branch or root `ForEach` item may independently
open its own lease when no ancestor lease is live, but the resulting leased body cannot fan out or
reacquire. The compiler and runtime repeat the ancestry/quiescence and location checks for
hand-built or stale plans.

The obligation lifecycle is `Queued -> PendingCommit -> Held -> ReviewMarked -> AmbiguousHeld ->
Quarantined -> Released`. A transition may skip an inapplicable intermediate state, but never
reverses or allocates a successor ticket under the same obligation. A retryable leased-step
timeout, ambiguous submit, or recovered in-flight attempt moves `Held`/`ReviewMarked` to
`AmbiguousHeld`, preserves the same `StepOperationId`, protection token, ticket identities,
pool units, provider generations, fiber/scope occurrence, and reserved capacity, and retries under
that obligation. No overlapping retry starts while the prior in-process leased body still runs;
host-loss recovery may retry because the old local execution is gone. A successful retry does not
erase the recorded ambiguity.

Normal completion without ambiguity, definite pre-effect failure, and causally proven
never-started/cleaned-up work release exactly once before parent resume. If ambiguity remains when
the lexical scope tries to exit, retries exhaust, or cancellation, workflow deadline, forced
termination, or abandonment wins, one atomic commit transfers `AmbiguousHeld` to `Quarantined`
before branch/item failure, merge, parent continuation, or terminal progression. Quarantined units
remain reserved until a trusted idempotent confirmation proves every protected work item stopped,
became terminal, or was end-to-end fenced. A delete acknowledgement, elapsed time, workflow
terminal status, Kubernetes label, or successful retry alone is not proof. A stale/mismatched
confirmation cannot release another occurrence.

If workflow state says an occurrence is pending, held, ambiguous-held, or quarantined but the governance aggregate
has lost any matching ticket, the runtime never silently recreates or reacquires it. It commits
terminal `LeaseLostException`, fences progression and merges, emits a critical provider-integrity
signal, transitions the diagnostic obligation to `DurableResourceLeaseObligationStatus.LeaseLost`,
and requires operator/provider repair; external protected work is still treated as
possibly live. This is provider-corruption evidence, not capacity available for a successor.

For a branch/item used by `WhenAllOutcomes`, a definite failure may release normally and become
a merge-visible `Failed` outcome. If protected work is ambiguous, one commit first transfers the
lease ticket/token from the lexical scope into runtime-owned quarantine and records the branch or
item as failed; only then may the parent merge/progress. The merge cannot release, reuse, or
confirm that obligation, and normal workflow/scope completion never reclaims it. A later
acquisition is a new occurrence with a new token, is no longer an ancestry conflict with the
detached terminal scope, and queues against the capacity still reserved by quarantine. An
ancestor workflow deadline/operator termination still suppresses the merge under the join rule
above while retaining quarantine.

`LeaseProtectionToken` is runtime-created and round-trippable so an integration can label
protected work. It is minted and committed with the queued obligation before capacity admission,
is delivered to author code only after grant through `StepContext<TState>.ResourceLease`, remains stable across
that occurrence's step retries/replay/host replacement, and is never reused by another loop
iteration, item, branch, or continue-as-new generation. `StopConfirmationId` is caller-created
and idempotent. The recovery and diagnostics interfaces are advanced host-management trust
boundaries for a trusted in-process reconciler/operator, not workflow-authoring or provider-storage
SPIs, and contain no Kubernetes, AWS, or job-system type. `EnumerateOutstandingAsync` returns
detached snapshots for every queued/reserved/ambiguous/quarantined/provider-integrity obligation
and excludes ordinary released history. Exact `GetAsync` may return a retained confirmation
tombstone. The public advanced interface itself has no caller-identity or per-call authorization
parameter: the in-process host-management boundary is trusted. V1 exposes no remote diagnostics
endpoint; any host adapter that exposes one SHALL authorize the request and redact protection and
ownership facts before returning any snapshot, with no partial result on denial. Exact ticket,
pool, unit, provider-generation, owner occurrence, review, quarantine, and confirmation facts give
operators a discovery-to-confirmation path without exposing the raw governance event stream.

Stop confirmation is serialized with normal release and uses this exhaustive, ordered result
matrix. Confirmation-ID bindings are evaluated before token lifecycle: an ID bound to another
token always returns `ConfirmationConflict`; an ID already bound to this token always returns
`AlreadyConfirmed`. Only an unbound ID proceeds to the remaining lifecycle rows.

| Token/confirmation state | Result | Capacity effect |
|---|---|---|
| Confirmation ID is bound to another token, regardless of supplied-token lifecycle | `ConfirmationConflict` | none |
| Confirmation ID is bound to this token, regardless of supplied-token lifecycle | `AlreadyConfirmed` | none |
| Unbound ID; known token is still queued, pending-commit, held, review-marked, or ambiguous-held by a live lexical scope | `NotConfirmable` | none |
| Unbound ID; known token is quarantined | `Released` | bind ID and release exactly once |
| Unbound ID; token was already released by an accepted confirmation | `AlreadyConfirmed` | none |
| Unbound ID; token was normally released, never existed, or its retained record was purged | `TokenNotFound` | none |

If normal release wins the serialized race, confirmation observes `TokenNotFound`; if confirmation
wins, later lexical cleanup is an idempotent no-op. Accepted confirmation bindings and released
token tombstones are retained at least as long as the owning workflow record and provider dedup
window; after an explicit coordinated purge, a later call may return `TokenNotFound` but can never
release successor capacity. The interface lives in the advanced host-management namespace
`OrcaCore.Hosting.ResourceLeases`, separate from workflow authoring and provider storage ports.

### 17.2.6 Typed DAG planning

`OrcaCore.Dag` is a separate application-contract project/package. `OrcaCore.Dag.Hosting` is its
runtime adapter and depends on both `OrcaCore.Dag` and the isolated advanced durable-host bridge;
OrcaCore application/core projects never depend on either DAG package.

The post-gate `admit-dag-authoring-friend-boundary` amendment proposes
`OrcaCore -> OrcaCore.Dag` solely to construct the five compiler-created application value
families used by `Build`/`TryBuild`: `Validation<T>`, `WorkflowDiagnostic`, `AuthoredLocation`,
`DefinitionFingerprint`, and `WorkflowDefinitionException`. It retains internal constructors,
the existing diagnostic catalog and ordering, and one shared canonical UTF-8/SHA-256 fingerprint
operation; compiled-metadata verification must limit DAG's actual internal member references to
the exact reviewed signatures. This is a proposed contract, not a current friend grant or
authorization for Task 8.2 source. It adds no `Core -> Dag` dependency or child-start access.

```csharp
public static class Dag
{
    public static WorkflowDagBuilder<TRunInput> Define<TRunInput>(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion);
}

public sealed class WorkflowDagBuilder<TRunInput>
{
    internal WorkflowDagBuilder();

    public DagNodeBuilder<TRunInput, TNodeInput> Node<TNodeInput>(
        DagNodeId nodeId,
        DurableWorkflowRef<TNodeInput> workflow);

    public DagNodeBuilder<TRunInput, TNodeInput, TNodeOutput>
        Node<TNodeInput, TNodeOutput>(
        DagNodeId nodeId,
        DurableWorkflowRef<TNodeInput, TNodeOutput> workflow);

    public WorkflowDagPlan<TRunInput> Build();
    public Validation<WorkflowDagPlan<TRunInput>> TryBuild();
}

public sealed class DagNodeBuilder<TRunInput, TNodeInput>
{
    internal DagNodeBuilder();
    public DagNodeBuilder<TRunInput, TNodeInput> DependsOn(
        params DagNodeRef[] dependencies);

    public DagNodeRef MapInput(
        Func<DagNodeInputContext<TRunInput>, TNodeInput> input);
}

public sealed class DagNodeBuilder<TRunInput, TNodeInput, TNodeOutput>
{
    internal DagNodeBuilder();
    public DagNodeBuilder<TRunInput, TNodeInput, TNodeOutput> DependsOn(
        params DagNodeRef[] dependencies);

    public DagNodeRef<TNodeOutput> MapInput(
        Func<DagNodeInputContext<TRunInput>, TNodeInput> input);
}

public class DagNodeRef
{
    internal DagNodeRef(DagNodeId nodeId);
    public DagNodeId NodeId { get; }
}

public sealed class DagNodeRef<TOutput> : DagNodeRef
{
    internal DagNodeRef(DagNodeId nodeId);
}

public sealed class DagNodeInputContext<TRunInput>
{
    internal DagNodeInputContext();
    public TRunInput RunInput { get; }
    public TDependencyOutput OutputOf<TDependencyOutput>(
        DagNodeRef<TDependencyOutput> dependency);
}

public sealed class WorkflowDagPlan<TRunInput>
{
    internal WorkflowDagPlan();
    public DefinitionId DefinitionId { get; }
    public DefinitionVersion DefinitionVersion { get; }
    public DefinitionFingerprint DefinitionFingerprint { get; }
    public IReadOnlyList<DagNodeRef> Nodes { get; }
}

public sealed class DagRunId : IEquatable<DagRunId>
{
    private DagRunId(Guid value);
    public Guid Value { get; }
    public static DagRunId Parse(string value);
    public static bool TryParse(string? value, out DagRunId? runId);
}

public sealed class DagRunNotFoundException : OrcaCoreException
{
    internal DagRunNotFoundException(DagRunId runId);
    public DagRunId RunId { get; }
}

public sealed class DagRunDefinitionMismatchException : OrcaCoreException
{
    internal DagRunDefinitionMismatchException(
        DagRunId runId,
        DefinitionId expectedDefinitionId,
        DefinitionId actualDefinitionId);
    public DagRunId RunId { get; }
    public DefinitionId ExpectedDefinitionId { get; }
    public DefinitionId ActualDefinitionId { get; }
}

public sealed class DagDefinitionRegistrationConflictException : OrcaCoreException
{
    internal DagDefinitionRegistrationConflictException(
        DefinitionRegistrationConflict conflict);
    public DefinitionRegistrationConflict Conflict { get; }
}

public sealed class DagStartIdempotencyConflictException : OrcaCoreException
{
    internal DagStartIdempotencyConflictException(
        StartIdempotencyConflict conflict);
    public StartIdempotencyConflict Conflict { get; }
}

public enum DagRunStatus
{
    Running,
    CancellationRequested,
    Succeeded,
    Failed,
    Cancelled
}

public enum DagNodeStatus
{
    Pending,
    Ready,
    Running,
    CancellationRequested,
    Succeeded,
    Failed,
    Cancelled,
    DependencyBlocked
}

public sealed record DagNodeSnapshot(
    DagNodeId NodeId,
    int AuthoredOrdinal,
    DagNodeStatus Status,
    IReadOnlyList<DagNodeId> Dependencies,
    InstanceId? ChildInstanceId,
    PayloadFingerprint? MappedInputFingerprint,
    bool OutputAvailable,
    DateTimeOffset? ReadyAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    WorkflowFailure? Failure);

public sealed record DagRunSnapshot(
    DagRunId RunId,
    DefinitionId DefinitionId,
    DefinitionVersion DefinitionVersion,
    DefinitionFingerprint DefinitionFingerprint,
    DagRunStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<DagNodeSnapshot> Nodes);

public abstract record DagNodeOutputResult<TOutput>
{
    private protected DagNodeOutputResult();
    public sealed record Available(TOutput Output) : DagNodeOutputResult<TOutput>;
    public sealed record Unavailable(
        DagNodeStatus Status,
        WorkflowFailure? Failure) : DagNodeOutputResult<TOutput>;
}

public abstract record DagRegistrationResult<TRunInput>
{
    private protected DagRegistrationResult();
    public DagDefinitionHandle<TRunInput> GetHandleOrThrow();
    public sealed record Registered(
        DagDefinitionHandle<TRunInput> Handle) : DagRegistrationResult<TRunInput>;
    public sealed record Conflict(
        DefinitionRegistrationConflict Error) : DagRegistrationResult<TRunInput>;
}

public abstract record DagStartResult
{
    private protected DagStartResult();
    public DagRunHandle GetHandleOrThrow();
    public sealed record Accepted(
        DagRunHandle Handle,
        bool WasExisting) : DagStartResult;
    public sealed record Conflict(
        StartIdempotencyConflict Error) : DagStartResult;
}

public enum DagCancellationRequestStatus
{
    Requested,
    AlreadyRequested,
    AlreadyTerminal
}

public interface IDagDefinitionRegistry
{
    DagRegistrationResult<TRunInput> Register<TRunInput>(
        WorkflowDagPlan<TRunInput> plan);
}

public sealed class DagDefinitionHandle<TRunInput>
{
    internal DagDefinitionHandle();
    public DefinitionId DefinitionId { get; }
    public DefinitionVersion DefinitionVersion { get; }
    public DefinitionFingerprint DefinitionFingerprint { get; }

    public ValueTask<DagStartResult> StartOrGetAsync(
        TRunInput input,
        StartIdempotencyKey idempotencyKey,
        CancellationToken cancellationToken = default);

    public ValueTask<DagRunHandle> GetRunAsync(
        DagRunId runId,
        CancellationToken cancellationToken = default);
}

public sealed class DagRunHandle
{
    internal DagRunHandle(DagRunId runId);
    public DagRunId RunId { get; }

    public ValueTask<DagRunSnapshot> GetSnapshotAsync(
        CancellationToken cancellationToken = default);

    public ValueTask<DagNodeOutputResult<TOutput>> GetOutputAsync<TOutput>(
        DagNodeRef<TOutput> node,
        CancellationToken cancellationToken = default);

    public ValueTask<DagRunSnapshot> WaitForTerminalAsync(
        CancellationToken cancellationToken = default);

    public ValueTask<DagCancellationRequestStatus> RequestCancellationAsync(
        CancellationToken cancellationToken = default);
}
```

The DAG run input is immutable for the run. `OutputOf` returns only the successful committed child
output of a declared direct resultful dependency. A resultless node can be a dependency but cannot
be passed to `OutputOf`. Because the projector is opaque code, the runtime validates every access
when it evaluates the mapping after all declared direct dependencies succeed. Access to an
undeclared, non-direct, resultless, foreign-plan, or otherwise unavailable dependency, or another
projector failure, fails that node with stable code `DAG_INPUT_MAPPING_INVALID` before mapped-input
commit or child start; its dependants become dependency-blocked and independent nodes remain
eligible. A valid projector may be re-invoked before commit, but exactly one codec-normalized node
input commits before child start and is reused after restart. `MappedInputFingerprint` hashes those
committed bytes; it does not claim to hash projector code. Child workflow state remains private.
Small immutable output DTOs flow through the DAG; large data, logs, artifacts, and datasets use
external references.

Each node calls `MapInput` exactly once; `Build` rejects inspectable faults: a missing/duplicate
mapping, duplicate node/dependency, self-dependency, cycle, or foreign-plan reference. It does not
claim to inspect delegate control flow for an `OutputOf` call. A structurally valid independent node
that maps only run input is legal and need not be consumed by another node. The builder copies
dependency arrays and preserves the first authored node ordinal. `Build` and `TryBuild` use the
same deterministic diagnostic set and ordering as workflow compilation.

Every node receives a zero-based authored ordinal. It is the stable snapshot order and the
admission/tie-break order among nodes that are simultaneously eligible. V1 has one fixed failure
rule rather than a public policy family: a failed node blocks all transitive dependants,
independent ready/running nodes continue, and the run becomes terminal only when no node can still
progress. Child `Failed`, `TimedOut`, or `Terminated` maps to node `Failed` with stable failure
codes `CHILD_FAILED`, `CHILD_TIMED_OUT`, or `CHILD_TERMINATED`; a child cancelled without a DAG-run
cancellation request maps to `Failed` with `CHILD_CANCELLED`. A run succeeds only when every node
succeeds. Snapshot/result values returned by the runtime and all nested collections are immutable
and detached; constructing an equivalent data value has no management authority. Every terminal
snapshot reports nodes in authored order.

Each node occurrence is one durable child workflow instance. Internal runtime child-start and
join commands provide idempotency, lineage, restart recovery, and cancellation propagation;
they are not public `RunChild`/`RunChildren` authoring members. Host option
`MaxConcurrentNodes` governs DAG-node admission separately from per-instance fiber limits and
durable resource pools. It counts every admitted nonterminal child instance, including one parked
in a wait, delay, or lease queue, until that child becomes terminal.

Registration is explicit. `Registered` returns the only start-capable typed definition handle;
same identity/version with a different structural fingerprint returns
`DefinitionRegistrationConflict`. `StartOrGetAsync` binds definition identity/version/fingerprint
plus deterministic serialized run input to `StartIdempotencyKey`; compatible reuse returns
`Accepted` with the same `DagRunId` and `WasExisting = true`, while changed input or definition
identity returns typed `StartIdempotencyConflict`. A run handle cannot start arbitrary
children or inspect child state. Its snapshot preserves authored node order, and node output is
available only after that declared node succeeds; pending, failed, cancelled, or
dependency-blocked nodes return the typed unavailable variant. A node reference from another
plan is rejected before querying. Returned outputs/snapshots are detached.
`GetRunAsync` reopens only a run belonging to the typed DAG definition; absence and mismatch use
`DagRunNotFoundException` and `DagRunDefinitionMismatchException`. The closed registration/start
unions remain available for conflict inspection and `WasExisting`; their `GetHandleOrThrow()`
helpers return the successful handle or throw the corresponding typed exception carrying that
same conflict value, so ordinary code needs no cast.

`WaitForTerminalAsync` is a notification-driven, race-free wait that returns a detached terminal
snapshot. It uses subscribe-then-recheck around the committed notification boundary and never
polls. Caller cancellation cancels only the local wait, not the DAG run.

The ordinary success path therefore needs neither casts nor polling:

```csharp
var dagDefinition = dagRegistry.Register(plan).GetHandleOrThrow();
var start = await dagDefinition.StartOrGetAsync(input, idempotencyKey, token);
var run = start.GetHandleOrThrow();
var terminal = await run.WaitForTerminalAsync(token);
```

Cancellation is an idempotent request, not proof that a running child or external effect stopped.
One committed request prevents new admission, immediately marks pending/ready nodes `Cancelled`,
and marks running nodes `CancellationRequested` while propagating the request to their children.
Already dependency-blocked nodes remain `DependencyBlocked`. The run reaches `Cancelled` only
after every running child is terminal and no node can progress; child/protected-work cleanup keeps
its own quarantine semantics. Without a DAG-run cancellation request, any non-success child makes
the run `Failed`; otherwise all nodes succeeded and the run is `Succeeded`. No public DAG policy
selector, caller-owned ready/completed set, or child-management back door ships in v1.
Cancellation and final successful terminalization use one serialized winner: success first returns
`AlreadyTerminal`; cancellation first makes the eventual run `Cancelled` even if an already-running
child subsequently reports success.

## 17.3 Shared compiler, fingerprint, and diagnostic contract

The shared compiler uses a positive allowlist for the selected mode and produces one immutable
plan with a format version and deterministic fingerprint. Compiled plans, instructions,
fibers, scopes, policies, and routing identities are implementation details. Compilation
validates the complete graph before registration, including:

- exactly one root `Init`, exactly one root generation terminal (`End` or unconditional durable
  `ContinueAsNew`), and complete successful-path termination;
- staged-builder and root/nested capability legality;
- deterministic unique branch/node identities and valid returns/merges;
- fixed-codec round-trip compatibility for state, input, branch/item results, workflow output,
  DAG inputs/outputs, and detached snapshots;
- positive authored `ForEachOptions` values and host-configured concurrency ceilings;
- no root loop cycle that can consume work forever without a business step, suspension,
  failure, continue-as-new, or exit; internal turn yielding is runtime-owned;
- one committed `StepOperationId` coordinate per logical step visit, distinct across loop,
  item, branch, and generation occurrences;
- scoped lease ancestry/quiescence, exact obligation identity, and selector/request validity;
- definition identity/version/fingerprint consistency.

Diagnostics have a stable machine-readable code, severity, and structured `AuthoredLocation`.
Message text may improve. A compile orders diagnostics by `AuthoredLocation.Value` and then `Code`,
both with ordinal string comparison. Local invalid arguments fail at the fluent call; eager
decorator misuse and duplicate `CompleteWithin` throw `WorkflowDefinitionException` with one
diagnostic; graph-wide diagnostics accumulate through `TryBuild` and `Build` throws the same
immutable ordered set.

Every authored delegate other than an ephemeral lambda business-step body is deterministic and
side-effect-free. Durable `Init`, conditions, selectors, input/output projectors, merges, and
`End` output selectors may be invoked more than once before their result wins a commit. OrcaCore
guarantees one committed normalized result and no delegate re-invocation after that commit, not
exactly-once delegate invocation. Durable business steps are at-least-once and use
`StepOperationId` for external idempotency.

The complete first-release workflow compiler/runtime diagnostic catalog is below. Every entry has
severity `Error`; no implementation may emit an undocumented code or assign one code to multiple
meanings.

| Code | Stable name and meaning |
|---|---|
| `SFE-AUTH-ROOT-001` | `MissingRootInit`: no root initialization exists. |
| `SFE-AUTH-ROOT-002` | `MultipleRootInit`: more than one root initialization exists. |
| `SFE-AUTH-ROOT-003` | `MissingRootTerminal`: no root `End` or approved terminal `ContinueAsNew` exists. |
| `SFE-AUTH-ROOT-004` | `MultipleRootTerminal`: more than one root generation terminal exists. |
| `SFE-AUTH-PATH-001` | `IncompleteSuccessfulPath`: a reachable successful path does not converge on the generation terminal. |
| `SFE-AUTH-CAP-001` | `CapabilityNotAvailable`: a hand-built/stale graph uses a member outside its selected mode/location. |
| `SFE-AUTH-BRANCH-001` | `DuplicateBranchIdentity`: fixed branch identities collide. |
| `SFE-AUTH-BRANCH-002` | `MissingBranchReturn`: a reachable successful branch/item path has no typed return. |
| `SFE-AUTH-BRANCH-003` | `MultipleBranchReturn`: a branch/item has more than one return terminal. |
| `SFE-AUTH-BRANCH-004` | `EmptyParallelScope`: a fixed root `Parallel` contains no authored branch. |
| `SFE-AUTH-JOIN-001` | `MissingJoin`: a fan-out has no selected join. |
| `SFE-AUTH-JOIN-002` | `InvalidMergeContract`: result/state/merge contracts do not agree. |
| `SFE-AUTH-DECORATOR-001` | `MisplacedDecorator`: a retry/timeout/transient decorator is repeated or has no eligible immediately preceding business step. |
| `SFE-AUTH-DEADLINE-001` | `DuplicateWorkflowDeadline`: `CompleteWithin` is selected more than once; the second call is primary and the first is related. |
| `SFE-AUTH-LIFECYCLE-001` | `SupersededBuilderHandle`: a root builder handle belongs to an earlier authoring epoch or has a required join pending. |
| `SFE-AUTH-LIFECYCLE-002` | `JoinAlreadySelected`: the required join stage has already selected a terminal join operation. |
| `SFE-AUTH-LIFECYCLE-003` | `FrozenAuthoringSession`: the root authoring session has already selected its generation terminal and is immutable. |
| `SFE-AUTH-LIFECYCLE-004` | `ExpiredLexicalBuilderHandle`: a callback-local nested, branch, item, leased, or scope builder escaped its lexical callback. |
| `SFE-AUTH-LIFECYCLE-005` | `ConcurrentAuthoringConflict`: another authoring operation owns the session operation gate. |
| `SFE-AUTH-LOOP-001` | `NonProgressingLoop`: a root loop cycle can repeat without business work, suspension, failure, rollover, or exit. |
| `SFE-AUTH-LEASE-001` | `LeaseAncestryConflict`: acquisition is reachable under a live capacity-reserving lexical ancestor; primary/related locations identify both scopes. |
| `SFE-AUTH-LEASE-003` | `LeaseBlocksContinueAsNew`: rollover is reached before a lexical lease scope exits. |
| `SFE-TYPE-001` | `IncompatibleStateOrResultType`: a hand-built graph has incompatible state/result/output contracts. |
| `SFE-TYPE-002` | `CodecUnsupportedShape`: a required persisted/detached value shape cannot use `orcacore-json-v1`. |
| `SFE-LIMIT-001` | `InvalidForEachLimit`: a hand-built fan-out bypasses positive item/concurrency bounds. |
| `SFE-RUN-001` | `NonQuiescentContinueAsNew`: runtime defense rejects rollover while a descendant or owned obligation remains. |
| `SFE-RUN-002` | `LeaseAncestryViolation`: runtime defense terminally fails before queue/pool mutation and suppresses merges/restart loops. |

The complete DAG build catalog is:

| Code | Stable name and meaning |
|---|---|
| `DAG-AUTH-NODE-001` | `DuplicateNodeIdentity`: node identities collide. |
| `DAG-AUTH-DEPENDENCY-001` | `DuplicateDependency`: one node repeats a declared dependency. |
| `DAG-AUTH-DEPENDENCY-002` | `SelfDependency`: a node depends on itself. |
| `DAG-AUTH-DEPENDENCY-003` | `Cycle`: declared edges contain a cycle. |
| `DAG-AUTH-DEPENDENCY-004` | `ForeignPlanReference`: a declared node/dependency reference belongs to another plan. |
| `DAG-AUTH-MAP-001` | `MissingMapInput`: a node has no mapping delegate. |
| `DAG-AUTH-MAP-002` | `DuplicateMapInput`: a node assigns mapping more than once. |

Opaque mapper access is not a build diagnostic; it uses runtime node failure code
`DAG_INPUT_MAPPING_INVALID` before mapped-input commit or child start.

`AuthoredLocation.Value` uses exactly `workflow:$` or `dag:$` followed by zero or more
slash-delimited tokens from `n:dddddddd`, `if:true`, `if:false`, `while:body`,
`parallel:dddddddd`, `foreach:body`, `lease:body`, and `dag-node:dddddddd`. Every ordinal is
zero-based and formatted as exactly eight invariant-culture decimal digits. A synthetic root
diagnostic uses the bare mode root. Values contain no localized/message text; primary and related
locations are de-duplicated and sorted by `Value` with ordinal string comparison.

All public `OrcaCoreException` codes and runtime failure projection codes are fixed here:

| Code | Owning failure |
|---|---|
| `WF-DEFINITION-INVALID` | `WorkflowDefinitionException` |
| `WF-WAIT-TIMEOUT` | `WorkflowWaitTimeoutException` |
| `WF-STEP-TIMEOUT` | `StepAttemptTimeoutException` |
| `WF-DEADLINE-EXCEEDED` | `WorkflowDeadlineExceededException` |
| `WF-WAIT-AMBIGUOUS` | `AmbiguousWaitRegistrationException` |
| `WF-STATE-TYPE-MISMATCH` | `WorkflowStateTypeMismatchException` |
| `WF-LEASE-LOST` | `LeaseLostException` |
| `WF-RESOURCE-POOL-NOT-CONFIGURED` | `ResourcePoolNotConfiguredException` |
| `WF-INSTANCE-NOT-FOUND` | `WorkflowInstanceNotFoundException` |
| `WF-INSTANCE-DEFINITION-MISMATCH` | `WorkflowInstanceDefinitionMismatchException` |
| `WF-DEFINITION-NOT-REGISTERED` | `WorkflowDefinitionNotRegisteredException` |
| `WF-DEFINITION-HOST-INCOMPATIBLE` | `WorkflowDefinitionHostCompatibilityException` |
| `WF-DEFINITION-REGISTRATION-CONFLICT` | `WorkflowDefinitionRegistrationConflictException` |
| `WF-START-IDEMPOTENCY-CONFLICT` | `WorkflowStartIdempotencyConflictException` |
| `WF-OUTPUT-UNAVAILABLE` | `WorkflowOutputUnavailableException` |
| `WF-STEP-UNHANDLED` | normalized non-`OrcaCoreException` step failure |
| `SFE-JOIN-FAILED` | ordered multiple-branch/item aggregate failure |
| `DAG-RUN-NOT-FOUND` | `DagRunNotFoundException` |
| `DAG-RUN-DEFINITION-MISMATCH` | `DagRunDefinitionMismatchException` |
| `DAG-DEFINITION-REGISTRATION-CONFLICT` | `DagDefinitionRegistrationConflictException` |
| `DAG-START-IDEMPOTENCY-CONFLICT` | `DagStartIdempotencyConflictException` |
| `DAG_INPUT_MAPPING_INVALID` | opaque mapper access/projector failure before child start |
| `CHILD_FAILED`, `CHILD_TIMED_OUT`, `CHILD_TERMINATED`, `CHILD_CANCELLED` | normalized DAG child terminal failure |

`OrcaCoreException` validates a nonblank invariant code containing only uppercase ASCII letters,
digits, hyphen, or underscore. Built-in subclasses bind exactly one code above. Application-derived
subclasses use an `APP-` prefix and a stable code; an `OrcaCoreException` maps directly to
`WorkflowFailure.Code`, while any other unhandled exception maps to `WF-STEP-UNHANDLED`. Raw
exception messages and CLR type names remain diagnostic text and never become protocol identity.

The obsolete point-acquisition repeatable-loop diagnostic is not retained. A lexical
acquisition fully contained in one root-loop iteration is valid and releases before the next
iteration.

No compiler or runtime identifier becomes a parallel public author identity. Adding a new stable
code requires a reviewed matrix amendment and a Phase 0 fixture.

## 17.4 Concurrency and resource lifetimes

```csharp
public sealed class StructuredExecutionHostOptions
{
    public int MaxConcurrentExecutionPathsPerInstance { get; init; }
    public IReadOnlyList<StepExecutionThrottle> StepThrottles { get; init; }
}

public sealed class EphemeralEngineHostOptions
{
    public StructuredExecutionHostOptions StructuredExecution { get; init; }
    public IReadOnlyList<TransientPoolDefinition> TransientPools { get; init; }
}

public sealed class DurableEngineHostOptions
{
    public StructuredExecutionHostOptions StructuredExecution { get; init; }
    public DurableResourcePoolOptions ResourcePools { get; init; }
}

public sealed class DagHostOptions
{
    public int MaxConcurrentNodes { get; init; }
}

public sealed class StepExecutionThrottle
{
    private StepExecutionThrottle(Type stepType, int maxConcurrency);
    public Type StepType { get; }
    public int MaxConcurrency { get; }
    public static StepExecutionThrottle For<TStep>(int maxConcurrency);
}

public sealed class TransientPoolDefinition
{
    private TransientPoolDefinition(TransientPoolName name, int capacity);
    public TransientPoolName Name { get; }
    public int Capacity { get; }
    public static TransientPoolDefinition Create(
        TransientPoolName name,
        int capacity);
}

public sealed class DurableResourcePoolDefinition
{
    private DurableResourcePoolDefinition(
        ResourcePoolName name,
        int capacity,
        TimeSpan reviewAfter);
    public ResourcePoolName Name { get; }
    public int Capacity { get; }
    public TimeSpan ReviewAfter { get; }
    public static DurableResourcePoolDefinition Create(
        ResourcePoolName name,
        int capacity,
        TimeSpan reviewAfter);
}

public sealed class DurableResourcePoolOptions
{
    public ResourceGovernancePartitionId PartitionId { get; init; }
    public IReadOnlyList<DurableResourcePoolDefinition> Pools { get; init; }
}

public sealed record DurableResourcePoolSnapshot(
    ResourcePoolName Name,
    int ConfiguredCapacity,
    int ReservedUnits,
    int ResizeDebt,
    int QueuedRequestCount,
    DateTimeOffset? OldestReviewDeadline);

public abstract record DurableResourcePoolResizeResult
{
    private protected DurableResourcePoolResizeResult();

    public sealed record Applied(
        ResourcePoolOperationId OperationId,
        ResourcePoolName Pool,
        int Capacity,
        DurableResourcePoolSnapshot Snapshot)
        : DurableResourcePoolResizeResult;

    public sealed record Conflict(
        ResourcePoolOperationId OperationId,
        ResourcePoolName RecordedPool,
        int RecordedCapacity,
        ResourcePoolName AttemptedPool,
        int AttemptedCapacity)
        : DurableResourcePoolResizeResult;
}

public interface IDurableResourcePoolManagement
{
    ValueTask<IReadOnlyList<DurableResourcePoolSnapshot>> ListAsync(
        CancellationToken cancellationToken = default);

    ValueTask<DurableResourcePoolSnapshot> GetAsync(
        ResourcePoolName pool,
        CancellationToken cancellationToken = default);

    ValueTask<DurableResourcePoolResizeResult> ResizeAsync(
        ResourcePoolName pool,
        int capacity,
        ResourcePoolOperationId operationId,
        CancellationToken cancellationToken = default);
}

public sealed class ResourceGovernanceRecord
{
    private ResourceGovernanceRecord(
        long sequence,
        string formatId,
        ReadOnlyMemory<byte> payload,
        string checksum);
    public long Sequence { get; }
    public string FormatId { get; }
    public ReadOnlyMemory<byte> Payload { get; }
    public string Checksum { get; }
    public static ResourceGovernanceRecord FromPersisted(
        long sequence,
        string formatId,
        ReadOnlyMemory<byte> payload,
        string checksum);
}

public sealed class ResourceGovernanceStream
{
    private ResourceGovernanceStream(
        long version,
        IReadOnlyList<ResourceGovernanceRecord> records);
    public long Version { get; }
    public IReadOnlyList<ResourceGovernanceRecord> Records { get; }
    public static ResourceGovernanceStream Create(
        long version,
        IReadOnlyList<ResourceGovernanceRecord> records);
}

public abstract record ResourceGovernanceAppendResult
{
    private protected ResourceGovernanceAppendResult();
    public sealed record Committed(long Version) : ResourceGovernanceAppendResult;
    public sealed record Conflict(long ActualVersion) : ResourceGovernanceAppendResult;
}

public interface IDurableResourceGovernanceStore
{
    ValueTask<ResourceGovernanceStream> LoadAsync(
        ResourceGovernancePartitionId partitionId,
        CancellationToken cancellationToken = default);

    ValueTask<ResourceGovernanceAppendResult> AppendAsync(
        ResourceGovernancePartitionId partitionId,
        long expectedVersion,
        IReadOnlyList<ResourceGovernanceRecord> records,
        CancellationToken cancellationToken = default);
}

internal enum DurableResourceLeaseCommitBarrier
{
    WorkflowPendingObligationCommitted,
    GovernanceReservationCommitted,
    WorkflowActivationCommitted,
    GovernanceOwnershipConfirmed
}

internal sealed record DurableResourceLeaseCommitBarrierFact(
    DurableResourceLeaseCommitBarrier Barrier,
    ResourceGovernancePartitionId PartitionId,
    string ObligationId,
    InstanceId InstanceId,
    int Generation,
    string FiberOccurrence,
    string ScopeOccurrence,
    LeaseProtectionToken ProtectionToken,
    long WorkflowVersion,
    long GovernanceVersion,
    IReadOnlyList<DurableResourceLeaseTicketSnapshot> Tickets);

internal interface IDurableResourceLeaseCertificationGate
{
    ValueTask OnPostCommitAsync(
        DurableResourceLeaseCommitBarrierFact fact,
        CancellationToken cancellationToken = default);
}
```

`IDurableResourcePoolManagement.GetAsync` and `ResizeAsync` reject a null pool locally with
`ArgumentNullException` and throw
`ResourcePoolNotConfiguredException` for a well-formed unconfigured pool without provider mutation.
`ResizeAsync` additionally rejects a non-positive `capacity` with `ArgumentOutOfRangeException`
and a null `operationId` with `ArgumentNullException` at the public call. Capacity is strictly
positive in v1; zero is not a drain/delete alias. `Applied`/`Conflict`
therefore remain the exhaustive results only after pool and argument validation succeeds.

Both values are required and positive at host startup. V1 has no
`WorkflowAuthoringOptions`: definition semantics are expressed by typed nodes and host safety
configuration remains outside the authored graph. The host path ceiling is authoritative, so
there is no author-versus-host winner rule or misleading author promise.
`ForEachOptions.MaxConcurrency`
may only tighten that ceiling for its node. Hitting a concurrency ceiling parks admission; it
does not fail the workflow or change authored ordering.
V1 has no independent host-wide workflow-instance/advancement ceiling, untyped general-body
ceiling, fail-fast/capacity-wait-timeout policy, or provider-defined transient-governance SPI.
Driver segment budgets are a separate fairness mechanism and never end a capacity wait.
`StepExecutionThrottle.For<TStep>` is keyed solely by the exact named step type authored through
`Then<TStep>()`; it does not match assignable/base types or infer a type for an ephemeral lambda.
Several transient pools may be configured, but the single per-step `WithTransientPool` decorator
selects at most one. Host options are programmatically constructed typed objects, copied and
validated immediately. V1 makes no configuration-binder compatibility claim and publishes no
binder DTO/converter/section overload. Null/default collections, zero values, duplicate entries,
or missing nested options fail registration/startup and are never silently defaulted or normalized.
Factory-only authoring values retain their stronger construction-time invariants.

The execution-path ceiling has one countable token model. A runnable root, root-`Parallel` branch,
or root-`ForEach` item owns one token. It releases that token when it parks on a wait, delay,
resource request, or join, and reacquires one before it can progress. A root parent releases its
token before admitting fixed branches or items and reacquires one only for the merge/continuation.
Consequently, a ceiling of one cannot deadlock a root fan-out merely because its parent is waiting.
All root fixed branches and root-`ForEach` items share the same per-instance token pool; fixed
branches queue by authored ordinal and items by index.
`ForEachOptions.MaxConcurrency` separately counts admitted nonterminal item scopes, including an
item whose body is parked in a wait, delay, or durable resource request, until that item becomes
terminal. Ordinary token-ignoring
attempts that have been timed out/fenced no longer own a logical path token, but continue holding
their physical step-throttle/transient slot until they return. A leased in-process retry does not
overlap its still-running prior body and retains the same persistent lease obligation.
`DagHostOptions.MaxConcurrentNodes` likewise counts started nonterminal child instances, including
a child parked in a wait, delay, or lease queue, until its node is terminal; a child does not free
DAG admission merely by releasing an in-instance path token. Eligible nodes wait by authored
ordinal.

The following limits are distinct and MUST NOT share names or persistence claims:

1. **Per-step execution throttle**: named-step exact-type host-local capacity held only while a
   business-step attempt actually runs; lambdas have no throttle key.
2. **Named transient pool**: ephemeral host-local shared capacity identified by
   `TransientPoolName`; a granted slot is released only when the guarded body stops.
3. **Per-instance execution-path ceiling**: host option
   `MaxConcurrentExecutionPathsPerInstance`; authors cannot raise it. Root fixed `Parallel`
   branches are admitted in authored order. A root `ForEach` node's optional cap composes as the
   lower limit.
4. **DAG-node ceiling**: `MaxConcurrentNodes`, owned by the DAG host and separate from child
   workflow/resource limits.
5. **Durable resource lease**: persisted cross-host logical capacity identified by
   `ResourcePoolName`, held for one lexical acquisition scope, retained through `AmbiguousHeld`
   retry, and protected by exact obligation/ticket/provider-generation identity.

Local structured fibers are cooperative: at most one unfenced attempt owns commit authority for
one workflow instance at a time. Every attempt mutates only its detached copy. Parking one branch
or item releases the instance turn so runnable siblings can progress. A token-ignoring timed-out
body may still run physically against its discarded copy while a retry or sibling progresses;
true simultaneous external work also occurs across workflow instances, DAG child instances, and
external systems.

For durable pools, pending-commit, held, review-marked, ambiguous-held, and quarantined units remain
reserved. Queued, released, and cancelled-before-grant requests reserve zero. Downward resize
may create `max(0, reserved - configured)` no-new-grants debt without revocation. Release may
immediately transfer capacity to a waiter, so global equality with a prior available-capacity
snapshot is not an invariant; exact numeric restoration applies only to an isolated
no-waiter/no-resize case.

A pool review timestamp is a reconciliation deadline, not a lease expiry. It marks and audits
the exact ticket while retaining capacity. A multi-pool grant carries one review deadline per
ticket from that pool's policy; marking one ticket does not release it or rewrite sibling
deadlines. The baseline has no holder renewal and never
reclaims live or ambiguous ownership from elapsed time alone.

`StepExecutionThrottle`, `TransientPoolDefinition`, and every host ceiling require positive
capacity and reject duplicate names/types at startup. Transient pools are ephemeral host-local
configuration and have no restart claim. Durable pool definitions are durable-host configuration;
`ReviewAfter` is positive and controls when ownership is marked for reconciliation, never when it
is released. `DurableResourcePoolDefinition.Capacity` is immutable creation metadata, while
`DurableResourcePoolSnapshot.ConfiguredCapacity` is the replayed current value changed only by
accepted resize operations. First startup creates definitions atomically; later hosts must present
identical name/creation-capacity/review values or fail startup. Restart agreement never compares
host `Capacity` to, or overwrites, the replayed current configured capacity or resize debt.
Capacity changes use idempotent `ResizeAsync`; changing
review policy after creation is deferred. Queue order is fixed FIFO by committed request sequence; v1 has no author/provider
queue-policy plug-in. Downward resize records debt rather than revoking tickets. `ResizeAsync` is
idempotent by its caller-stable operation ID: same ID/same mutation returns the originally recorded
`Applied`, while same ID/different pool or capacity returns `Conflict` with recorded and attempted
facts and performs no mutation.
There is no force-release API. Stop/fence confirmation uses `IDurableResourceLeaseRecovery`.

V1 providers implement one serialized **resource-governance aggregate per configured provider
partition**. That aggregate owns every pool definition, queued atomic multi-pool request,
reservation/ticket, review mark, resize operation, confirmation binding, and tombstone in the
partition. One expected-version append grants all requirements or none, so no cross-pool partial
grant can occur. The workflow/resource handoff is an idempotent reservation protocol, not a false
cross-aggregate transaction: (1) the workflow commits its normalized pending obligation, (2) the
governance aggregate reserves all tickets under the exact instance/generation/scope occurrence,
(3) the workflow commits activation, and (4) governance confirms active ownership. The friend-only
certification gate blocks after each durable commit and before the next protocol command at exact
barriers `WorkflowPendingObligationCommitted`, `GovernanceReservationCommitted`,
`WorkflowActivationCommitted`, and `GovernanceOwnershipConfirmed`. Each immutable fact includes
partition, obligation, instance, generation, fiber/scope occurrence, protection token,
workflow/governance versions, and exact ticket/pool/unit/provider-generation facts. A crash in the
middle leaves capacity reserved; recovery completes or compensates the exact occurrence and never
guesses from time. Certification proves fact equality, no duplicate/ghost ticket, isolated exact
restoration, contended conservation/direct transfer, distinct successor tickets, and resize debt
as the sole permitted over-capacity state. This deterministic friend seam is test-only and never a
public authoring/runtime API. `OrcaCore.Runtime.Protocol` owns the protocol records;
`OrcaCore.Provider.Abstractions.ResourceGovernance.IDurableResourceGovernanceStore` exposes load plus one
expected-version atomic append for this single aggregate, and provider certification injects a
crash/conflict at every boundary, multi-host contention, atomic multi-pool grant, FIFO admission,
resize debt, quarantine, confirmation, and tombstone retention. A provider that can only append
per-workflow streams does not satisfy v1 durable leasing.
`ResourceGovernanceRecord.FromPersisted` copies payload bytes and validates one positive sequence,
supported protocol format, and checksum. `ResourceGovernanceStream.Create` copies non-null records
and requires version zero exactly for an empty stream or a complete ordered sequence `1..Version`
with no gap, duplicate, or reordering. `AppendAsync` copies a non-empty batch numbered consecutively
from `expectedVersion + 1` and commits the complete ordered batch or returns `Conflict`; partial
append is forbidden.
This single serialization point is a deliberate first-release correctness tradeoff for mandatory
EKS scheduling. Future sharding requires an amendment that assigns pools to governance partitions
and forbids or coordinates cross-partition atomic requests; v1 never silently splits one request.

## 17.5 Package and integration boundary

`OrcaCore` is the primary application contracts/authoring package and assembly, not a
dependency-only meta-package. The exhaustive first-release manifest is:

| PackageId / assembly | Tier and owning surface | Direct OrcaCore package dependencies | Packable |
|---|---|---|---:|
| `OrcaCore` | Public application values, authoring, definitions/references, facades/results/errors | none | yes |
| `OrcaCore.Core` | Internal compiler/execution support; no additional supported application surface | `OrcaCore` | yes |
| `OrcaCore.Engine.Ephemeral` | Ephemeral engine role/options and `OrcaCoreEphemeralEngineServiceCollectionExtensions` | `OrcaCore`, `OrcaCore.Core` | yes |
| `OrcaCore.Runtime.Protocol` | Advanced durable command/fact/checkpoint and certification records | `OrcaCore` | yes |
| `OrcaCore.Provider.Abstractions` | Advanced provider ports/commit DTOs/certification | `OrcaCore`, `OrcaCore.Runtime.Protocol` | yes |
| `OrcaCore.Engine.Durable` | Internal durable engine implementation | `OrcaCore`, `OrcaCore.Core`, `OrcaCore.Runtime.Protocol`, `OrcaCore.Provider.Abstractions` | yes |
| `OrcaCore.Durable.Hosting` | Durable engine/ingress roles, options, management/recovery/diagnostics, `OrcaCoreDurableEngineServiceCollectionExtensions` | `OrcaCore`, `OrcaCore.Engine.Durable`, `OrcaCore.Provider.Abstractions` | yes |
| `OrcaCore.Providers.InMemory` | Development/test complete durable provider role | `OrcaCore`, `OrcaCore.Runtime.Protocol`, `OrcaCore.Provider.Abstractions` | yes |
| `OrcaCore.Providers.PostgreSql` | Production complete certified durable provider role and options | `OrcaCore`, `OrcaCore.Runtime.Protocol`, `OrcaCore.Provider.Abstractions` | yes |
| `OrcaCore.Providers.SqlServer` | Production complete certified durable provider role and options | `OrcaCore`, `OrcaCore.Runtime.Protocol`, `OrcaCore.Provider.Abstractions` | yes |
| `OrcaCore.Dag` | Public typed DAG authoring/operation contracts; no visualization surface | `OrcaCore` | yes |
| `OrcaCore.Dag.Hosting` | DAG coordinator/registry/options and sole durable child friend bridge | `OrcaCore.Dag`, `OrcaCore.Durable.Hosting` | yes |

CLR namespace and assembly ownership are also exact; an implementation or guard SHALL NOT infer a
different namespace from an unqualified signature sketch:

| Public/internal family | CLR namespace | Owning PackageId / assembly |
|---|---|---|
| Workflow strong values (all §17.2.1 values except `DagNodeId` and `DagRunId`), workflow authoring/definitions/references, ordinary handles/events/results/errors | `OrcaCore` | `OrcaCore` |
| `DagNodeId`, `DagRunId`, DAG authoring/definitions/references/handles/results/errors | `OrcaCore.Dag` | `OrcaCore.Dag` |
| `StructuredExecutionHostOptions`, `StepExecutionThrottle` | `OrcaCore.Hosting` | `OrcaCore` |
| `EphemeralEngineHostOptions`, `TransientPoolDefinition`, ephemeral registration extension | `OrcaCore.Hosting` | `OrcaCore.Engine.Ephemeral` |
| `DurableEngineHostOptions`, `DurableResourcePoolOptions`, `DurableResourcePoolDefinition`, durable engine/ingress registration extension | `OrcaCore.Hosting` | `OrcaCore.Durable.Hosting` |
| `DagHostOptions`, DAG registration extension | `OrcaCore.Dag.Hosting` | `OrcaCore.Dag.Hosting` |
| Lease/pool statuses, snapshots, closed resize result, governance records/stream/append result | `OrcaCore.Runtime.Protocol.ResourceGovernance` | `OrcaCore.Runtime.Protocol` |
| `IDurableResourcePoolManagement`, `IDurableResourceLeaseRecovery`, `IDurableResourceLeaseDiagnostics` | `OrcaCore.Hosting.ResourceLeases` | `OrcaCore.Durable.Hosting` |
| `IDurableResourceGovernanceStore` | `OrcaCore.Provider.Abstractions.ResourceGovernance` | `OrcaCore.Provider.Abstractions` |
| Four barrier enum/fact/gate types | `OrcaCore.Engine.Durable.ResourceGovernance` | `OrcaCore.Engine.Durable` (internal) |
| In-memory provider extension | `OrcaCore.Providers.InMemory` | `OrcaCore.Providers.InMemory` |
| PostgreSQL options/extension | `OrcaCore.Providers.PostgreSql` | `OrcaCore.Providers.PostgreSql` |
| SQL Server options/extension | `OrcaCore.Providers.SqlServer` | `OrcaCore.Providers.SqlServer` |

Every direct OrcaCore edge not listed is forbidden. In particular, protocol never references
provider abstractions, application/core never references DAG or advanced packages, providers never
reference an engine, and no OrcaCore package references a companion integration. Provider-native
dependencies remain inside their provider package.

Implementation package boundaries use exact type-safe internal friends so the package split never
forces compiler, execution-kernel, concrete engine, provider, or hosted-loop types into exported
metadata. The complete product-friend set is:

- `OrcaCore -> OrcaCore.Core`, `OrcaCore.Engine.Ephemeral`, and `OrcaCore.Engine.Durable` for
  compile-checked application-owned internal authoring/runtime contracts; these access grants do
  not create reverse package references;
- `OrcaCore.Core -> OrcaCore.Engine.Ephemeral` and `OrcaCore.Engine.Durable` for the shared internal
  compiler/execution kernel;
- `OrcaCore.Engine.Durable -> OrcaCore.Durable.Hosting` for the internal durable role bootstrap;
- `OrcaCore.Durable.Hosting -> OrcaCore.Dag.Hosting` for the named, versioned internal child-start/
  join contract.

The proposed eighth product friend is `OrcaCore -> OrcaCore.Dag` for the five compiler-created
authoring value families and one shared fingerprint operation named in §17.2.6. It is excluded
from the current compiled set above until the post-gate contract receives independent approval
and the implementation slice passes exact friend and member-reference guards. It is not a
DAG-to-durable runtime bridge; the existing Durable Hosting to DAG Hosting edge remains the only
such bridge.

These CLR access grants do not create reverse package dependencies or public application/provider
SPIs. Exact owning white-box test friends are `OrcaCore.Core.Tests`,
`OrcaCore.Engine.Ephemeral.Tests`, `OrcaCore.Engine.Durable.Tests`, `OrcaCore.Hosting.Tests`,
`OrcaCore.Providers.PostgreSql.Tests`, and `OrcaCore.Providers.SqlServer.Tests`. The only
cross-package test friend remains
`OrcaCore.Engine.Durable -> OrcaCore.ProviderCertification` for the four internal post-commit
barrier types. Acceptance, behavior-scenario, compile-fixture, and integration assemblies receive
no friend access. Every other friend is forbidden; a future product or test friend requires a
reviewed matrix amendment first.

Phase 0 packs every manifest entry at exact verification version `0.0.0-phase0` into repository-local
feed `artifacts/phase0-packages`. Clean fixtures use only `PackageReference` and that feed: no
`ProjectReference`, source inclusion, loose DLL, undeclared package ID, or repository build output.
`OrcaCore.Runtime.Protocol` and `OrcaCore.Provider.Abstractions` are advanced packages for
provider/custom-host implementers; ordinary applications do not directly select them or expose
their types in application signatures.

The seven first-release Microsoft-hosting entry points are exact and role-specific:

```csharp
namespace OrcaCore.Hosting
{
    public static class OrcaCoreEphemeralEngineServiceCollectionExtensions
    {
        public static OrcaCoreEphemeralEngineBuilder AddOrcaCoreEphemeralEngine(
            this IServiceCollection services,
            EphemeralEngineHostOptions options);
    }

    public static class OrcaCoreDurableEngineServiceCollectionExtensions
    {
        public static OrcaCoreDurableEngineBuilder AddOrcaCoreDurableEngine(
            this IServiceCollection services,
            DurableEngineHostOptions options);

        public static IServiceCollection AddOrcaCoreDurableEventIngress(
            this IServiceCollection services);
    }

    public sealed class OrcaCoreEphemeralEngineBuilder
    {
        internal OrcaCoreEphemeralEngineBuilder(IServiceCollection services);
        public OrcaCoreEphemeralEngineBuilder AddWorkflow<TInput>(
            EphemeralWorkflowDefinition<TInput> definition);
        public OrcaCoreEphemeralEngineBuilder AddWorkflow<TInput, TOutput>(
            EphemeralWorkflowDefinition<TInput, TOutput> definition);
    }

    public sealed class OrcaCoreDurableEngineBuilder
    {
        internal OrcaCoreDurableEngineBuilder(IServiceCollection services);
        public OrcaCoreDurableEngineBuilder AddWorkflow<TInput>(
            DurableWorkflowDefinition<TInput> definition);
        public OrcaCoreDurableEngineBuilder AddWorkflow<TInput, TOutput>(
            DurableWorkflowDefinition<TInput, TOutput> definition);
    }
}

namespace OrcaCore.Providers.InMemory
{
    public static class OrcaCoreInMemoryProviderServiceCollectionExtensions
    {
        public static IServiceCollection AddOrcaCoreInMemoryDurableProvider(
            this IServiceCollection services);
    }
}

namespace OrcaCore.Providers.PostgreSql
{
    public sealed class PostgreSqlDurableProviderOptions
    {
        public PostgreSqlDurableProviderOptions(
            string connectionString,
            string schema);
        public string ConnectionString { get; }
        public string Schema { get; }
    }

    public static class OrcaCorePostgreSqlProviderServiceCollectionExtensions
    {
        public static IServiceCollection AddOrcaCorePostgreSqlDurableProvider(
            this IServiceCollection services,
            PostgreSqlDurableProviderOptions options);
    }
}

namespace OrcaCore.Providers.SqlServer
{
    public sealed class SqlServerDurableProviderOptions
    {
        public SqlServerDurableProviderOptions(
            string connectionString,
            string schema);
        public string ConnectionString { get; }
        public string Schema { get; }
    }

    public static class OrcaCoreSqlServerProviderServiceCollectionExtensions
    {
        public static IServiceCollection AddOrcaCoreSqlServerDurableProvider(
            this IServiceCollection services,
            SqlServerDurableProviderOptions options);
    }
}

namespace OrcaCore.Dag.Hosting
{
    public static class OrcaCoreDagHostingServiceCollectionExtensions
    {
        public static IServiceCollection AddOrcaCoreDag(
            this IServiceCollection services,
            DagHostOptions options);
    }
}
```

Engine/provider registration copies and validates programmatically constructed options immediately,
registers the selected hosted loops, and is idempotent only for the same role/options; conflicting
duplicate registration fails startup. V1 publishes no configuration-binder DTO/converter/section
overload. One service provider cannot select both ephemeral and durable engines: mixed engine roles
fail deterministically rather than making registry or ingress ownership implicit.
Durable-engine startup requires exactly one complete certified provider role set and the configured
resource-governance partition. `AddOrcaCoreDurableEventIngress` is the definition-less callback
role: it exposes `IWorkflowEventIngress` and durable inbox/start-intent/continuation handoff but registers no
definition registry, execution worker, timer/reconciliation loop, or DAG coordinator. The durable
engine includes ingress and owns progression; registering callback-only ingress beside that engine
is rejected. `AddOrcaCoreEphemeralEngine` registers only process-local wait matching and no durable
ingress contract; the durable engine/ingress role registers `IWorkflowEventIngress`.
The in-memory durable provider is development/test-only and makes no process-restart claim.
`AddOrcaCorePostgreSqlDurableProvider` and `AddOrcaCoreSqlServerDurableProvider` each supply one
complete independently certified production role set. Each provider's required nonblank
`ConnectionString` and `Schema` are copied and validated before any partial service registration.
Applications never register individual runtime-protocol ports.
`AddOrcaCoreDag` requires the durable-engine role and adds only the DAG coordinator/registry. There
is no catch-all `AddOrcaCore`, `AddOrcaCoreHostedServices`, second hosted-service toggle, implicit
mode selection, serializer replacement hook, binder overload, or partial provider-role shortcut.

The companion scheduler/integration project may remain in `OrcaCore.slnx` for v1. It owns
Kubernetes clients and `batch/v1 Job` objects, AWS authentication/discovery if required,
manifests, namespaces, cluster targets, watcher/reconciler deployment, job-specific DTOs, and
operator policy. No Kubernetes/AWS/job-system type or SDK appears in an OrcaCore public
signature or dependency closure. Standard Kubernetes access does not require an AWS package;
AWS-specific code is added only for a concrete AWS capability.

A durable typed step may make a short, bounded, idempotent create-or-observe API call using
`StepOperationId` and, inside a lease scope, `LeaseProtectionToken`. The external workload runs
outside the step. A watcher reports a normalized event with one caller-stable `EventId` reused
on redelivery. When that terminal report proves protected work stopped, the workflow keeps the
`Wait` and the immediately following validation inside the same lease; it validates the payload,
Kubernetes Job UID, `StepOperationId`, `LeaseProtectionToken`, and terminality before lexical
release. An invalid or unproven report preserves ambiguity/quarantine. A trusted reconciler stops
protected work and confirms the generic lease token.
OrcaCore guarantees stable identity, at-least-once invocation, event deduplication, and exact
lease accounting; it does not claim exactly-once external API calls or external fence
enforcement.

## 17.6 Explicitly deferred or removed surface

Deferred capabilities are scored against the authoring shapes they restore, not against their
individual appeal. As of 2026-07-28 the v1 residue is unbounded data-dependent repetition, plus
the budget and observation preconditions under which root-only encodings substitute for nested
ones. Conditional/finite `ContinueAsNew` restores the former at the generation level and is the
cheapest such recovery; nested `While` restores unbounded per-unit iteration. Nested `Parallel`
restores neither and was declined on that basis.

**Re-entry bar for nested fan-out.** A concrete workload must demonstrate material latency,
throughput, memory, or **budget-acceptance** failure against **every applicable root-only
encoding**. A definition rejected by `MaxItems`, parent-state, payload, or snapshot limits under
every applicable encoding, but acceptable when nested, qualifies as budget-acceptance evidence.
It is not claimed that the root-only encodings preserve every bounded workload.

Deferred capabilities remain design-visible but have no public member, alias, obsolete
tombstone, empty implementation, reflection-visible placeholder, or compile fixture that
pretends they ship:

| Capability | Why deferred | Required future amendment |
|---|---|---|
| `WhenFirst` | Loser cancellation and residual protected work are not yet closed. | Winner/tie rule, loser fate, merge/failure contract, lease interaction. |
| Saga | Compensation/recovery/remediation would enlarge the first-release state machine. | Typed action/result API, durable reverse progression, failure/remediation, restart evidence. |
| Public `RunExternalJob` | Kubernetes can use ordinary typed steps; a generic protocol is not yet justified. | Typed request/result, dispatch/outbox topology, identity, timeout/stop, report dedup, resource bracket. |
| Public `RunChild`/`RunChildren` | DAG needs internal child orchestration, not a general provisional fluent member. | Typed parent/child mapping, cancellation, group results, public failure policy. |
| Nested `While` | Re-entry analysis and usability can wait. | Exact allowed nesting and compiler/runtime evidence. |
| Nested `Parallel` | Reviewed 2026-07-28 and declined for v1. Tagged-item flattening and sequential staging cover common cases under explicit budget/dependency/observation preconditions; nesting primarily removes barriers and does not solve unbounded data-dependent repetition. | A measured latency/throughput case the sanctioned encodings cannot meet; exact allowed locations; recursive identity and token/admitted-item semantics; merge/failure behavior; lease interaction; a sound conditional progress contract; occurrence provenance as an ancestry path; and compiler/runtime evidence. |
| Nested `ForEach` | Multilevel dynamic expansion complicates limits and recovery. | Combined item bounds, identity, admission, payload and merge rules. |
| Conditional/finite `ContinueAsNew` | V1's root form is an unconditional/perpetual generation terminal. | A root-only conditional terminal shape, finite path validation, output/lineage semantics, and deadline behavior. |
| Durable lambda steps | Delegate identity/capture/versioning is unsafe for persisted definitions. | Stable code identity and replay/version contract. |
| Definition-wide retry | Reset/state/output semantics are unresolved. | Reset point, retained input/state, attempt identity, terminal policy. |
| Failed-instance/step management retry | Reopening a terminal instance conflicts with immutable terminal history. | New-generation identity, retained input/state, output invalidation, lineage, authorization. |
| Public pause/resume/archive/purge | Not required for the first scheduler release and easy to confuse with waits, terminality, or provider retention. | Authorization, lifecycle transitions, retention/reference safety, provider certification. |
| Workflow-authored `Cancel` | Self-cancellation adds no value over explicit failure/output in v1. | Target semantics, terminal outcome, descendant/lease cleanup, authorization. |

`WaitLong` and author `Yield` are removed rather than deferred: their useful behavior is owned
by durable `Wait` residency and the runtime's internal execution quantum. Their names must be
absent from public assemblies after the v1 refactor.

## 17.7 Registration, continuation, and application projections

```csharp
public enum WorkflowMode { Ephemeral, Durable }

public enum WorkflowInstanceStatus
{
    Pending,
    Running,
    Waiting,
    CancellationRequested,
    Completed,
    Failed,
    TimedOut,
    Cancelled,
    Terminated
}

public sealed class DefinitionRegistrationConflict
{
    internal DefinitionRegistrationConflict(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        DefinitionFingerprint existingFingerprint,
        DefinitionFingerprint attemptedFingerprint);
    public DefinitionId DefinitionId { get; }
    public DefinitionVersion DefinitionVersion { get; }
    public DefinitionFingerprint ExistingFingerprint { get; }
    public DefinitionFingerprint AttemptedFingerprint { get; }
}

public abstract record DefinitionHostCompatibilityFailure
{
    private protected DefinitionHostCompatibilityFailure();

    public sealed record EngineModeMismatch(
        WorkflowMode HostMode,
        WorkflowMode DefinitionMode) : DefinitionHostCompatibilityFailure;

    public sealed record MissingTransientPools(
        IReadOnlyList<TransientPoolName> PoolNames) : DefinitionHostCompatibilityFailure;

    public sealed record MissingDurableResourcePools(
        IReadOnlyList<ResourcePoolName> PoolNames) : DefinitionHostCompatibilityFailure;

    public sealed record MissingWorkflowEventDispatcher
        : DefinitionHostCompatibilityFailure;
}

public sealed class StartIdempotencyConflict
{
    internal StartIdempotencyConflict(
        StartIdempotencyKey key,
        DefinitionId existingDefinitionId,
        DefinitionVersion existingDefinitionVersion,
        DefinitionFingerprint existingDefinitionFingerprint,
        PayloadFingerprint existingInputFingerprint,
        DefinitionId attemptedDefinitionId,
        DefinitionVersion attemptedDefinitionVersion,
        DefinitionFingerprint attemptedDefinitionFingerprint,
        PayloadFingerprint attemptedInputFingerprint);
    public StartIdempotencyKey Key { get; }
    public DefinitionId ExistingDefinitionId { get; }
    public DefinitionVersion ExistingDefinitionVersion { get; }
    public DefinitionFingerprint ExistingDefinitionFingerprint { get; }
    public PayloadFingerprint ExistingInputFingerprint { get; }
    public DefinitionId AttemptedDefinitionId { get; }
    public DefinitionVersion AttemptedDefinitionVersion { get; }
    public DefinitionFingerprint AttemptedDefinitionFingerprint { get; }
    public PayloadFingerprint AttemptedInputFingerprint { get; }
}

public sealed class WorkflowDefinitionRegistrationConflictException : OrcaCoreException
{
    internal WorkflowDefinitionRegistrationConflictException(
        DefinitionRegistrationConflict conflict);
    public DefinitionRegistrationConflict Conflict { get; }
}

public sealed class WorkflowDefinitionNotRegisteredException : OrcaCoreException
{
    internal WorkflowDefinitionNotRegisteredException(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        DefinitionFingerprint definitionFingerprint);
    public DefinitionId DefinitionId { get; }
    public DefinitionVersion DefinitionVersion { get; }
    public DefinitionFingerprint DefinitionFingerprint { get; }
}

public sealed class WorkflowDefinitionHostCompatibilityException : OrcaCoreException
{
    internal WorkflowDefinitionHostCompatibilityException(
        DefinitionHostCompatibilityFailure failure);
    public DefinitionHostCompatibilityFailure Failure { get; }
}

public sealed class WorkflowStartIdempotencyConflictException : OrcaCoreException
{
    internal WorkflowStartIdempotencyConflictException(
        StartIdempotencyConflict conflict);
    public StartIdempotencyConflict Conflict { get; }
}

public sealed class WorkflowInstanceNotFoundException : OrcaCoreException
{
    internal WorkflowInstanceNotFoundException(InstanceId instanceId);
    public InstanceId InstanceId { get; }
}

public sealed class WorkflowInstanceDefinitionMismatchException : OrcaCoreException
{
    internal WorkflowInstanceDefinitionMismatchException(
        InstanceId instanceId,
        DefinitionId expectedDefinitionId,
        DefinitionId actualDefinitionId);
    public InstanceId InstanceId { get; }
    public DefinitionId ExpectedDefinitionId { get; }
    public DefinitionId ActualDefinitionId { get; }
}

public abstract record WorkflowRegistrationResult<TDefinitionHandle>
{
    private protected WorkflowRegistrationResult();
    public TDefinitionHandle GetHandleOrThrow();
    public sealed record Registered(
        TDefinitionHandle Handle) : WorkflowRegistrationResult<TDefinitionHandle>;
    public sealed record Conflict(
        DefinitionRegistrationConflict Error) : WorkflowRegistrationResult<TDefinitionHandle>;
    public sealed record HostIncompatible(
        DefinitionHostCompatibilityFailure Error)
        : WorkflowRegistrationResult<TDefinitionHandle>;
}

public abstract record WorkflowStartResult<TInstanceHandle>
{
    private protected WorkflowStartResult();
    public TInstanceHandle GetHandleOrThrow();
    public sealed record Accepted(
        TInstanceHandle Handle,
        bool WasExisting) : WorkflowStartResult<TInstanceHandle>;
    public sealed record Conflict(
        StartIdempotencyConflict Error) : WorkflowStartResult<TInstanceHandle>;
}

public interface IWorkflowDefinitionRegistry
{
    WorkflowRegistrationResult<EphemeralDefinitionHandle<TInput>> Register<TInput>(
        EphemeralWorkflowDefinition<TInput> definition);
    WorkflowRegistrationResult<EphemeralDefinitionHandle<TInput, TOutput>> Register<TInput, TOutput>(
        EphemeralWorkflowDefinition<TInput, TOutput> definition);
    WorkflowRegistrationResult<DurableDefinitionHandle<TInput>> Register<TInput>(
        DurableWorkflowDefinition<TInput> definition);
    WorkflowRegistrationResult<DurableDefinitionHandle<TInput, TOutput>> Register<TInput, TOutput>(
        DurableWorkflowDefinition<TInput, TOutput> definition);
    EphemeralDefinitionHandle<TInput> GetRequiredHandle<TInput>(
        EphemeralWorkflowRef<TInput> reference);
    EphemeralDefinitionHandle<TInput, TOutput> GetRequiredHandle<TInput, TOutput>(
        EphemeralWorkflowRef<TInput, TOutput> reference);
    DurableDefinitionHandle<TInput> GetRequiredHandle<TInput>(
        DurableWorkflowRef<TInput> reference);
    DurableDefinitionHandle<TInput, TOutput> GetRequiredHandle<TInput, TOutput>(
        DurableWorkflowRef<TInput, TOutput> reference);
}

public sealed class EphemeralDefinitionHandle<TInput>
{
    internal EphemeralDefinitionHandle();
    public DefinitionId DefinitionId { get; }
    public DefinitionVersion DefinitionVersion { get; }
    public DefinitionFingerprint DefinitionFingerprint { get; }
    public ValueTask<WorkflowStartResult<WorkflowInstanceHandle>> StartOrGetAsync(
        TInput input,
        StartIdempotencyKey idempotencyKey,
        CancellationToken cancellationToken = default);
    public ValueTask<WorkflowInstanceHandle> GetInstanceAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken = default);
}

public sealed class EphemeralDefinitionHandle<TInput, TOutput>
{
    internal EphemeralDefinitionHandle();
    public DefinitionId DefinitionId { get; }
    public DefinitionVersion DefinitionVersion { get; }
    public DefinitionFingerprint DefinitionFingerprint { get; }
    public ValueTask<WorkflowStartResult<WorkflowInstanceHandle<TOutput>>> StartOrGetAsync(
        TInput input,
        StartIdempotencyKey idempotencyKey,
        CancellationToken cancellationToken = default);
    public ValueTask<WorkflowInstanceHandle<TOutput>> GetInstanceAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken = default);
}

public sealed class DurableDefinitionHandle<TInput>
{
    internal DurableDefinitionHandle();
    public DefinitionId DefinitionId { get; }
    public DefinitionVersion DefinitionVersion { get; }
    public DefinitionFingerprint DefinitionFingerprint { get; }
    public ValueTask<WorkflowStartResult<WorkflowInstanceHandle>> StartOrGetAsync(
        TInput input,
        StartIdempotencyKey idempotencyKey,
        CancellationToken cancellationToken = default);
    public ValueTask<WorkflowInstanceHandle> GetInstanceAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken = default);
}

public sealed class DurableDefinitionHandle<TInput, TOutput>
{
    internal DurableDefinitionHandle();
    public DefinitionId DefinitionId { get; }
    public DefinitionVersion DefinitionVersion { get; }
    public DefinitionFingerprint DefinitionFingerprint { get; }
    public ValueTask<WorkflowStartResult<WorkflowInstanceHandle<TOutput>>> StartOrGetAsync(
        TInput input,
        StartIdempotencyKey idempotencyKey,
        CancellationToken cancellationToken = default);
    public ValueTask<WorkflowInstanceHandle<TOutput>> GetInstanceAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken = default);
}

public sealed record ActiveWaitSnapshot(
    WaitId WaitId,
    AuthoredLocation AuthoredLocation,
    WorkflowEventContract EventContract,
    CorrelationId CorrelationId,
    DateTimeOffset RegisteredAt,
    DateTimeOffset? Deadline);

public sealed record WorkflowInstanceSnapshot(
    InstanceId InstanceId,
    WorkflowMode Mode,
    DefinitionId DefinitionId,
    DefinitionVersion DefinitionVersion,
    DefinitionFingerprint DefinitionFingerprint,
    WorkflowInstanceStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    WorkflowOutcomeName? Outcome,
    WorkflowFailure? Failure,
    IReadOnlyList<ActiveWaitSnapshot> ActiveWaits);

public abstract record WorkflowOutputResult<TOutput>
{
    private protected WorkflowOutputResult();
    public sealed record Pending : WorkflowOutputResult<TOutput>;
    public sealed record Available(TOutput Output) : WorkflowOutputResult<TOutput>;
    public sealed record Unavailable(
        WorkflowInstanceStatus Status,
        WorkflowFailure? Failure) : WorkflowOutputResult<TOutput>;
}

public sealed class WorkflowOutputUnavailableException : OrcaCoreException
{
    internal WorkflowOutputUnavailableException(
        WorkflowInstanceStatus status,
        WorkflowFailure? failure);
    public WorkflowInstanceStatus Status { get; }
    public WorkflowFailure? Failure { get; }
}

public enum WorkflowCancellationRequestStatus
{
    Requested,
    AlreadyRequested,
    AlreadyTerminal
}

public enum WorkflowTerminationStatus
{
    Terminated,
    AlreadyTerminal
}

public class WorkflowInstanceHandle
{
    internal WorkflowInstanceHandle(InstanceId instanceId);
    public InstanceId InstanceId { get; }
    public ValueTask<WorkflowInstanceSnapshot> GetSnapshotAsync(
        CancellationToken cancellationToken = default);
    public ValueTask<TState> GetStateAsync<TState>(
        CancellationToken cancellationToken = default);
    public ValueTask<WorkflowCancellationRequestStatus> RequestCancellationAsync(
        CancellationToken cancellationToken = default);
    public ValueTask<WorkflowTerminationStatus> TerminateAsync(
        CancellationToken cancellationToken = default);
}

public sealed class WorkflowInstanceHandle<TOutput> : WorkflowInstanceHandle
{
    internal WorkflowInstanceHandle(InstanceId instanceId);
    public ValueTask<WorkflowOutputResult<TOutput>> GetOutputAsync(
        CancellationToken cancellationToken = default);

    public ValueTask<TOutput> WaitForOutputAsync(
        CancellationToken cancellationToken = default);
}

public static class WorkflowStartResultExtensions
{
    public static ValueTask<TOutput> WaitForOutputAsync<TOutput>(
        this WorkflowStartResult<WorkflowInstanceHandle<TOutput>> start,
        CancellationToken cancellationToken = default);
}

public class WorkflowEventContract : IEquatable<WorkflowEventContract>
{
    private protected WorkflowEventContract(
        EventName eventName,
        EventContractVersion version);
    public EventName EventName { get; }
    public EventContractVersion Version { get; }
    public static WorkflowEventContract Create(
        EventName eventName,
        EventContractVersion version);
}

public sealed class WorkflowEventContract<TPayload> : WorkflowEventContract
{
    private WorkflowEventContract(
        EventName eventName,
        EventContractVersion version);
    public static new WorkflowEventContract<TPayload> Create(
        EventName eventName,
        EventContractVersion version);
}

public abstract record WorkflowEventRoute
{
    private protected WorkflowEventRoute();
    public sealed record Direct(InstanceId InstanceId) : WorkflowEventRoute;
    public sealed record Correlation(DefinitionId DefinitionId) : WorkflowEventRoute;
    public sealed record DefinitionFanout(DefinitionId DefinitionId) : WorkflowEventRoute;
    public sealed record StartOrDeliver<TInput>(
        DefinitionId DefinitionId,
        DefinitionVersion DefinitionVersion,
        StartIdempotencyKey StartIdempotencyKey,
        TInput WorkflowInput) : WorkflowEventRoute;
}

public class WorkflowInboundEvent
{
    private protected WorkflowInboundEvent(
        WorkflowEventContract eventContract,
        EventId eventId,
        CorrelationId correlationId,
        EventId? causationEventId,
        DateTimeOffset occurredAt,
        WorkflowEventRoute route);
    public WorkflowEventContract EventContract { get; }
    public EventId EventId { get; }
    public CorrelationId CorrelationId { get; }
    public EventId? CausationEventId { get; }
    public DateTimeOffset OccurredAt { get; }
    public WorkflowEventRoute Route { get; }
    public static WorkflowInboundEvent Create(
        WorkflowEventContract eventContract,
        EventId eventId,
        CorrelationId correlationId,
        EventId? causationEventId,
        DateTimeOffset occurredAt,
        WorkflowEventRoute route);
}

public sealed class WorkflowInboundEvent<TPayload> : WorkflowInboundEvent
{
    private WorkflowInboundEvent(
        WorkflowEventContract<TPayload> eventContract,
        EventId eventId,
        CorrelationId correlationId,
        EventId? causationEventId,
        DateTimeOffset occurredAt,
        WorkflowEventRoute route,
        TPayload payload);
    public new WorkflowEventContract<TPayload> EventContract { get; }
    public TPayload Payload { get; }
    public static WorkflowInboundEvent<TPayload> Create(
        WorkflowEventContract<TPayload> eventContract,
        EventId eventId,
        CorrelationId correlationId,
        EventId? causationEventId,
        DateTimeOffset occurredAt,
        WorkflowEventRoute route,
        TPayload payload);
}

public abstract record WorkflowEventAcceptanceRejection
{
    private protected WorkflowEventAcceptanceRejection();
    public sealed record EventConflict : WorkflowEventAcceptanceRejection;
    public sealed record DirectInstanceNotFound : WorkflowEventAcceptanceRejection;
    public sealed record DirectInstanceTerminal : WorkflowEventAcceptanceRejection;
    public sealed record StartConflict(
        StartIdempotencyConflict Conflict) : WorkflowEventAcceptanceRejection;
    public sealed record FanoutLimitExceeded : WorkflowEventAcceptanceRejection;
}

public abstract record WorkflowEventAcceptanceResult
{
    private protected WorkflowEventAcceptanceResult();
    public sealed record Accepted : WorkflowEventAcceptanceResult;
    public sealed record Duplicate : WorkflowEventAcceptanceResult;
    public sealed record Rejected(
        WorkflowEventAcceptanceRejection Reason) : WorkflowEventAcceptanceResult;
}

public sealed class WorkflowOutboundEvent
{
    internal WorkflowOutboundEvent(
        WorkflowEventContract eventContract,
        EventId eventId,
        CorrelationId correlationId,
        EventId? causationEventId,
        DateTimeOffset occurredAt,
        InstanceId originInstanceId,
        DefinitionId originDefinitionId,
        DefinitionVersion originDefinitionVersion,
        ReadOnlyMemory<byte> payload);
    public WorkflowEventContract EventContract { get; }
    public EventId EventId { get; }
    public CorrelationId CorrelationId { get; }
    public EventId? CausationEventId { get; }
    public DateTimeOffset OccurredAt { get; }
    public InstanceId OriginInstanceId { get; }
    public DefinitionId OriginDefinitionId { get; }
    public DefinitionVersion OriginDefinitionVersion { get; }
    public TPayload GetPayload<TPayload>(WorkflowEventContract<TPayload> eventContract);
}

public sealed class WorkflowEventDispatchFailure
{
    private WorkflowEventDispatchFailure(string code, string? detail);
    public string Code { get; }
    public string? Detail { get; }
    public static WorkflowEventDispatchFailure Create(string code, string? detail = null);
}

public abstract record WorkflowEventDispatchResult
{
    private protected WorkflowEventDispatchResult();
    public sealed record Succeeded : WorkflowEventDispatchResult;
    public sealed record RetryableFailure(
        WorkflowEventDispatchFailure Failure) : WorkflowEventDispatchResult;
    public sealed record PermanentFailure(
        WorkflowEventDispatchFailure Failure) : WorkflowEventDispatchResult;
}
```

The durable hosting interfaces are owned by `OrcaCore.Durable.Hosting`:

```csharp
namespace OrcaCore.Durable.Hosting;

public interface IWorkflowEventIngress
{
    ValueTask<WorkflowEventAcceptanceResult> AcceptAsync(
        WorkflowInboundEvent inboundEvent,
        CancellationToken cancellationToken = default);
    ValueTask<WorkflowEventAcceptanceResult> AcceptAsync<TPayload>(
        WorkflowInboundEvent<TPayload> inboundEvent,
        CancellationToken cancellationToken = default);
}

public interface IWorkflowEventDispatcher
{
    ValueTask<WorkflowEventDispatchResult> DispatchAsync(
        WorkflowOutboundEvent outboundEvent,
        CancellationToken cancellationToken = default);
}
```

The four definition and definition-reference types expose `Mode`, `DefinitionId`,
`DefinitionVersion`, and `DefinitionFingerprint`; resultful types additionally expose their output
type through the generic contract. They expose immutable authored metadata but no executable plan.
Registration is explicit per host. Before registry mutation or fingerprint comparison, it
validates compatibility with the
selected host. A mode mismatch returns `HostIncompatible` carrying `EngineModeMismatch` and does
not inspect pool references. A matching ephemeral definition returns `HostIncompatible` carrying
`MissingTransientPools` when any authored transient pool is absent; a matching durable definition
returns `HostIncompatible` carrying `MissingDurableResourcePools` for
any statically authored lease request that references an absent durable pool. Missing-name lists
are non-empty immutable distinct values sorted by ordinal name. Selector-produced durable requests
are validated at runtime as specified in 17.2.5. A compatibility failure performs no registry or
provider mutation. `GetHandleOrThrow()` on `HostIncompatible` throws
`WorkflowDefinitionHostCompatibilityException` carrying the unchanged failure value.
Once compatibility succeeds, repeat registration returns the same typed handle; the same
identity/version with another structural fingerprint returns `Conflict`. Opaque code changes are
detected by the required version bump, not by pretending to hash delegate code.

`StartOrGetAsync` binds definition identity, version, structural fingerprint, and fixed-codec input
bytes to the key. Compatible reuse returns `Accepted` with `WasExisting = true`; incompatible
reuse returns the typed conflict and does not start anything. The closed registration/start unions
remain the inspection surface for conflict data and `WasExisting`. `GetHandleOrThrow()` returns the
successful handle or throws `WorkflowDefinitionHostCompatibilityException`,
`WorkflowDefinitionRegistrationConflictException`, or `WorkflowStartIdempotencyConflictException`
carrying the same immutable failure/conflict value; it does not
replace the union. Handle constructors are internal and
abstract result bases are closed to external derivation; constructing a public immutable snapshot
or result variant grants no runtime authority. Query values are fixed-codec detached.
`GetInstanceAsync` reopens a handle only when the instance belongs to that definition and typed
contract; absence and mismatch use `WorkflowInstanceNotFoundException` and
`WorkflowInstanceDefinitionMismatchException` rather than an untyped null.
`GetStateAsync<TState>` returns only the last-committed root state and fails with a typed state-type
mismatch; it never exposes private branch/item or in-flight attempt copies. `GetOutputAsync` remains
a nonblocking pending/available/unavailable snapshot query. `WaitForOutputAsync` uses a
notification-driven subscribe-then-recheck protocol around the committed completion boundary,
returns the detached successful output, and throws `WorkflowOutputUnavailableException` carrying
terminal status/failure when no output can exist. It never polls. Caller cancellation cancels only
the local wait, not the workflow. The start-result extension first calls `GetHandleOrThrow()`, so the
ordinary consumer path is `var output = await start.WaitForOutputAsync(token);`. Resultless handles
have no output member. V1 exposes no workflow-instance enumeration, list/count/statistics, or bulk
retrieval facade; runtime-owned keyed routing and internal continuation processing do not create
such an application API.

The ordinary `WorkflowInstanceSnapshot` intentionally exposes terminal workflow timeout through
`Status = TimedOut` plus `Failure`, and exposes an active structural wait's optional deadline only
inside `ActiveWaitSnapshot`. It does not expose the in-flight workflow deadline, current
retry-policy attempt coordinate, attempt deadline/outcome, or remaining attempt budget. Those are
advanced runtime diagnostic/telemetry facts, not additional v1 application-management fields;
their absence from this exact snapshot is normative.

Durable ingress compares global `EventId` plus the complete normalized envelope fingerprint before
current route or target state. Identical redelivery returns `Duplicate` even after progression or
terminalization; changed bytes return `Rejected(EventConflict)` without overwriting ownership.
Only `Accepted` and `Duplicate` mean the complete envelope/route intent is durably owned and safe
for upstream acknowledgement. Infrastructure, serialization, and cancellation failures remain
exceptional. A new direct event for an absent or terminal target, an incompatible start binding,
or an atomically unrepresentable fanout snapshot returns the exact rejection and commits no event
ownership or partial target set.

Direct events are stored in the target inbox before a wait exists. Correlation events without one
unique active wait are stored in a route-level inbox keyed by exact definition, contract, and
correlation. Acceptance, wait registration, claim, timeout, cancellation, and consumption serialize
so the oldest eligible accepted event is consumed once or remains pending. No pending-event TTL or
`NoActiveWait` result silently discards ownership. Correlation wait registration remains unique by
`(DefinitionId, WorkflowEventContract, CorrelationId)` and rejects ambiguity before parking.

Definition fanout atomically snapshots all current nonterminal persisted instances for the
`DefinitionId` across versions from provider state, commits independently deduplicated target
deliveries, accepts an empty set, excludes later instances, and reuses membership on redelivery.
Start-or-deliver binds `StartIdempotencyKey` to exact definition/version and normalized workflow
input distinct from event payload, then atomically owns the pending start intent and event. A
callback-only ingress host persists the same ownership/handoff without loading a definition;
definition-owning pumps materialize or rehydrate exact bound work. Post-acceptance semantic failures
become observable poison rather than retroactive broker redelivery.

Event/correlation pairs have signal-stream semantics. After one wait consumes an event, a later
loop iteration may register the same pair and a later-arriving event may satisfy it. OrcaCore does
not claim that an envelope without a wait-occurrence identity can distinguish iterations; authors
that require occurrence-specific matching include that occurrence in `CorrelationId`. Delivery
contains no raw command, fiber, scope, provider-generation, checkpoint, or wait-sequence identity.
Ephemeral hosting provides only process-local descriptor matching and no durable acceptance or
broker-acknowledgement contract. Durable engine or callback-ingress hosting owns
`IWorkflowEventIngress`; mixed ephemeral/durable roles remain rejected.

Each durable `Publish` node records its explicit contract and position in the structural
fingerprint. Runtime-owned replay-stable event, causation, correlation, origin, and occurrence-time
metadata plus fixed-codec payload commit atomically with workflow progression. The external outbox
pump invokes only `IWorkflowEventDispatcher.DispatchAsync(WorkflowOutboundEvent, ...)`. Success
marks dispatch; retryable failure or exception retains retry; cancellation releases the claim;
permanent failure records observable poison. An ambiguous broker send may repeat the same outbound
`EventId`; internal continuation records never reach the application dispatcher. A definition that
contains `Publish` is host-incompatible when no dispatcher is registered.

`Completed`, `Failed`, `TimedOut`, `Cancelled`, and `Terminated` are immutable terminal statuses;
v1 never reopens them. `RequestCancellationAsync` is cooperative and idempotent. `TerminateAsync`
fences progression immediately but applies the same definite-cleanup/quarantine rules as a
deadline; it is not proof that external work stopped. Failed-instance retry is deferred rather
than smuggled in as a terminal-state transition.
Completion, failure, deadline, cancellation, and termination race through one serialized mutation
lane; the first committed terminal status wins and every loser is a no-op/`AlreadyTerminal` result.
