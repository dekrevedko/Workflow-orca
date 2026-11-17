## MODIFIED Requirements

### Requirement: Implementation package boundaries use exact internal friends
Compiler, execution-kernel, concrete engine, provider, and hosted-loop implementation types SHALL remain internal even when another first-release implementation package consumes them. Cross-assembly implementation access SHALL use only the exact reviewed product friends `OrcaCore` to `OrcaCore.Core`, `OrcaCore.Engine.Ephemeral`, `OrcaCore.Engine.Durable`, and `OrcaCore.Dag` for application-owned internal authoring/runtime contracts; `OrcaCore.Core` to `OrcaCore.Engine.Ephemeral` and `OrcaCore.Engine.Durable`; `OrcaCore.Engine.Durable` to `OrcaCore.Durable.Hosting`; `OrcaCore.Durable.Hosting` to `OrcaCore.Dag.Hosting`; and `OrcaCore.Dag` to `OrcaCore.Dag.Hosting` for the internal runtime view. The `OrcaCore -> OrcaCore.Dag` grant SHALL be used only for the internal constructors of `Validation<T>`, `WorkflowDiagnostic`, `AuthoredLocation`, `DefinitionFingerprint`, and `WorkflowDefinitionException`, plus one internal canonical-structure hash operation owned by `DefinitionFingerprint`; the exact signatures SHALL be pinned. A compiled-metadata guard SHALL reject every other non-public `OrcaCore` type or member referenced by `OrcaCore.Dag.dll`, including runtime, child-start, engine, hosting, and protocol internals. DAG diagnostics SHALL use the existing `WorkflowDiagnosticCatalog` and `Validation<T>` ordering, and DAG fingerprints SHALL use the same internal UTF-8/SHA-256 operation as workflow compilation without changing existing workflow hashes. The `OrcaCore -> OrcaCore.Dag` authoring grant SHALL NOT add a package reference or give `OrcaCore.Dag` durable child-start access; `OrcaCore.Durable.Hosting -> OrcaCore.Dag.Hosting` remains the sole DAG-to-durable runtime bridge. Exact owning white-box test friends and the durable provider-certification barrier friend MAY inspect internals; acceptance, behavior-scenario, compile-fixture, and integration assemblies SHALL NOT. No other friend, public reflection bridge, or exported test helper SHALL exist. Friend grants SHALL NOT create reverse package references.

The `OrcaCore.Dag -> OrcaCore.Dag.Hosting` runtime-view grant SHALL expose only `WorkflowDagPlan<TRunInput>.GetRuntimeView()`, `DagRuntimeView<TRunInput>.Nodes`, `DagRuntimeView<TRunInput>.EvaluateMapping(DagNodeRef, TRunInput, IReadOnlyDictionary<DagNodeRef, object?>)`, and only the twelve descriptor/result getters enumerated below. Its only non-public DAG type references SHALL be `DagRuntimeView<TRunInput>`, `DagRuntimeNodeDescriptor`, and `DagMappedInputResult`; constructors, setters, draft types, raw node plans, mapper delegates, and every other non-public DAG type/member/overload SHALL be forbidden, except the two facade-handle constructors named below. The durable bridge SHALL decode successful committed dependency outputs using their declared output types before the evaluator receives them; Dag SHALL NOT decode bytes. The evaluator SHALL accept runtime-supplied immutable run input and successful direct-dependency outputs, enforce the existing `OutputOf` access rules, and return the typed mapped input with its declared type or the stable `DAG_INPUT_MAPPING_INVALID` failure before input commit or child start. A successful null output SHALL be distinguished from a missing output by presence in the successful-output map and SHALL be valid only for a reference or nullable-value declared type; successful null mapped input SHALL follow the same type rule. C# nullable-reference annotations SHALL NOT be treated as a runtime discriminator. The adapter SHALL NOT receive mapping delegates or mutable authoring drafts. A compiled-metadata guard SHALL inspect `OrcaCore.Dag.Hosting.dll` and reject every non-public `OrcaCore.Dag` type or member reference outside the exact runtime-view signatures and the two facade-handle constructors, including type references in signatures, base types, implemented interfaces, generic arguments, and attributes. Runtime-view behavior SHALL be exercised through Dag.Hosting-level behavior without a new test friend. Fixed-codec normalization, dependency-output materialization, committed-byte fingerprinting, persistence, and child-start/join SHALL remain owned by the durable runtime and its existing hosting bridge; the six-signature `OrcaCore -> OrcaCore.Dag` authoring allowlist SHALL NOT gain codec or runtime access. The runtime view SHALL add no public surface, package edge, reflection bridge, or additional friend beyond the named `OrcaCore.Dag -> OrcaCore.Dag.Hosting` proposal; the facade-handle grant below reuses that same friend edge.

