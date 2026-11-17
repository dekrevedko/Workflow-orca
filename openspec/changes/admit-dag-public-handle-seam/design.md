## Current completion scope (2026-11-16)

The implementation/canonical sync is independently approved at `b9dcdef7` / `598c6d4`; its reviewed main merge/remediation is checkpointed at `1e960fc4` / `94e9aca5`. The owner authorizes the remaining review fixes and Task 3.2 Complete activation after validation. The reviewers authored RV-1–RV-4 and expressly excluded independent contract approval; owner-approved RV-4 remains the unchanged five-block contract. This activation closes only the facade seam and reshape 8.6, not the DAG run change or the rest of Section 8. Earlier dated draft and process statements below are historical context. See `artifacts/task-3-2-owner-authority-2026-11-16.md`.

## Context

Base: `da1a4729` (DAG Task 2.3 regression follow-up approved and recorded). The runtime-view amendment is Complete; reshape 8.3–8.5 and `admit-durable-dag-run-progression` 2.4/3.x are still open. Doc 17 §17.2.6 fixes the public DAG facade. The approved run contract (RC-3) delegates two things to Task 8.6: the public status projection, and the public cancellation trigger on a DAG-role handle. The 8.2 authoring validation delegates a third: whether registration keys on the run-input type.

This implementation candidate follows the approved RV-4 pre-gate design (a focused correction of RV-3, which resolved the RV-2 blocker). The first pre-gate draft and RV-1 were rejected with the two-constructor seam upheld, and RV-2 was returned with one blocking inconsistency and a recommendation to prototype. The dispositions are in §11 to §14; the prototype evidence is `artifacts/task-1-1-public-handle-seam-prototype-evidence-2026-11-15.md`.

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
| The parent normalization accepts a value it cannot read back | prototype codec probe: a private-setter type stored `{"Value":17}` and re-encoded `{"Value":0}` |
| Without a stored-input check an incompatible host maps the misread value and the run succeeds | prototype control with the recovery skip disabled |
| A role-free pump joins an admitted child on a host that cannot map the run | prototype redeploy test |

## Goals / Non-Goals

**Goals**

- The smallest friend widening that lets `OrcaCore.Dag.Hosting` create the two public handles.
- No new internal type crossing the boundary; no new friend; no new package edge.
- Every typed DAG exception and type check in `OrcaCore.Dag`.
- A binding that cannot confuse two plans differing only in run-input type.
- A defined, restart-stable public outcome for a faulted run.

**Non-Goals**

- No compatibility layer for the unshipped facade. The owner authorized source, canonical sync, guard and fixture changes for final review.
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

**Run-input round-trip rule (RV-3).** Scope: the DAG run input only. Mapped node inputs, child outputs and ordinary workflow inputs have different commit paths and failure outcomes and are outside this rule.

| Point | Rule |
|---|---|
| Start and replay, before creating a run or accepting a replay | encode the submitted input with the declared type, decode those bytes, require byte-identical re-encoding; on failure throw `NotSupportedException` before any run is committed or any start key is bound |
| Reopen and recovery, before issuing a handle or evaluating a mapper | verify the stored input the same way under the installed binding |
| Well-formed stored payload fails the verification | the run is locally unavailable on that host, exactly as with a different key; no mapping failure, node failure or integrity fault |
| Stored value's recorded contract key disagrees with its own stored graph, or framing, codec format or fingerprint is invalid | corrupt storage: keeps its existing integrity classification; never reclassified as a contract mismatch; applies equally to a start-key owner examined during replay |
| Valid stored graph key differs from the installed plan's key | binding mismatch |
| Storage failure, integrity failure, caller cancellation | propagate as exceptions from registration, start and reopen, alongside the closed comparison results |

The byte check is a guarantee about the stored representation only. A change in what the values mean still requires a new definition version. Because the same check runs at submission, a run that was created or replayed on a host is reopenable and recoverable on that host under the same binding; the prototype showed that the parent accepts a value it cannot read back, which is what made RV-2 inconsistent. The run input is immutable, so a host may cache the stored-run verdict per run for its lifetime. Strengthening the digest instead was rejected: it would chase codec semantics indefinitely and still not test the stored bytes.

