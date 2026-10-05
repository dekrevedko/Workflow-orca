## Context

Task 8.4's independently approved child-protocol checkpoint is `1d9672a51ed6f2770dcf7023d13a915a9b135571` (tree `4b83f4d9a5cf2ecaf8d3dc2e60b6e1c56e6c02e2`); its direct-child evidence commit is `095bbfd1a6b559f8f1be47fb8d4bcd4749d40854`. The verdict is `developer-facing-interface-section-08-task-8-4-durable-dag-bridge-contract-br-remediation-independent-review-verdict-2026-10-04.md`, SHA-256 `2fe0cac10719ab0aa9e6321461fb4b9cb5d63504a0436585dc66f51b32375557`. There is no activation: reshape 8.4 and 8.5 remain open.

That approval covers the six-operation child protocol, not a complete DAG runtime seam. Its section 10 requires a run-persistence/progression contract and canonical amendment before source. This target is the prerequisite process gate for four proposed successors, not that contract's approval. The old approved and rejected artifacts remain immutable, including their explicitly provisional section 10 assumptions. The five new review notes are addressed as proposed decisions here and must be independently judged in Task 1.1/1.2.

## Goals / Non-Goals

**Goals:**

- Register only four exact `(capability, requirement)` successors of reshape, pinned to predecessor and proposed block hashes, with stage `Proposed` and sync owner 1.3.
- Preserve every existing amendment row, approval, superseded block and artifact; recompute provenance honestly as pending and ineligible for semantic approval.
- Make the next contract review cover complete lifecycle/persistence, internal routing and provider identity together rather than lock in a partial seam.

**Non-Goals:**

- No canonical sync, friend attribute, public/advanced API implementation, bridge source, codec access, provider migration, runtime registration or test-disposition change.
- No additional package edge or friend. DAG authoring/mapping owns delegates; the durable side owns codec bytes, commit, child identity and persistence.
- No claim that the six child operations create or cancel a run. No claim of atomic writes spanning a run stream and a child stream.
- No rewriting approved artifacts, dated decisions, completed tasks or the Task 8.0 map in this process gate. Contract Task 1.1 must reconcile their current owners explicitly.

## Decisions

### 1. Exact process exception, not a generic duplicate-owner waiver

The proposed successor set is closed:

| Capability | Exact heading |
| --- | --- |
| durable-persistence-and-outbox | Workflow store persists first-class durable records |
| durable-persistence-and-outbox | Outbox delivery is adapter-driven and at-least-once |
| durable-persistence-and-outbox | First-release provider ownership and schemas are exact |
| durable-runtime | Durable DAG progression is runtime owned |

Each full delta copies the canonical predecessor block unchanged and appends the proposed obligations and scenarios. The process registry pins both blocks and their operations; only those four headings may coexist under reshape and this change. A third owner, undeclared heading, stage change, early sync or altered block fails closed. Task 0.2 is deliberately not pinned open, so evidence plus checkbox-only activation does not require a red intermediate commit. Contract and source tasks remain open.

Task 1.1 may revise these proposed blocks only in a new uncommitted reviewed target, with coherent source/fixture hash updates and a new provenance artifact. Approval at 1.2 must be bound to a unique terminal APPROVE verdict, the reviewed tree, and a single-parent evidence commit that adds that verdict. Tasks 1.3/1.4 are atomic: sync the exact approved blocks and promote their registry to ApprovedPending while retaining the proposed rows and hash-exact predecessor history. Source cannot begin before that transition's independent approval/checkpoint. Complete needs separate implementation evidence.

Withdrawal requires one reviewed target that removes all four proposed rows and the change directory together, preserves proposal/review history in an immutable recorded disposition, and restores measured provenance. Archival resolution must accept one active or dated archived record, fail on duplicates, and must not require an unarchived delta forever. These transitions are requirements for later gates, not implemented here.

### 2. Real commit port and an explicit identity amendment

There is no `IWorkflowCommitStore`. The existing public advanced port is `IWorkflowEventStore.AppendAsync(ProviderCommitBatch)`. The batch's event, checkpoint, projection and idempotency shapes are currently workflow-oriented. `WorkflowStreamId` in `OrcaCore` exposes `InstanceId` and renders `workflow-{id}`; it cannot honestly name `dag-run-{lowercase-D-guid}` today.

Task 1.1 must freeze an exact typed stream/record identity proposal and all required port changes. It must enumerate the application/advanced public baseline changes, `WorkflowEvent`/checkpoint framing, fenced discovery and recovery queries, production first-create tables/indexes and provider certification. The proposed `dag-run-v1` aggregate is first-class durable state, not an internal continuation payload masquerading as a workflow instance. Existing workflow stream names and value fingerprints must remain stable. Separate stream commits require deterministic reconciliation, not a false cross-stream transaction promise.

Alternatives rejected: reusing a fake commit-port name; hiding a DAG aggregate behind `InstanceId`; treating new public stream construction as an internal-only change; silently widening provider JSON framing.

### 3. Complete lifecycle before bridge source