The exhaustive proposed signature contract SHALL be exactly the following, with no additional type or member access:

```csharp
// On the existing public WorkflowDagPlan<TRunInput>:
internal DagRuntimeView<TRunInput> GetRuntimeView();

internal sealed class DagRuntimeView<TRunInput>
{
    internal IReadOnlyList<DagRuntimeNodeDescriptor> Nodes { get; }
    internal DagMappedInputResult EvaluateMapping(
        DagNodeRef node,
        TRunInput immutableRunInput,
        IReadOnlyDictionary<DagNodeRef, object?> successfulDirectDependencyOutputs);
}

internal sealed class DagRuntimeNodeDescriptor
{
    internal DagNodeRef Reference { get; }
    internal int AuthoredOrdinal { get; }
    internal DefinitionId ChildDefinitionId { get; }
    internal DefinitionVersion ChildDefinitionVersion { get; }
    internal DefinitionFingerprint ChildFingerprint { get; }
    internal Type InputType { get; }
    internal Type? OutputType { get; }
    internal IReadOnlyList<DagNodeRef> Dependencies { get; }
}

internal sealed class DagMappedInputResult
{
    internal bool IsValid { get; }
    internal object? Input { get; }
    internal Type? InputType { get; }
    internal string? FailureCode { get; }
}
```

Mapper exceptions derived from `Exception`, including mapper-thrown `OperationCanceledException`, SHALL become `DAG_INPUT_MAPPING_INVALID`, except `OutOfMemoryException`, `StackOverflowException`, and `AccessViolationException`, which SHALL NOT be converted. Host/run cancellation observed outside mapper evaluation SHALL retain the runtime cancellation outcome. Before invoking the evaluator, an ordinary declared-type output decode/materialization failure SHALL fail the node as `DAG_INPUT_MAPPING_INVALID`, without invoking its mapper, committing input, or starting a child; protocol-integrity or storage failures SHALL remain runtime failures and SHALL NOT be disguised as mapper failures.

The same `OrcaCore.Dag -> OrcaCore.Dag.Hosting` friend SHALL additionally carry a separately named facade-handle grant limited to exactly two internal constructors, one on the public `DagDefinitionHandle<TRunInput>` and one on the public `DagRunHandle`, with the exact signatures below. Every parameter SHALL be a public type or a delegate over public types; the grant SHALL add no non-public type reference, so the non-public DAG type references of `OrcaCore.Dag.Hosting` SHALL remain exactly the three runtime-view types and its non-public DAG member references SHALL be exactly the fifteen runtime-view signatures plus these two constructors. No other constructor, setter, field, factory, interface, or overload SHALL be reachable through the grant.

