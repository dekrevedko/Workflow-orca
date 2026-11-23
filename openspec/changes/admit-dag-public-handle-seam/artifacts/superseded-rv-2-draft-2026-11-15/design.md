## Context

Base: `da1a4729` (DAG Task 2.3 regression follow-up approved and recorded). The runtime-view amendment is Complete; reshape 8.3–8.5 and `admit-durable-dag-run-progression` 2.4/3.x are still open. Doc 17 §17.2.6 fixes the public DAG facade. The approved run contract (RC-3) delegates two things to Task 8.6: the public status projection, and the public cancellation trigger on a DAG-role handle. The 8.2 authoring validation delegates a third: whether registration keys on the run-input type.

This is the RV-2 revision. The first pre-gate draft and RV-1 were both rejected with the two-constructor seam upheld; their disposition tables are in §11 and §12.

Facts established against the code at the base commit:

| Fact | Evidence |
|---|---|
| Hosting may touch 3 internal DAG types / 15 members only | `DagRuntimeViewBoundaryGuards.cs` `AllowedTypes`, `AllowedMembers` |
| The structural fingerprint excludes `TRunInput` | `DagAuthoring.cs` `CanonicalStructure()`; 8.2 source validation, "Authoring boundary" |
| The durable side already derives a run-input contract key from the fixed-codec shape | `DurableDagInputShape.Digest`, `DurableDagRunInputCatalog.Install` (`dag:{id}:{version}:run-input:{digest}`) |
| A stored run compares that key with the installed plan | `DurableDagRunStore.SameGraph`, `DurableDagRunInputCatalog.Resolve` |
| `DefinitionRegistrationConflict`, `StartIdempotencyConflict`, `WorkflowFailure`, `PayloadFingerprint`, `FailureOccurrence.Root` have internal constructors in `OrcaCore` | `WorkflowFacadeContracts.cs`, `AuthoringValues.cs`, `PublicWorkflowValues.cs` |
| `OrcaCore.Engine.Durable` is a friend of `OrcaCore` and already creates `PayloadFingerprint` for DAG | `DurableDagChildEngine.RehydrateFingerprint` |
| The fault record replaces the run state and changes no node | `DurableDagChildCoordinator.RecordFaultAsync`, `DurableDagIntegrity.RecordIntegrityFaultAsync` |
| Reconciliation infers the pre-fault state from node states | `DurableDagIntegrity.ReconcilePoisonAsync` (`cancelling`) |
| The terminal wait loops through `IntegrityFault` | `DurableDagNotifications.WaitForTerminalAsync` |
| Every committed run append notifies live observers | `DagRunStoreRules` observer notice on append |
| Start replay with a different run id reports conflict | `DurableDagRunStore.FindExistingAsync` |
| The owner-chain guard hard-codes three owners | `OpenSpecCorpusGuards.ValidateActivePostGateOwners` |
| The settlement validator requires exactly two owners of its nine headings and its own block to stay canonical | `OpenSpecCorpusGuards` settlement successor loop (`owners.Should().HaveCount(2)`, `ProvenanceState.Synchronized`) |
| The run-input shape digest does not capture every codec acceptance rule | reviewer experiment at the parent: two DTOs with one full name, equal digest, the newer one rejecting the older one's `{}` bytes |
| The local catalog compares CLR `Type` as well as key | `DurableDagRunInputCatalog.Install` (`Entry` record equality) |
| A run-change notice carries a run id and version; the waiter rereads the latest checkpoint | `DagRunStoreRules` notice, `DurableDagNotifications.WaitForChangeAsync` |
| A run-input decode failure during mapping is currently a mapping failure | `DurableDagMappingReader.ReadMappingInputsAsync` |

## Goals / Non-Goals

**Goals**

- The smallest friend widening that lets `OrcaCore.Dag.Hosting` create the two public handles.
- No new internal type crossing the boundary; no new friend; no new package edge.
- Every typed DAG exception and type check in `OrcaCore.Dag`.
- A binding that cannot confuse two plans differing only in run-input type.
- A defined, restart-stable public outcome for a faulted run.

**Non-Goals**

- No product source, canonical sync, guard or fixture change in this draft.
- No change to the six child operations, the run contract, the provider ports or the lease protocol.
- No configuration-time plan staging (`AddDag`) and no operator reconciliation API; both are follow-ups.

## Decisions

### 1. Delegates over public types, not an internal operations interface