Task 1.1 must specify exact operations and closed outcomes for create/reopen, run-input and dependency persistence, read/fenced recovery, ready notification, mapping failure with transitive blocking, mapped-input admission, child-terminal join, run cancellation, fault/poison reconciliation, and run-change/terminal subscriptions. It must define provider-authoritative aggregate ownership, compare-and-append versions, lease/fence checks, deterministic idempotency, restart discovery and atomic related records for each transition.

The one-way package edge stays intact: Durable.Hosting exposes an internal ready-sink registration seam; Dag.Hosting implements it and evaluates mappers. Durable.Hosting must never reference Dag.Hosting or invoke a persisted delegate. The durable bridge eagerly decodes **every** successful declared resultful dependency, including unused outputs, before handing the exact declared map to the evaluator. Null, declared types and mapper exceptions retain the already-approved runtime-view policy. Decode failure is a durable bridge failure before mapper execution, not a lazy `OutputOf` error. The six approved child operations remain their own protocol slice within this lifecycle.

### 4. Claim partitions, including role-free durable hosts

The three child kinds `dag-child-start-v1`, `dag-child-join-v1`, `dag-child-cancel-v1` have a dedicated runtime pump on **every** durable host, independent of dispatcher or DAG role. All external pumps (both `IMessageDispatcher` and `IWorkflowEventDispatcher`) exclude them and reject wrong-lane records after claim. Wrong-lane work is released, never acknowledged as Dispatched; retry/poison rules must not silently release uncertain admission.

`dag-run-ready-v1` is a separate claim partition included **only** on DAG-role hosts with a registered ready sink. A role-free host must not claim it at all. Missing-role/receiver state retains ready work for a capable host; it is not external delivery or successful acknowledgement. Task 1.1 must freeze filters, fencing, poison outcomes and retry wakeups; DR-037/PR-015 and canonical outbox wording must agree before source.

### 5. Host-owned capacity after restart

The proposed ceiling authority is the current **fenced progression owner**, not a creation-time persisted ceiling. Every admission reapplies that owner's validated host-owned `MaxConcurrentNodes` limit (and any separately approved stricter authored limit). A replacement owner uses its current configuration, reconciling CP-025. Already admitted identities remain durable across ownership changes and count until terminal, including parked children and committed-but-not-yet-materialized starts. A lower replacement limit stops new admissions; it does not cancel existing children, discard admission or pretend the old ceiling remains authoritative.

Only the fenced owner may serialize admission; stale owners or competing hosts with different options cannot independently admit under their own ceilings. Task 1.1 must define the fence and takeover boundary precisely. A full window returns `CapacityDeferred`, leaves the node ready without an input/start commit, discards a speculative mapped value and wakes on a committed capacity/ownership change. It is not terminal cancellation and is not `AdmissionStopped`. The latter is reserved for a cancellation/terminal admission barrier. Retry must not hot-loop through claim/release or poll terminal state.

### 6. Null, identity and bytes

Present-null input is not absent input: the durable bridge commits the four UTF-8 bytes `null`, explicit input presence, its declared type contract and the committed-byte hash. The mapper never invokes a codec. Child start identities are deterministic from complete DAG/run/node/workflow coordinates and exact-bound on collision, not drawn from the caller's global start-key namespace. A repeated matching commit returns the original receipt; a divergent collision is visible and does not overwrite state.

### 7. Document reconciliation belongs to the contract target

Task 1.1 must enumerate precise sections of docs 03/04/05/06/08/10/12/17, implementation decisions/architecture/phased plan, root guides and the Task 8.0 map. In particular: DR-037, PR-015, CP-022/CP-025 and doc 17 §17.2.6. Doc 11 is explicitly excluded unless the contract changes a nonfunctional obligation; otherwise record the exclusion reason. Preserve dated entries and append a new decision. Refresh every edited Task 7.3 source-map row/digest and the Task 8.0 map pin in the same target. Any additional requirement owner discovered in this audit needs a reviewed process extension, not a silent canonical edit.

## Risks / Trade-offs

- [New public identity/advanced ports] → Freeze exact signatures and baseline/schema impact at 1.1; no source while a proposed shape is unresolved.
- [Separate run/child streams] → Persist start intent first; deterministic child reattachment and terminal join must recover every ambiguous boundary.
- [Changing host limits] → Fence one admission owner, retain admitted identities, lower limits stop only new admission. Test competing hosts and restart before source approval.
- [Role-free host delivery] → Child pump is always installed, ready claim is role-filtered, external pumps fail closed on all four kinds.
- [Historical records acquire stale provisional wording] → Preserve immutable approved artifacts and document the successor's correction; never repin them to make a new design look historically approved.

## Migration Plan

Process approval/checkpoint/evidence/0.2 activation → contract 1.1/1.2 → atomic canonical/registry 1.3/1.4 review → source/provider slices → implementation approval/Complete. The current process target leaves reshape 8.4/8.5 open. Any future archive is a separate reviewed provenance refreeze. Rollback before approval is withdrawal through the reviewed process in Decision 1, not deletion of provenance.

## Open Questions

No source implementation decision is delegated to this process gate. Task 1.1 must settle the exact public stream identity and provider port/schema signatures, lifecycle result/fence types, notification deduplication and terminal fanout before contract approval. The proposed direction above addresses the five notes, but does not authorize these unsettled APIs or their implementation.