`OrcaCore.Dag` SHALL own, inside the handles and result unions, typed union construction for node output, rejection of a node reference from another plan by reference identity before any runtime query, the check that a supplied output matches the declared `TOutput` under the existing null rule, and every throw of `DagRunNotFoundException`, `DagRunDefinitionMismatchException`, `DagRunInputContractMismatchException`, and `DagRunFaultedException`. `OrcaCore.Dag.Hosting` SHALL NOT construct a DAG exception and SHALL signal those outcomes only through delegate results: an absent run and a run bound to another definition identity, version, or fingerprint through the reopen result; a reopened run bound to this exact definition under another declared run-input contract through the reopen flag, which SHALL be used for reopen only; and the integrity-fault state through a null terminal-wait or cancellation result. Every comparison of a start with an existing start-key owner SHALL be a closed `DagStartResult` variant that `OrcaCore.Dag.Hosting` constructs publicly, and a start whose key owner has an equal definition triple and a different run-input contract key SHALL be reported as `DagStartResult.RunInputConflict`, never through the reopen flag. `OrcaCore.Dag.Hosting` SHALL project public snapshots and status values from committed run facts supplied by the existing `OrcaCore.Durable.Hosting -> OrcaCore.Dag.Hosting` bridge; every snapshot and result-union value SHALL be constructed through its public constructor, and status values SHALL be public enumeration members. `DefinitionRegistrationConflict`, `StartIdempotencyConflict`, `WorkflowFailure`, and `PayloadFingerprint` values SHALL be created by `OrcaCore.Engine.Durable` and SHALL cross that bridge as opaque public values; the six-signature `OrcaCore -> OrcaCore.Dag` authoring allowlist SHALL NOT be widened, `OrcaCore.Dag.Hosting` SHALL NOT become a friend of `OrcaCore`, and `OrcaCore.Dag` SHALL gain no codec, runtime-protocol, provider, or child-management access. `IDagDefinitionRegistry` SHALL be implemented by `OrcaCore.Dag.Hosting` through its public interface with no friend access. The handle delegates SHALL carry no authority beyond the run or definition they were created for, and constructing an equal public data value SHALL confer no management authority. A handle SHALL be valid only within the host process that created it; another DAG-role host SHALL reopen the run with `GetRunAsync`.

A DAG definition binding SHALL be its definition identity, version, structural fingerprint, and declared run-input contract; the structural fingerprint SHALL NOT be assumed to cover `TRunInput`. The durable runtime SHALL own the run-input contract key, derived from the fixed-codec shape of the declared run-input type, and `OrcaCore.Dag` SHALL NOT compute it. Two comparisons SHALL be distinguished. Within one host process, two registrations have the same run-input contract only when their declared run-input types are the same CLR `Type` and their keys are equal; a different `Type` SHALL conflict even when its key is equal. Against a stored run, whose declaring type is not knowable, the installed plan matches the stored run-input contract only when the keys are equal and the durable runtime has verified that the stored run-input bytes decode with the installed declared type and re-encode through the fixed codec to exactly the stored bytes; an equal key SHALL NOT by itself establish compatibility. That verification SHALL complete before a host issues a run handle and before it evaluates any mapper for the run. A well-formed stored payload that fails the verification under the installed binding SHALL make the run locally unavailable exactly as a different key does, and SHALL NOT be recorded as a mapping failure, node failure, or integrity fault. Two key comparisons SHALL be kept apart. The contract key recorded on a stored run-input value SHALL equal the run-input contract key of its own stored graph; a disagreement between those two, like invalid framing, codec format, or fingerprint of a checkpoint or value, is corrupt storage, SHALL keep its existing integrity classification, and SHALL NOT be reclassified as a contract mismatch. A valid stored graph key that differs from the run-input contract key of the installed plan is a binding mismatch. The same integrity check SHALL apply to a start-key owner examined during a replay. Storage failures, integrity failures, and caller cancellation SHALL propagate as exceptions from registration, start, and reopen alongside the closed comparison results, which SHALL NOT absorb them.