The handles receive their behavior as delegates in one internal constructor each, the pattern `WorkflowInstanceHandle` uses. Exact signatures are in the deltas. Two kinds of delegate result are nullable or flagged so that hosting can report an outcome whose exception only `OrcaCore.Dag` may construct. Start outcomes are not among them: every start outcome is a closed `DagStartResult` variant that hosting constructs publicly.

| Signal | Meaning | Thrown inside `OrcaCore.Dag` |
|---|---|---|
| reopen `Run == null`, `ActualDefinitionId == null` | run absent | `DagRunNotFoundException` |
| reopen `Run == null`, actual triple set, flag false | other identity, version or fingerprint | `DagRunDefinitionMismatchException` |
| reopen flag `RunInputContractDiffers` | same triple, other run-input contract | `DagRunInputContractMismatchException` |
| wait or cancel result `null` | run is in the integrity-fault state | `DagRunFaultedException` |

`OrcaCore.Dag` cannot reference any bridge-owned type, which is why these are result signals and not bridge exceptions.

Alternatives rejected: internal operations interfaces implemented by hosting (two more non-public types, six or more members); public interfaces or abstract handles (any assembly could supply a handle); making `OrcaCore.Dag.Hosting` a friend of `OrcaCore` (a tenth friend for values the durable engine already creates).

### 2. Ownership and projection split

| Concern | Owner |
|---|---|
| Typed throws, foreign-plan node rejection, `TOutput` check, node-output union | `OrcaCore.Dag` (handles and unions) |
| `IDagDefinitionRegistry`, handle wiring, public `DagRunSnapshot`/`DagNodeSnapshot`/status projection from run facts | `OrcaCore.Dag.Hosting`, public constructors and enum members only |
| Committed run facts, declared-type output decode, run-input contract key, start-key owner resolution | `OrcaCore.Durable.Hosting` behind the existing bridge |
| `DefinitionRegistrationConflict`, `StartIdempotencyConflict`, `WorkflowFailure`, `PayloadFingerprint` values | `OrcaCore.Engine.Durable` |

`planNodes` is the public `WorkflowDagPlan<TRunInput>.Nodes`, so hosting needs no access to `DagNodeRef.PlanToken` or `AuthoredOrdinal`. The durable-bridge reference fixture is refreshed in the source target; that is implementation evidence, not a canonical change.

### 3. Four-part definition binding (D4)

The binding is `(DefinitionId, DefinitionVersion, DefinitionFingerprint, run-input contract)`. The contract key is the existing durable value `dag:{id}:{version}:run-input:{fixed-codec shape digest of typeof(TRunInput)}`; `OrcaCore.Dag` never computes it. Folding the run-input type into the structural fingerprint was rejected: the digest needs fixed-codec metadata that `OrcaCore.Dag` must not reach, and a CLR type name is not a contract.

**The key is necessary, not sufficient.** The shape digest records member names and types, not every rule the codec applies when it reads bytes (requiredness, omitted defaults). Two comparisons are therefore distinguished:

| Comparison | Rule |
|---|---|
| Two registrations in one host process | same contract only when the declared types are the same CLR `Type` **and** the keys are equal; a different `Type` conflicts even with an equal key |
| Installed plan against a stored run | match only when the keys are equal **and** the durable runtime has verified that the stored run-input bytes decode with the installed declared type and re-encode through the fixed codec to exactly the stored bytes |

The stored-run verification is authoritative because it tests the actual committed bytes against the actual installed type. The run input is immutable, so a host may cache the verdict per run for its lifetime. It must complete before reopen and before any mapping evaluation. A failed verification is treated exactly as a different key and is never recorded as a mapping failure, node failure or integrity fault; today the same decode failure inside mapping would become `DAG_INPUT_MAPPING_INVALID`, a permanent business failure caused by a deployment mismatch. Strengthening the digest instead was rejected: it would chase codec semantics indefinitely and still not test the stored bytes.