**Locally unavailable means no new mapping or admission, nothing more.** The incompatible host skips mapping and admission for the run without mutation. Child start, join and cancellation work already committed for the run is role-independent and is still processed by any durable host; the prototype observed such a host joining an admitted child while leaving the unmapped node `Ready`. Likewise, cancellation accepted through a compatible host's handle commits child-cancellation work that any durable host's pump can consume, including the host that cannot map the run.

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
| `StartOrGetAsync` | submitted input fails the round-trip rule | `NotSupportedException`; no run, no key bound; first start and replay alike |
| `StartOrGetAsync` | key owner equal in triple, contract key and normalized bytes | `Accepted(owner's run, WasExisting = true)` |
| `StartOrGetAsync` | key owner equal triple, other contract key | closed `RunInputConflict(ExistingRunId, triple, attempted type)`; no run created; nothing thrown; never signalled through the reopen flag |
| `StartOrGetAsync` | any other difference, including equal key with different bytes | `Conflict(StartIdempotencyConflict)` |
| Recovery discovery | triple equal, key equal, stored bytes verified | run is progressed |
| Recovery discovery | anything else | locally unavailable: no new mapping or admission, no mutation or fault; committed child work is still processed |

`DagRunInputContractMismatchException : OrcaCoreException`, code `DAG-RUN-INPUT-CONTRACT-MISMATCH`, internal constructor, properties `DefinitionId`, `DefinitionVersion`, `DefinitionFingerprint`, `Type AttemptedRunInputType`, `Type? ExistingRunInputType` (null when the other contract was recorded by another host and is known only by key), `DagRunId? RunId` (null for registration; the owner's run id for a start conflict). It is thrown only by `GetRunAsync` and by `GetHandleOrThrow()` on a `RunInputConflict` variant, never by `Register` or `StartOrGetAsync`. The closed start variants cover every comparison with an existing key owner; the only exception `StartOrGetAsync` raises for its input is the round-trip rejection above.

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

**The exception is level-triggered.** It means an authoritative recheck observed a committed checkpoint in the faulted state; it does not mean a fault was committed at some point during the wait. A run-change notice names a version, but the waiter always rereads the latest checkpoint, so `Running -> IntegrityFault -> Running` can wake it after the fault is gone. Reporting every fault committed during the wait was rejected: it requires inspecting committed transitions after the waiter's last observed version, and it would report a condition that authoritative reconciliation has already cleared. Task 2.5 adds a test that holds the waiter until both commits have landed, using an explicit barrier; the prototype arranged the race with timing delays, which is acceptable for a prototype only.

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

### 13. RV-3 disposition of RV-2 and the prototype

| Input | Disposition |
|---|---|
| Blocking: RV-2 could accept a new run and an exact-byte replay, then refuse to reopen that run | the round-trip check now runs at submission as well; a value that cannot be read back never creates a run; the RV-2 argument that equal start bytes prove compatibility is removed; §3 |
| Scope: "every committed DAG value" was too wide | the rule covers the DAG run input only; §3 and friends delta |
| Outcome for a failing submitted value | `NotSupportedException` before any commit or key binding; §3 |
| Corrupt storage must keep its classification | stated; the prototype's reclassification is recorded as a deviation |
| What an incompatible host still does | skips only new mapping and admission; committed child work proceeds; §3 |
| Fault race arranged by delays in the prototype | final regression test uses an explicit barrier; task 2.5 |
| Byte equality is about representation | stated in §3 and in the friends delta |

Upheld and unchanged: the two constructors, the closed `RunInputConflict` variants with the owner's run id, the level-triggered fault outcome and the cancellation outcomes.

### 14. RV-4 focused correction of RV-3

RV-3 resolved the RV-2 blocker; the review accepted D4 and `NotSupportedException` for a submitted run input that fails the byte round-trip, and asked for three corrections before Task 0.1.

| Correction | Disposition |
|---|---|
| Route each mismatch through the right result | the reopen flag is for reopen only; a start with an equal triple and different contract keys returns `DagStartResult.RunInputConflict`; the cross-host scenario is narrowed to that condition; equal keys with different validated bytes are an ordinary `StartIdempotencyConflict` with its own scenario |
| Name both key comparisons | recorded value key versus own stored graph key is integrity; stored graph key versus installed plan key is binding mismatch; the integrity check also covers an owner examined during replay; storage, integrity and cancellation exceptions are preserved alongside the closed results |
| Narrow the prototype evidence claim | the evidence record now names the suites that passed and states that the shared normalizer covers run input and mapped node input only, child output going through a separate unchanged path |
| Make committed cancellation on an incompatible host explicit | added as a scenario and in §3 |

### 15. Implementation and verification outcome

The public facade is implemented through the exact two constructors. The stored-input verdict is cached per run, installed CLR type, contract key and stored fingerprint, while integrity validation runs on every read. A start normalizes once, then resolves the authoritative start-key owner before and after a competing create. Fault waits use the existing subscribe/recheck loop and test barriers hold rechecks until both commits.

The real PostgreSQL run uncovered a substrate defect: checkpoint loading dropped child ParentInstanceId and RootInstanceId, so the terminal-notice path could not join the child. The provider now writes, loads and updates both nullable UUIDs, with a regression covering preservation and clearing. The initial schema changes under the owner's greenfield instruction; no migration of existing deployed databases is claimed. SQL Server already preserved these values.

The full provider suites and real process-restart certification exercise the production facade. The standalone RV-2 proof still checks signatures only. Historical reviews grant no forward implementation approval; the final candidate and validation record await external review.


## Risks / Trade-offs

- [Long delegate constructors with nullable and flagged results] → internal, called from one place, pinned by exact metadata; the alternative adds internal types.
- [A third registration variant widens a closed union] → it is closed and typed; the alternative is an uninformative conflict whose two fingerprints are equal.
- [A third start variant widens a closed union] → it keeps the rejection inspectable with the owner's run id; `StartIdempotencyConflict` cannot express it because the definition triple is equal and the input fingerprint covers bytes only.
- [Stored-bytes verification costs one decode and encode per run per host] → the input is immutable, the verdict is cached, and mapping decodes the same bytes anyway.
- [Prototype evidence is in-process and in-memory] → the implementation now passes both full database-provider suites and separate-process restart certification; the prototype alone receives no credit for those exits.
- [Start can now throw for its input] → the added NotSupportedException outcome rejects a value that cannot be stored faithfully before any effect; storage, integrity and caller-cancellation exceptions still propagate. Every binding comparison with an existing run stays a closed result.
- [A transient fault is invisible to a slow waiter] → by design; the snapshot and operator telemetry remain the record of a fault.
- [Five headings now gain an owner] → five registry rows with three different predecessor chains and one independent settlement validator; Task 0.1 says so.
- [8.6 source before predecessor checkboxes close] → design and gate work may proceed; Task 2.0 blocks source on a recorded reviewed substrate checkpoint.

## Follow-ups recorded, not proposed here

- Configuration-time plan staging (`AddOrcaCoreDag(...).AddDag(plan)`), mirroring `AddWorkflow`.
- A management entry point for integrity reconciliation; without it a faulted run can only be observed.

## Open Questions

None. The owner accepted D1 and D2 on 2026-11-15. D3 follows from the grant. D4 and the submitted-input `NotSupportedException` outcome were accepted during the RV-3 review; RV-4 passed the pre-gate design review. Implementation/canonical sync and merge/remediation reviews are approved; Task 3.2 records the owner-authorized closeout.

## 2026-11-16 implementation review follow-up

Implementation checkpoint `b9dcdef7e611da45d7dc4f75946ee192fc83c37e` preserves reviewed tree `8ef5842bb8c7952f30f0f4184e97baf45ba986c0`. Its direct child `598c6d4fb61f7883e25f79f03001aa620dda0bf7` catalogues the byte-exact independent implementation verdict and clears the active freeze. The reviewer authored RV-1–RV-4 and explicitly excluded independent contract approval; the owner-approved RV-4 contract and owner-directed combined process authority remain the contract basis.

F1 now uses an independently reconstructed typed frame, verifies exact equality before corruption, and checks the precise run-input integrity exception. It covers recorded-key mismatch and malformed JSON with a valid fingerprint on reopen, replay, public reads/cancellation/wait and a real Ready node observed by the scheduler. A separate white-box mapping check proves mapper refusal. Restoring the original readable bytes lets that same node complete. The earlier JsonNode tests are retained with a narrower name; they certify outer framing/value rejection only.

F3 uses a public InvalidOperationException for a missing or incompatible installed child binding before any start effect; registration remains the plan-install point, without a new result variant. F4 explicitly records existing fail-closed behavior for corrupt stored input across all reads and poison reconciliation, distinct from a valid checkpoint in IntegrityFault. The five RV-4 delta blocks and public signatures are unchanged.

F2 is a separate merge/remediation candidate with second parent `ba2b057d7898f516845e90323b4288fb0c1744d8`. Both sides' source and canonical changes are retained; the nine ephemeral source paths are separately checked against that exact reviewed mainline tip. No broad folder exclusion is permitted. Final promotion depends on review of this merge and the F1 evidence; no activation was part of the checkpoint/evidence chain.

## Task 3.2 finding resolution (2026-11-16)

The Core.Tests wall-clock guard is retained unchanged. Admission is observed through the real child gate; the redundant snapshot poll is removed; test-only lease contention retries are bounded by the provider's five-second lease and fifteen-second maximum turn. A timeout cancels the queued test request. Full CI unit lanes now include Core.Tests and ProviderCertification, with provider process-restart evidence rerun.

The missing/incompatible child-binding InvalidOperationException is explicitly documented as host-configuration behavior outside the five seam requirements, not a fourth closed comparison result. No delta or canonical block changes. The historical guard now checks committed implementation/merge blobs and real direct-child evidence, not live source/doc bytes against the old draft. Current canonical, API and maintained documentation checks remain active; the three shutdown-sync document checks strictly require their reviewed merged after-state.