Before creating a run or accepting a replay, the durable runtime SHALL encode the submitted run input with the declared run-input type, decode those bytes, and require byte-identical re-encoding. A submitted value that fails SHALL be rejected with `NotSupportedException` before any run is committed and before any start key is bound. This rule and the stored-run verification SHALL be a guarantee about the stored representation only: a change in what the values mean SHALL still require a new definition version. The rule SHALL apply to the DAG run input only; mapped node inputs, child outputs, and ordinary workflow inputs SHALL keep their existing commit paths and failure outcomes.

On one host, registering an identity and version again with an equal fingerprint and the same run-input contract SHALL return `Registered`; a different fingerprint SHALL return `Conflict` carrying `DefinitionRegistrationConflict` and SHALL take precedence; an equal fingerprint with another declared run-input type SHALL return the closed `RunInputConflict` variant and SHALL leave the earlier registration unchanged. `GetRunAsync` SHALL reopen only a run whose stored binding matches all four parts, SHALL report another identity, version, or fingerprint as `DagRunDefinitionMismatchException` first, and SHALL report an equal triple under another run-input contract as `DagRunInputContractMismatchException` carrying the run id. `StartOrGetAsync` SHALL resolve the authoritative owner of the start key, both on retry and after a competing create, and SHALL return `Accepted` with the run id of that owner and `WasExisting = true` only when the definition triple, the run-input contract key, and the normalized run-input bytes are all equal; an equal triple with another run-input contract key SHALL return the closed `DagStartResult.RunInputConflict` variant carrying the run id of that owner; and every other difference SHALL return `Conflict` carrying `StartIdempotencyConflict`, including an equal contract key with different validated run-input bytes. No comparison with an intact existing key owner SHALL be reported by exception from `StartOrGetAsync`; apart from storage, integrity, and cancellation exceptions, only a submitted value that fails the round-trip rule SHALL be rejected by exception. A host SHALL map and admit nodes of a discovered run only when its installed plan matches all four parts of the stored binding. For any other run, which is locally unavailable, that host SHALL skip new mapping and admission without mutation, mapping failure, or integrity fault, while role-independent child start, join, and cancellation work already committed for the run SHALL still be processed by any durable host.

The exhaustive facade-handle signature contract SHALL be exactly the following, with no additional non-public type or member access:

```csharp
// Facade-handle grant: the only two non-public constructors Dag.Hosting may call.
public sealed class DagDefinitionHandle<TRunInput>
{
    internal DagDefinitionHandle(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        DefinitionFingerprint definitionFingerprint,
        Func<TRunInput, StartIdempotencyKey, CancellationToken, ValueTask<DagStartResult>> startOrGet,
        Func<DagRunId, CancellationToken, ValueTask<(
            DagRunHandle? Run,
            DefinitionId? ActualDefinitionId,
            DefinitionVersion? ActualDefinitionVersion,
            DefinitionFingerprint? ActualDefinitionFingerprint,
            bool RunInputContractDiffers)>> getRun);
}

public sealed class DagRunHandle
{
    internal DagRunHandle(
        DagRunId runId,
        IReadOnlyList<DagNodeRef> planNodes,
        Func<CancellationToken, ValueTask<DagRunSnapshot>> getSnapshot,
        Func<DagNodeRef, CancellationToken, ValueTask<(
            DagNodeStatus Status,
            bool HasOutput,
            object? Output,
            WorkflowFailure? Failure)>> getOutput,
        // null from either delegate: the run is in the integrity-fault state
        Func<CancellationToken, ValueTask<DagRunSnapshot?>> waitForTerminal,
        Func<CancellationToken, ValueTask<DagCancellationRequestStatus?>> requestCancellation);
}
```

#### Scenario: Engine consumes the shared execution kernel
- **WHEN** either engine compiles against `OrcaCore.Core`
- **THEN** it receives type-safe friend access to internal kernel types without making those types externally visible