| Operation | Stored / installed vs attempted | Outcome |
|---|---|---|
| `Register` | same id+version, equal fingerprint, same `Type` and key | `Registered`, idempotent |
| `Register` | same id+version, different fingerprint | `Conflict(DefinitionRegistrationConflict)`; takes precedence |
| `Register` | equal triple, other `TRunInput` (even with an equal key) | `RunInputConflict(existing type, attempted type)`; earlier registration unchanged; `GetHandleOrThrow()` throws `DagRunInputContractMismatchException` |
| `Register` | run-input type the fixed codec rejects | the existing fixed-codec declared-type rejection propagates unchanged; nothing installed |
| `GetRunAsync` | triple equal, key equal, stored bytes verified | run handle |
| `GetRunAsync` | other id, version or fingerprint | `DagRunDefinitionMismatchException` (checked first) |
| `GetRunAsync` | equal triple, other key or failed verification | `DagRunInputContractMismatchException` with `RunId` |
| `GetRunAsync` | absent | `DagRunNotFoundException` |
| `StartOrGetAsync` | key owner equal in triple, contract key and normalized bytes | `Accepted(owner's run, WasExisting = true)` |
| `StartOrGetAsync` | key owner equal triple, other contract key | closed `RunInputConflict(ExistingRunId, triple, attempted type)`; no run created; nothing thrown |
| `StartOrGetAsync` | any other difference, including equal key with different bytes | `Conflict(StartIdempotencyConflict)` |
| Recovery discovery | triple equal, key equal, stored bytes verified | run is progressed |
| Recovery discovery | anything else | locally unavailable: skipped, no mutation, mapping, child effect or integrity fault |

