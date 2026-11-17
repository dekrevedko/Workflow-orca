## Context

Base: `da1a4729` (DAG Task 2.3 regression follow-up approved and recorded). The runtime-view amendment is Complete; reshape 8.3–8.5 and `admit-durable-dag-run-progression` 2.4/3.x are still open. Doc 17 §17.2.6 fixes the public DAG facade. The approved run contract (RC-3) delegates two things to Task 8.6: the public status projection, and the public cancellation trigger on a DAG-role handle.

Facts established against the code at the base commit:

| Fact | Evidence |
|---|---|
| Hosting may touch 3 internal DAG types / 15 members only | `tests/OrcaCore.DeveloperSurface.Guards/DagRuntimeViewBoundaryGuards.cs` `AllowedTypes`, `AllowedMembers` |
| `DefinitionRegistrationConflict`, `StartIdempotencyConflict`, `WorkflowFailure`, `PayloadFingerprint` have internal constructors in `OrcaCore` | `src/OrcaCore.Abstractions/Facades/WorkflowFacadeContracts.cs`, `Instances/AuthoringValues.cs`, `Instances/PublicWorkflowValues.cs` |
| `OrcaCore.Engine.Durable` already mints `PayloadFingerprint` for DAG | `DurableDagChildEngine.RehydrateFingerprint` |
| `DagRunId` is publicly constructible | `DagRunId.Parse(string)` |
| No public start path exists; plans install only on create | `DagCoordinator.CreateOrReopenAsync`, `DagDefinitionRegistry.Install` |
| Cancellation, integrity reconciliation have no production caller | `DurableDagRunBridge.RequestRunCancellationAsync`, `ReconcilePoisonAsync` |
| Start replay with a different run id reports conflict | `DurableDagRunStore.FindExistingAsync` (`keyOwner.RunId != request.RunId`) |

## Goals / Non-Goals

**Goals**

- The smallest friend widening that lets `OrcaCore.Dag.Hosting` create the two public handles.
- No new internal type crossing the boundary; no new friend; no new package edge.
- Keep every typed DAG exception and type check in `OrcaCore.Dag`.
- Freeze the public projection of the two internal-only states.

**Non-Goals**

- No product source, canonical sync, guard or fixture change in this draft.
- No change to the six child operations, the run contract, the provider ports or the lease protocol.
- No configuration-time plan staging (`AddDag`) and no operator reconciliation API; both are recorded as follow-ups below.

## Decisions

### 1. Delegates over public types, not an internal operations interface

The handles receive their behavior as delegates in one internal constructor each, the same pattern `WorkflowInstanceHandle` uses for the workflow facade. Exact signatures are in the two deltas.

Alternatives rejected:

- **Internal `IDagRunOperations` / `IDagDefinitionOperations<T>` interfaces implemented by hosting.** Adds two non-public types and six or more members to the allowlist, and an interface implemented across the friend edge is harder to pin by member reference than a constructor call.
- **Public interfaces or abstract handles.** Removes the friend need but lets any assembly supply a handle, and breaks symmetry with the sealed workflow handles.
- **Make `OrcaCore.Dag.Hosting` a friend of `OrcaCore`.** A tenth product friend, for values the durable side can already create.

### 2. Type checks and typed throws stay in `OrcaCore.Dag`

- `DagRunHandle.GetOutputAsync<TOutput>` rejects a node that is not one of `planNodes` by reference identity before calling any delegate, then checks the supplied `object?` against `TOutput` using the existing null rule (null is valid only for a reference or nullable-value type), then builds `Available`/`Unavailable`.
- `DagDefinitionHandle.GetRunAsync` throws `DagRunNotFoundException` when the reopen delegate returns no run and no actual definition, and `DagRunDefinitionMismatchException` when it returns an actual definition. Hosting never constructs either.
- `planNodes` is the public `WorkflowDagPlan<TRunInput>.Nodes` list, so hosting needs no access to `DagNodeRef.PlanToken` or `AuthoredOrdinal`.

### 3. Values with internal constructors come from the durable side

Hosting cannot construct `DefinitionRegistrationConflict`, `StartIdempotencyConflict`, `WorkflowFailure` or `PayloadFingerprint`. `OrcaCore.Engine.Durable` can, as an existing friend of `OrcaCore`. Task 8.6 adds internal facade helpers there, exposed to hosting through the existing `OrcaCore.Durable.Hosting -> OrcaCore.Dag.Hosting` bridge, returning those values as public types. The durable-bridge reference fixture (`dag-task-2-3-bridge-references.txt`) is refreshed in the source target; that is implementation evidence, not a canonical change.

Node failure values use the stable codes already persisted (`DAG_INPUT_MAPPING_INVALID`, `CHILD_FAILED`, `CHILD_TIMED_OUT`, `CHILD_TERMINATED`, `CHILD_CANCELLED`, `DEPENDENCY_BLOCKED`), the node id as authored location, and an empty cause list. The child's own failure is not copied into the run snapshot in v1, because the run record does not persist it and a snapshot read must not cross into child streams.

### 4. Frozen public status projection (delegated by RC-3)

| Internal | Public |
|---|---|
| node `StartAuthorized` | `DagNodeStatus.Running` |
| node `CancellationRequested` | `DagNodeStatus.CancellationRequested` |
| every other node state | same-named value |
| run `Running`, `CancellationRequested`, `Succeeded`, `Failed`, `Cancelled` | same-named value |
| run `IntegrityFault` | no enum value; see D1 |

`DagRunSnapshot.StartedAt` is the run creation time. `DagNodeSnapshot.OutputAvailable` is true only for a `Succeeded` resultful node.