#### Scenario: Internal application contracts cross implementation packages
- **WHEN** Core or an engine constructs or reads an application-owned internal authoring/runtime contract
- **THEN** it uses compile-checked `OrcaCore` friend access and no reflection, CLR-name lookup, or public compatibility bridge

#### Scenario: Consumer inspects an implementation package
- **WHEN** a fresh-package consumer or exported-API baseline inspects Core, an engine, hosting, or a provider package
- **THEN** only the documented role-specific surface is exported and no compiler IR, concrete loop/store, observer hook, test helper, or reflection bridge is visible

#### Scenario: New friend assembly is proposed
- **WHEN** compiled metadata contains a friend outside the exact product, owning-test, and provider-certification allowlist
- **THEN** the guard fails until a reviewed contract amendment authorizes that boundary

#### Scenario: DAG author constructs compiler-owned values
- **WHEN** `OrcaCore.Dag` builds a typed plan or returns ordered build diagnostics
- **THEN** it constructs only the six allowlisted internal member signatures on the five named authoring value families, reuses the shared catalog, sorter, and canonical hash, and exposes no new public construction path

#### Scenario: DAG references another application internal
- **WHEN** compiled `OrcaCore.Dag.dll` references any other non-public `OrcaCore` type, member, or overload
- **THEN** the compiled-metadata guard fails even though the CLR friend grant would permit the reference

#### Scenario: DAG runtime evaluates one ready node
- **WHEN** the hosting adapter receives a validated plan and all declared direct dependencies have succeeded
- **THEN** its allowlisted runtime-view evaluator returns typed mapped input and its declared type, or DAG_INPUT_MAPPING_INVALID, without exposing delegates or committing bytes or starting a child

#### Scenario: DAG host references an unrelated authoring internal
- **WHEN** compiled OrcaCore.Dag.Hosting.dll refers to any non-public DAG type or member outside the exact runtime-view allowlist
- **THEN** metadata verification fails even when the CLR friend grant permits that reference

#### Scenario: Runtime normalizes node input
- **WHEN** Dag.Hosting submits valid typed mapped input to the existing durable bridge
- **THEN** the durable runtime alone normalizes fixed-codec bytes, fingerprints and commits them once before child start, and Dag gains no codec or runtime-protocol access

#### Scenario: Successful dependency output is null
- **WHEN** a successful committed direct resultful dependency has a null output of a reference or nullable-value declared type
- **THEN** the durable bridge supplies a present null entry, OutputOf returns that successful null, and a missing entry or nonnullable-value null still fails mapping before commit or child start

#### Scenario: DAG host creates public handles
- **WHEN** `OrcaCore.Dag.Hosting` returns a registered definition handle or an accepted, reopened, or replayed run handle
- **THEN** it calls only the two allowlisted handle constructors with delegates over public types, constructs every result and snapshot value through public constructors, and references no other non-public DAG member

#### Scenario: Run reopen finds no matching run
- **WHEN** the reopen delegate reports an absent run or a run bound to another definition identity, version, or fingerprint
- **THEN** the definition handle inside `OrcaCore.Dag` throws `DagRunNotFoundException` or `DagRunDefinitionMismatchException`, and the hosting assembly constructs neither exception

#### Scenario: Two hosts register equal structure with different run input
- **WHEN** a run was created by a plan whose identity, version, and structural fingerprint equal those of a plan registered on another host whose declared run-input type yields a different run-input contract key
- **THEN** that other host neither reopens nor progresses the run, `GetRunAsync` throws `DagRunInputContractMismatchException` carrying the run id, a start with the same key returns `DagStartResult.RunInputConflict` carrying the run id of the owner without creating a run because the definition triple is equal and the contract keys differ, and that host skips new mapping and admission for the run without mutation

#### Scenario: Start key is reused with an equal contract key and different input
- **WHEN** `StartOrGetAsync` submits a run input that passes the round-trip rule for a start key whose owner has an equal definition triple and an equal run-input contract key but different normalized run-input bytes
- **THEN** it returns `Conflict` carrying `StartIdempotencyConflict`, not `RunInputConflict`

