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

The same `OrcaCore.Dag -> OrcaCore.Dag.Hosting` friend SHALL additionally carry a separately named facade-handle grant limited to exactly two internal constructors, one on the public `DagDefinitionHandle<TRunInput>` and one on the public `DagRunHandle`, with the exact signatures below. Every parameter SHALL be a public type or a delegate over public types; the grant SHALL add no non-public type reference, so the non-public DAG type references of `OrcaCore.Dag.Hosting` SHALL remain exactly the three runtime-view types and its non-public DAG member references SHALL be exactly the fifteen runtime-view signatures plus these two constructors. No other constructor, setter, field, factory, interface, or overload SHALL be reachable through the grant. `OrcaCore.Dag` SHALL own, inside the handles, typed union construction for node output, rejection of a node reference from another plan by reference identity before any runtime query, the check that a supplied output matches the declared `TOutput` under the existing null rule, and the `DagRunNotFoundException` and `DagRunDefinitionMismatchException` throws; `OrcaCore.Dag.Hosting` SHALL NOT construct those exceptions and SHALL signal absence or mismatch only through the result of the reopen delegate. A null result from the terminal-wait or cancellation delegate SHALL mean that the run is in the internal integrity-fault state, and the run handle SHALL translate it into the public `DagRunFaultedException`; the snapshot delegate SHALL keep reporting the last committed public status. `DefinitionRegistrationConflict`, `StartIdempotencyConflict`, `WorkflowFailure`, and `PayloadFingerprint` values SHALL be created on the durable side of the existing `OrcaCore.Durable.Hosting -> OrcaCore.Dag.Hosting` bridge and SHALL cross it as opaque public values; the six-signature `OrcaCore -> OrcaCore.Dag` authoring allowlist SHALL NOT be widened, `OrcaCore.Dag.Hosting` SHALL NOT become a friend of `OrcaCore`, and `OrcaCore.Dag` SHALL gain no codec, runtime-protocol, provider, or child-management access. `IDagDefinitionRegistry` SHALL be implemented by `OrcaCore.Dag.Hosting` through its public interface with no friend access, and every snapshot, result-union, and status value SHALL be constructed through its public constructor. The handle delegates SHALL carry no authority beyond the run or definition they were created for, and constructing an equal public data value SHALL confer no management authority. A handle SHALL be valid only within the host process that created it; another DAG-role host SHALL reopen the run with `GetRunAsync`.

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
            DefinitionFingerprint? ActualDefinitionFingerprint)>> getRun);
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

#### Scenario: Output is requested with a node from another plan
- **WHEN** `GetOutputAsync` receives a node reference that is not one of the plan nodes of that run
- **THEN** the run handle rejects it by reference identity before invoking any runtime delegate

#### Scenario: Conflict and failure values reach the application
- **WHEN** registration, start, or a snapshot reports a registration conflict, start-idempotency conflict, node failure, or mapped-input fingerprint
- **THEN** that value was created on the durable side of the existing bridge and passed through unchanged, and neither DAG package gained a new application-contract friend or constructor grant

#### Scenario: DAG host references another handle internal
- **WHEN** compiled `OrcaCore.Dag.Hosting.dll` references any non-public member of a facade handle other than its one allowlisted constructor
- **THEN** metadata verification fails even though the CLR friend grant permits the reference