### 5. Owner decisions (D1 and D2 accepted by the owner on 2026-11-15)

**D1. How a faulted run appears publicly. Accepted: typed exception.**
RC-3 says `IntegrityFault` is not a new public enum value and not a false terminal result. Today `WaitForTerminalAsync` on such a run never returns and cancellation leaks `InvalidDataException`. Proposed: `GetSnapshotAsync` keeps reporting the last committed public status; `WaitForTerminalAsync` and `RequestCancellationAsync` throw a new public `DagRunFaultedException : OrcaCoreException` (code `DAG-RUN-FAULTED`, property `RunId`) constructed inside `OrcaCore.Dag`. The terminal-wait and cancellation delegates return a nullable result, and null means "faulted"; the handle translates null into the exception, so hosting never constructs it and the seam stays at two constructors with no new type. `OrcaCore.Dag` cannot reference any bridge-owned type, which is why the signal is a null result and not a bridge exception. A caller may wait again after an operator reconciles the run.
Alternative: add nonterminal `DagRunStatus.Faulted`. Simpler for callers, but reopens the approved RC-3 wording; the two delegates would then return non-null results and the nullable annotations would be dropped from the signatures.

**D2. Richer mismatch exception. Accepted.**
`DagRunDefinitionMismatchException` carries only definition ids, so a run stored under the same id with another version or fingerprint produces a message with identical expected and actual values. Add `ExpectedDefinitionVersion`, `ActualDefinitionVersion`, `ExpectedDefinitionFingerprint`, `ActualDefinitionFingerprint`. The reopen delegate already returns all three actual values, so declining D2 changes no signature in the grant.

**D3. Handle constructor listing in doc 17. Required by the grant.**
Replace `internal DagDefinitionHandle();` and `internal DagRunHandle(DagRunId runId);` with the delegate constructors.

### 6. Source obligations inherited by Task 8.6

- **Install on register.** `IDagDefinitionRegistry.Register` installs the plan on this host. Runs of a plan not registered on a host are skipped by that host and picked up within one discovery pass after registration. Applications register every plan at startup.
- **Start replay by key.** RC-3 binds the start key to one runtime-created run id and returns `Existing` for an exact match. The facade must look up the key owner first and compare binding and input, not treat a different run id as a conflict. `StartIdempotencyConflict` carries the existing and attempted definition triple and input fingerprint.
- **Cancellation trigger.** `DagRunHandle.RequestCancellationAsync` maps `Requested`, `AlreadyRequested`, `AlreadyTerminal` one to one; `NotFound` after a successful open is a runtime defect.
- **Terminal wait.** Uses the existing subscribe-then-recheck notification path; no polling loop is added in hosting.
- **Guard.** `DagRuntimeViewBoundaryGuards` gains exactly two member strings; its type list is unchanged. Probes for a third constructor, a field, a setter and a non-public type reference stay red.

### 7. Document reconciliation belongs to the contract target

Task 1.1 edits, in one reviewed target: `CLAUDE.md` (friend paragraph: three types, seventeen members); doc 17 §17.2.6 (constructor listing, D1/D2 text, status projection table) and §17.3/§17.5 boundary rows; doc 10 PR-005; doc 08 CP-020 pointer; Decision 22; solution architecture; the Task 8.0 map row for 8.6; affected Task 7.3 rows and digest. Doc 11 is checked and excluded unless D1 is judged a nonfunctional obligation. Dated decisions and prior packets stay byte-exact.

### 8. Exact canonical edits beyond insertion

The deltas insert one paragraph and the signature block above the first scenario and append scenarios. They also change three existing sentences so the text does not contradict itself:

| Capability | Existing clause | Change |
|---|---|---|
| developer-facing-surface | "...every other non-public DAG type/member/overload SHALL be forbidden." | append ", except the two facade-handle constructors named below" |
| developer-facing-surface | "...outside the exact runtime-view signatures, including..." | insert "and the two facade-handle constructors" |
| developer-facing-surface | "...beyond the named `OrcaCore.Dag -> OrcaCore.Dag.Hosting` proposal." | append "; the facade-handle grant below reuses that same friend edge" |
| repository-foundation | "...or any additional internal type/member/overload. It SHALL NOT add..." | insert "other than the two facade-handle constructors named below" |

Everything else in both blocks is byte-identical to canonical at the base commit. The deltas were generated from the canonical files by script with a uniqueness assertion on each replaced clause.

## Risks / Trade-offs

- [Delegate constructors are long] → They are internal, called from one place, and pinned by exact metadata; the alternative adds internal types.
- [Tuple results in a friend signature] → `System.ValueTuple` is public; the guard pins the full generic signature.
- [Fault translation by exception type for D1] → Keeps the grant at two members. If judged too indirect, D1's alternative needs no translation.
- [Fourth owner of two headings] → Requires the process-only registry gate before contract review, as the runtime-view change did.
- [8.6 starting before 8.3–8.5 checkboxes close] → The source slice depends only on bridge members that already exist plus the helpers in Decision 3; the owner decides sequencing.

## Follow-ups recorded, not proposed here

- Configuration-time plan staging (`AddOrcaCoreDag(...).AddDag(plan)`), mirroring `AddWorkflow`, so recovery does not depend on application startup code. Touches the exact hosting-signature requirement, so it needs its own change.
- A management entry point for integrity reconciliation; without it a faulted run can only be observed.

## Open Questions

None. The owner accepted D1 and D2 as recommended on 2026-11-15; D3 follows from approving the grant. Independent review can still reject any of them.