#### Scenario: Start-key owner examined during replay is corrupt
- **WHEN** the owner of a start key has a stored run-input value whose recorded contract key disagrees with its own stored graph, or whose framing, codec format, or fingerprint is invalid
- **THEN** the existing integrity failure propagates and no closed start result is returned for that owner

#### Scenario: Run is cancelled through a compatible host while another host cannot map it
- **WHEN** a host with a compatible binding accepts `RequestCancellationAsync` for a run that another durable host treats as locally unavailable
- **THEN** the committed child-cancellation work may be consumed by the pump of any durable host, including the host that skips mapping and admission for that run

#### Scenario: One host registers the same definition with another run-input type
- **WHEN** a second plan with an equal identity, version, and structural fingerprint but another `TRunInput` is registered on a host
- **THEN** registration returns `RunInputConflict` carrying the existing and attempted types even when both types produce an equal contract key, the earlier registration and its handles are unchanged, and `GetHandleOrThrow()` throws `DagRunInputContractMismatchException`

#### Scenario: Redeployed run-input type keeps its key but rejects stored bytes
- **WHEN** a host installs a plan whose run-input type has the same contract key as a stored run but cannot decode the stored run-input bytes, or re-encodes them differently
- **THEN** the stored run is treated as bound to another run-input contract: reopen throws `DagRunInputContractMismatchException`, that host skips new mapping and admission, no mapping failure, node failure, or integrity fault is committed, a child already admitted can still be joined by its role-independent pump, and a host with a compatible binding can reopen and finish the run

#### Scenario: Submitted run input does not round-trip
- **WHEN** `StartOrGetAsync` receives a run input whose encoded bytes do not decode and re-encode to the same bytes under the declared run-input type
- **THEN** it throws `NotSupportedException` before any run is committed or any start key is bound, for a first start and for a replay alike

#### Scenario: Stored run input is corrupt
- **WHEN** the stored run-input value has invalid framing, codec format, or fingerprint, or records a contract key that disagrees with its own stored graph
- **THEN** the existing integrity classification applies and the run is not reported as bound to another run-input contract

#### Scenario: Start is replayed with the same key
- **WHEN** `StartOrGetAsync` is retried, or loses a competing create, for a start key whose owner has an equal definition triple, an equal run-input contract key, and equal normalized run-input bytes
- **THEN** it returns `Accepted` with the run id of that owner and `WasExisting = true`, and a caller-supplied run id that collides with another run remains a separate conflict

#### Scenario: Output is requested with a node from another plan
- **WHEN** `GetOutputAsync` receives a node reference that is not one of the plan nodes of that run
- **THEN** the run handle rejects it by reference identity before invoking any runtime delegate

#### Scenario: Conflict and failure values reach the application
- **WHEN** registration, start, or a snapshot reports a registration conflict, start-idempotency conflict, node failure, or mapped-input fingerprint
- **THEN** that value was created by the durable engine and passed through the existing bridge unchanged, and neither DAG package gained a new application-contract friend or constructor grant

#### Scenario: DAG host references another handle internal
- **WHEN** compiled `OrcaCore.Dag.Hosting.dll` references any non-public member of a facade handle other than its one allowlisted constructor
- **THEN** metadata verification fails even though the CLR friend grant permits the reference