`DagRunInputContractMismatchException : OrcaCoreException`, code `DAG-RUN-INPUT-CONTRACT-MISMATCH`, internal constructor, properties `DefinitionId`, `DefinitionVersion`, `DefinitionFingerprint`, `Type AttemptedRunInputType`, `Type? ExistingRunInputType` (null when the other contract was recorded by another host and is known only by key), `DagRunId? RunId` (null for registration; the owner's run id for a start conflict). It is thrown only by `GetRunAsync` and by `GetHandleOrThrow()` on a `RunInputConflict` variant, never by `Register` or `StartOrGetAsync`. For start, equal attempted and stored bytes under an equal key already prove the stored bytes round-trip with this host's type, so no separate verification is needed there.

The start path resolves the authoritative key owner both on retry and after losing a competing create, as the approved run contract requires, and keeps the separate explicit run-id collision check.

### 4. Frozen public status projection (delegated by RC-3)

| Internal | Public |
|---|---|
| node `StartAuthorized` | `DagNodeStatus.Running` |
| node `CancellationRequested` | `DagNodeStatus.CancellationRequested` |
| every other node state | same-named value |
| run `Running`, `CancellationRequested`, `Succeeded`, `Failed`, `Cancelled` | same-named value |
| run `IntegrityFault` | derived pre-fault status, see §5 |

`DagRunSnapshot.StartedAt` is the run creation time. `DagNodeSnapshot.OutputAvailable` is true only for a `Succeeded` resultful node.

### 5. Integrity-fault outcome (D1), defined against durable facts

The fault record sets the run state to `IntegrityFault` and changes no node, so the checkpoint no longer stores the prior run status. It is reconstructed from the checkpoint alone:

> pre-fault status is `CancellationRequested` when any node is `CancellationRequested` or `Cancelled`; otherwise `Running`.

Why this is exact, from the committed transitions:

- Only the run-cancellation commit produces node `CancellationRequested` (from `StartAuthorized`) or node `Cancelled` from `Pending`/`Ready`. A child-cancelled join yields node `Cancelled` only when the run state is already `CancellationRequested` or `Cancelled`; otherwise it yields `Failed` with `CHILD_CANCELLED`. So without a committed cancellation request no node is in either state.
- The cancellation commit converts every nonterminal node: `Pending`/`Ready` to `Cancelled`, `StartAuthorized` to `CancellationRequested`. A run that is still nonterminal afterwards has at least one node not yet joined, and that node is `CancellationRequested`. So with a committed cancellation request at least one node is in one of the two states.
- No join or admission is applied while the run is faulted, so node states at the fault are the node states read later, before and after restart.

`DurableDagIntegrity.ReconcilePoisonAsync` already uses the same inference to restore the state, which makes it the candidate rule the reviewer asked to verify; Task 2.5 pins it with tests for both orders and across restart.

| Call on a faulted run | Outcome |
|---|---|
| `GetSnapshotAsync` | nonterminal snapshot, derived status, `CompletedAt = null` |
| `WaitForTerminalAsync`, first recheck observes the fault | `DagRunFaultedException` |
| `WaitForTerminalAsync`, fault commits after registration and is still committed at the recheck | observer notice wakes the waiter; `DagRunFaultedException` |
| `WaitForTerminalAsync`, fault and reconciliation both commit before the recheck | no exception; the waiter observes the reconciled checkpoint and keeps waiting |
| `WaitForTerminalAsync` after reconciliation | ordinary wait |
| `RequestCancellationAsync`, derived status `CancellationRequested` | `AlreadyRequested` |
| `RequestCancellationAsync`, derived status `Running` | `DagRunFaultedException`; nothing committed |

**The exception is level-triggered.** It means an authoritative recheck observed a committed checkpoint in the faulted state; it does not mean a fault was committed at some point during the wait. A run-change notice names a version, but the waiter always rereads the latest checkpoint, so `Running -> IntegrityFault -> Running` can wake it after the fault is gone. Reporting every fault committed during the wait was rejected: it requires inspecting committed transitions after the waiter's last observed version, and it would report a condition that authoritative reconciliation has already cleared. Task 2.5 adds a test that holds the waiter until both commits have landed.

`DagRunFaultedException : OrcaCoreException`, code `DAG-RUN-FAULTED`, internal constructor, property `RunId`. This honors RC-3: no new public status value and no false terminal result. The two canonical wait requirements promised only a terminal snapshot; both are modified in this change. The alternative, a nonterminal `DagRunStatus.Faulted`, was declined by the owner.

### 6. Node failure values

Created by `OrcaCore.Engine.Durable` from the run record alone:

| Field | Value |
|---|---|
| `Code` | the persisted stable code: `DAG_INPUT_MAPPING_INVALID`, `CHILD_FAILED`, `CHILD_TIMED_OUT`, `CHILD_TERMINATED`, `CHILD_CANCELLED`, `DEPENDENCY_BLOCKED` |
| `Message` | one fixed invariant sentence per code, listed in doc 17; no free-form or localized text |
| `AuthoredLocation` | `dag:$/dag-node:dddddddd`, the node's zero-based authored ordinal as exactly eight invariant decimal digits |
| `Occurrence` | `FailureOccurrence.Root`; a DAG node is neither a fixed branch nor a dynamic item |
| `Causes` | empty in v1; the run record carries the normalized code and a snapshot must not read child streams |
| `ExceptionType` | null |

`Failed` and `DependencyBlocked` nodes carry a failure; `Cancelled` and every other state carry none.

### 7. Richer mismatch exception (D2)

`DagRunDefinitionMismatchException` gains `ExpectedDefinitionVersion`, `ActualDefinitionVersion`, `ExpectedDefinitionFingerprint`, `ActualDefinitionFingerprint`, so a same-identity mismatch is diagnosable.

### 8. Source obligations inherited by Task 8.6

- **Install on register.** `IDagDefinitionRegistry.Register` installs the plan on this host. Discovery skips runs whose plan is absent, so applications register every plan at startup.
- **Start replay by key owner**, per §3.
- **Cancellation trigger.** `Requested`, `AlreadyRequested`, `AlreadyTerminal` map one to one; the faulted case follows §5.
- **Terminal wait.** Uses the existing subscribe-then-recheck path; no polling loop is added.
- **Guard.** `DagRuntimeViewBoundaryGuards` gains exactly two member strings; its type list is unchanged. Probes for a third constructor, a field, a setter and a non-public type reference stay red.

### 9. Document reconciliation belongs to the contract target

Task 1.1 edits, in one reviewed target: `CLAUDE.md` (three types, seventeen members); doc 17 §17.2.6 (constructor listing, both `RunInputConflict` variants, both new exceptions, D2 members, status projection, failure-message table, the stored-bytes verification rule) and its exception catalog (`DAG-RUN-FAULTED`, `DAG-RUN-INPUT-CONTRACT-MISMATCH`), plus §17.3/§17.5 boundary rows; doc 09 and doc 12 where they restate the DAG terminal wait; doc 10 PR-005; doc 08 CP-020 pointer; Decision 22; solution architecture; the Task 8.0 map row for 8.6; affected Task 7.3 rows and digest. Doc 11 is checked and excluded unless the fault outcome is judged a nonfunctional obligation. Dated decisions and prior packets stay byte-exact.

### 10. Exact canonical edits beyond insertion

Each delta inserts text above the first scenario and appends scenarios. Existing clauses changed:

| Capability / heading | Existing clause | Change |
|---|---|---|
| developer-facing-surface / friends | "...every other non-public DAG type/member/overload SHALL be forbidden." | append ", except the two facade-handle constructors named below" |
| developer-facing-surface / friends | "...outside the exact runtime-view signatures, including..." | insert "and the two facade-handle constructors" |
| developer-facing-surface / friends | "...beyond the named `OrcaCore.Dag -> OrcaCore.Dag.Hosting` proposal." | append "; the facade-handle grant below reuses that same friend edge" |
| developer-facing-surface / closed results | "...so callers can inspect failure values and `WasExisting`." | append "; the DAG registration and start results SHALL additionally carry the closed `RunInputConflict` variants defined below" |
| developer-facing-surface / closed results | "...carrying the corresponding closed conflict value." | append ", or `DagRunInputContractMismatchException` for a `RunInputConflict` variant" |
| developer-facing-surface / completion waits | "...containing the detached terminal snapshot." | append ", or throw `DagRunFaultedException` while the run is in the nonterminal integrity-fault state as the `management-and-querying` DAG terminal-wait requirement defines" |
| management-and-querying / DAG terminal wait | "...with one terminal snapshot, and SHALL NOT poll." | insert "or with the typed integrity-fault outcome defined below" |
| repository-foundation / dependency direction | "...or any additional internal type/member/overload. It SHALL NOT add..." | insert "other than the two facade-handle constructors named below" |

Both closed-results clauses are in one canonical paragraph. Everything else in the five blocks is byte-identical to canonical at the base commit. The deltas are generated from the canonical files by script with a uniqueness assertion on each replaced clause. The completion-waits delta appends a scenario and inserts no paragraph.

### 11. RV-1 disposition of the rejected draft

| Finding | Disposition |
|---|---|
| P1 run-input contract missing from registration and reopen | §3; friends delta paragraph and three scenarios; reopen flag and nullable start result in both constructor signatures; new exception and union variant; revised proof |
| P1 D1 not defined against durable facts; wait requirements unreconciled | §5; two wait headings added as `MODIFIED`; `DAG-RUN-FAULTED` in §9 catalog scope; five scenarios covering fault before and after cancellation, fault after waiter registration, restart and reconciliation |
| P2 node failure location and occurrence | §6 |
| P2 gate tasks not executable | tasks.md 0.1 (guard edit), 1.1 (re-pin), 1.3/1.4 (atomic), 2.0 (substrate checkpoint, 2.4 overlap), 3.1/3.2 (split closeout) |
| Note: enum values have no constructors | friends delta now says status values are public enumeration members |
| Note: make the projection split explicit | §2 and tasks 2.2/2.3 |
| Note: start replay must also cover a competing create and keep the run-id collision check | §3 and friends delta |

### 12. RV-2 disposition of rejected RV-1

| Finding | Disposition |
|---|---|
| P1 start under another run-input contract contradicted the closed-result requirement | start returns closed `DagStartResult.RunInputConflict` retaining the owner's run id; the start delegate result is no longer nullable; the closed-results heading is added as a fifth `MODIFIED` requirement; §1, §3 |
| P1 equal shape digests can admit incompatible input contracts | key equality is necessary only; stored-run matching also requires verified decode and byte-identical re-encode of the stored run input before reopen and mapping; local `Type` equality is distinguished from durable key equality; §3; friends delta and scenario |
| P1 a fault notice can arrive after reconciliation | the exception is level-triggered on an authoritative recheck; a reconciled fault is not reported and transitions are not inspected; §5; both wait deltas; held-waiter scenario and test |
| Note: the settlement validator requires exactly two owners and a canonical block | tasks 0.1 and 1.4 include it in the stage-aware guard work |

## Risks / Trade-offs

- [Long delegate constructors with nullable and flagged results] → internal, called from one place, pinned by exact metadata; the alternative adds internal types.
- [A third registration variant widens a closed union] → it is closed and typed; the alternative is an uninformative conflict whose two fingerprints are equal.
- [A third start variant widens a closed union] → it keeps the rejection inspectable with the owner's run id; `StartIdempotencyConflict` cannot express it because the definition triple is equal and the input fingerprint covers bytes only.
- [Stored-bytes verification costs one decode and encode per run per host] → the input is immutable, the verdict is cached, and mapping decodes the same bytes anyway.
- [A transient fault is invisible to a slow waiter] → by design; the snapshot and operator telemetry remain the record of a fault.
- [Five headings now gain an owner] → five registry rows with three different predecessor chains and one independent settlement validator; Task 0.1 says so.
- [8.6 source before predecessor checkboxes close] → design and gate work may proceed; Task 2.0 blocks source on a recorded reviewed substrate checkpoint.

## Follow-ups recorded, not proposed here

- Configuration-time plan staging (`AddOrcaCoreDag(...).AddDag(plan)`), mirroring `AddWorkflow`.
- A management entry point for integrity reconciliation; without it a faulted run can only be observed.

## Open Questions

None. The owner accepted D1 and D2 on 2026-11-15. D3 follows from the grant. D4 (typed run-input binding) is new in RV-1 and awaits the owner and the reviewer.