### Requirement: Closed registration and start results retain a cast-free success path
Workflow registration SHALL return a closed `Registered`/`HostIncompatible`/`Conflict` result, while workflow start and DAG registration/start operations SHALL retain their closed accepted-or-conflict or registered-or-conflict results so callers can inspect failure values and `WasExisting`; the DAG registration and start results SHALL additionally carry the closed `RunInputConflict` variants defined below. `DefinitionHostCompatibilityFailure` SHALL be a closed union of `EngineModeMismatch(WorkflowMode HostMode, WorkflowMode DefinitionMode)`, `MissingTransientPools(IReadOnlyList<TransientPoolName>)`, `MissingDurableResourcePools(IReadOnlyList<ResourcePoolName>)`, and `MissingWorkflowEventDispatcher`; missing-name lists SHALL be copied, distinct, and ordinal-sorted. Registration SHALL validate mode first, statically inspectable host capabilities second, and fingerprint conflict third without mutation. `WorkflowRegistrationResult<TDefinitionHandle>` and `WorkflowStartResult<TInstanceHandle>` SHALL expose `GetHandleOrThrow()` returning the registered/accepted handle. The common registry SHALL additionally expose four exact typed `GetRequiredHandle` overloads for already registered ephemeral/durable resultless/resultful references; lookup SHALL never register and an absent or stale exact reference SHALL throw `WorkflowDefinitionNotRegisteredException` with code `WF-DEFINITION-NOT-REGISTERED` carrying the requested reference's `DefinitionId`, `DefinitionVersion`, and `DefinitionFingerprint` as immutable structured properties. Host incompatibility SHALL throw `WorkflowDefinitionHostCompatibilityException` with fixed code `WF-DEFINITION-HOST-INCOMPATIBLE` carrying the failure; conflict paths SHALL throw `WorkflowDefinitionRegistrationConflictException` carrying `DefinitionRegistrationConflict` and `WorkflowStartIdempotencyConflictException` carrying `StartIdempotencyConflict`, respectively. `DagRegistrationResult<TRunInput>` and `DagStartResult` SHALL expose the same-named success projection and SHALL throw `DagDefinitionRegistrationConflictException` or `DagStartIdempotencyConflictException` carrying the corresponding closed conflict value, or `DagRunInputContractMismatchException` for a `RunInputConflict` variant. The helpers SHALL NOT replace or hide the inspectable result unions.

`DagRegistrationResult<TRunInput>` SHALL be the closed union of `Registered(DagDefinitionHandle<TRunInput> Handle)`, `Conflict(DefinitionRegistrationConflict Error)`, and `RunInputConflict(DefinitionId DefinitionId, DefinitionVersion DefinitionVersion, DefinitionFingerprint DefinitionFingerprint, Type ExistingRunInputType, Type AttemptedRunInputType)`. `DagStartResult` SHALL be the closed union of `Accepted(DagRunHandle Handle, bool WasExisting)`, `Conflict(StartIdempotencyConflict Error)`, and `RunInputConflict(DagRunId ExistingRunId, DefinitionId DefinitionId, DefinitionVersion DefinitionVersion, DefinitionFingerprint DefinitionFingerprint, Type AttemptedRunInputType)`. A `RunInputConflict` SHALL report that the definition identity, version, and structural fingerprint are equal while the declared run-input contract differs, and SHALL be returned, never thrown, by `Register` and `StartOrGetAsync`; the start variant SHALL retain the run id of the run that owns the start key. `GetHandleOrThrow()` on either variant SHALL throw `DagRunInputContractMismatchException` with stable code `DAG-RUN-INPUT-CONTRACT-MISMATCH` carrying the definition identity, version, fingerprint, attempted run-input type, the existing run-input type when it is known on that host, and the existing run id when one exists.

#### Scenario: Caller wants ordinary success flow
- **WHEN** registration or start returns its success variant and the caller invokes `GetHandleOrThrow()`
- **THEN** the exact typed definition, instance, DAG-definition, or DAG-run handle is returned without a cast

#### Scenario: Caller wants conflict detail
- **WHEN** registration or start returns a conflict variant
- **THEN** the caller may inspect the union directly, while `GetHandleOrThrow()` throws the exact typed exception carrying that same immutable conflict value

#### Scenario: Workflow definition is incompatible with its host
- **WHEN** registration finds an engine-mode mismatch, missing statically inspectable transient/durable pools, or a durable `Publish` without an application dispatcher
- **THEN** it returns the copied closed `HostIncompatible` failure before registry mutation or fingerprint-conflict evaluation

#### Scenario: Registered reference is resolved
- **WHEN** a caller requests the handle for an exact typed reference already installed by application configuration
- **THEN** the matching typed handle is returned without registration, a cast, keyed DI, or a second workflow client surface

#### Scenario: Registered reference is absent or stale
- **WHEN** no installed definition matches the reference's exact mode, identity, version, fingerprint, and typed contract
- **THEN** lookup throws `WorkflowDefinitionNotRegisteredException` carrying the requested definition id, version, and fingerprint and starts or registers nothing

#### Scenario: DAG registration or start meets another run-input contract
- **WHEN** DAG registration or start finds an equal definition identity, version, and structural fingerprint under another declared run-input contract
- **THEN** it returns the closed `RunInputConflict` variant for inspection, the start variant carries the run id of the owner of the start key, and `GetHandleOrThrow()` throws `DagRunInputContractMismatchException` carrying the same values

### Requirement: Completion waits are notification-driven and race-free
`WorkflowInstanceHandle<TOutput>.WaitForOutputAsync(CancellationToken cancellationToken = default)` SHALL return `ValueTask<TOutput>` containing the fixed-codec-detached output after successful completion or throw `WorkflowOutputUnavailableException` carrying terminal status and failure when the instance terminalizes without output. A same-named `ValueTask<TOutput>` convenience extension on `WorkflowStartResult<WorkflowInstanceHandle<TOutput>>` SHALL project through `GetHandleOrThrow()`. `DagRunHandle.WaitForTerminalAsync(CancellationToken cancellationToken = default)` SHALL return `ValueTask<DagRunSnapshot>` containing the detached terminal snapshot, or throw `DagRunFaultedException` while the run is in the nonterminal integrity-fault state as the `management-and-querying` DAG terminal-wait requirement defines. Durable implementations SHALL use subscribe-then-recheck around the committed notification boundary so completion cannot be lost; these operations SHALL NOT poll. Caller cancellation SHALL cancel only the local wait and SHALL NOT cancel the workflow instance or DAG run.

Logical output/terminal completion SHALL remain separate from physical settlement.
The new WorkflowInstanceHandle.WaitForSettlementAsync(CancellationToken) SHALL be
available on resultless and inherited resultful handles and SHALL NOT change any
existing output wait, DAG wait, timeout or cancellation contract. Ephemeral output
waits SHALL expose an eligible original step exception only through the existing
wrapper's InnerException; WorkflowFailure.ExceptionType SHALL remain detached text.
Only the two exact members in this change's approved catalogue SHALL be added.
The normative declaration companion SHALL change in the reviewed canonical and
numbered-document transition. Executable public-API/compile/package baselines
SHALL change with actual source, never during a contract or canonical-only target.

#### Scenario: Completion races wait subscription
- **WHEN** output or DAG terminal state commits immediately before, during, or after wait subscription
- **THEN** the wait observes the committed result exactly once without a polling interval or lost-notification window

#### Scenario: Caller abandons a local wait
- **WHEN** the wait cancellation token is cancelled
- **THEN** only that caller's wait ends and runtime execution remains governed by its separate cancellation API

#### Scenario: A timed-out physical step keeps running
- **WHEN** output waiting reports TimedOut before the noncooperative body and its ownership handoffs finish
- **THEN** the independent settlement wait remains pending and existing output waiting does not silently become a physical join

#### Scenario: DAG wait observes a faulted run
- **WHEN** an authoritative recheck of the DAG wait, whether the first or one following a committed-change notification, observes the integrity-fault state
- **THEN** the wait ends with `DagRunFaultedException` without polling and without returning a snapshot that claims a terminal result, while a fault that was reconciled before the recheck is not reported and the wait continues
