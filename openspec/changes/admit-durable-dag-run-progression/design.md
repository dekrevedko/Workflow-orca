## Context

Task 8.4's independently approved child-protocol checkpoint is `1d9672a51ed6f2770dcf7023d13a915a9b135571` (tree `4b83f4d9a5cf2ecaf8d3dc2e60b6e1c56e6c02e2`); its direct-child evidence commit is `095bbfd1a6b559f8f1be47fb8d4bcd4749d40854`. The verdict is `developer-facing-interface-section-08-task-8-4-durable-dag-bridge-contract-br-remediation-independent-review-verdict-2026-10-04.md`, SHA-256 `2fe0cac10719ab0aa9e6321461fb4b9cb5d63504a0436585dc66f51b32375557`. There is no activation: reshape 8.4 and 8.5 remain open.

That approval covers the six-operation child protocol, not a complete DAG runtime seam. Its section 10 requires a run-persistence/progression contract and canonical amendment before source. The prerequisite process gate is checkpoint b55056c0d9136f509296167ae5984881e7b5de97, evidence e99f338d9319b9af5459f5685c69585493773d7c and activation dab498ff54321d802b36ce052100954bbe00487d. This is its full Task 1.1 contract proposal, not approval. The old approved and rejected artifacts remain immutable, including their explicitly provisional section 10 assumptions. The five new review notes are addressed as proposed decisions here and must be independently judged in Task 1.1/1.2.

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

Each full delta preserves the canonical statement/scenarios byte-exact, inserts its new SHALL text ABOVE the first scenario, then adds behavioral scenarios for each new obligation group. The process registry pins both blocks and their operations; only those four headings may coexist under reshape and this change. A third owner, undeclared heading, stage change, early sync or altered block fails closed. Task 0.2 is deliberately not pinned open, so evidence plus checkbox-only activation does not require a red intermediate commit. Task 1.1 is prepared; 1.2 stays unpinned and 1.3–3.2 remain open.

Task 1.1 may revise these proposed blocks only in a new uncommitted reviewed target, with coherent source/fixture hash updates and a new provenance artifact. Approval at 1.2 must be bound to a unique terminal APPROVE verdict, the reviewed tree, and a single-parent evidence commit that adds that verdict. Tasks 1.3/1.4 are atomic: sync the exact approved blocks and promote their registry to ApprovedPending while retaining the proposed rows and hash-exact predecessor history. Source cannot begin before that transition's independent approval/checkpoint. Complete needs separate implementation evidence.

Withdrawal requires one reviewed target that removes all four proposed rows and the change directory together, preserves proposal/review history in an immutable recorded disposition, and restores measured provenance. Archival resolution must accept one active or dated archived record, fail on duplicates, and must not require an unarchived delta forever. These transitions are requirements for later gates, not implemented here.

### 2. Real commit port and an explicit identity amendment

There is no `IWorkflowCommitStore`. The existing public advanced port is `IWorkflowEventStore.AppendAsync(ProviderCommitBatch)`. The batch's event, checkpoint, projection and idempotency shapes are currently workflow-oriented. `WorkflowStreamId` in `OrcaCore` exposes `InstanceId` and renders `workflow-{id}`; it cannot honestly name `dag-run-{lowercase-D-guid}` today.

Task 1.1 must freeze an exact typed stream/record identity proposal and all required port changes. It must enumerate the application/advanced public baseline changes, `WorkflowEvent`/checkpoint framing, fenced discovery and recovery queries, production first-create tables/indexes and provider certification. The proposed `dag-run-v1` aggregate is first-class durable state, not an internal continuation payload masquerading as a workflow instance. Existing workflow stream names and value fingerprints must remain stable. Separate stream commits require deterministic reconciliation, not a false cross-stream transaction promise.

Alternatives rejected: reusing a fake commit-port name; hiding a DAG aggregate behind `InstanceId`; treating new public stream construction as an internal-only change; silently widening provider JSON framing.

### 3. Complete lifecycle before bridge source

Task 1.1 must specify exact operations and closed outcomes for create/reopen, run-input and dependency persistence, read/fenced recovery, ready notification, mapping failure with transitive blocking, mapped-input admission, child-terminal join, run cancellation, fault/poison reconciliation, and run-change/terminal subscriptions. It must define provider-authoritative aggregate ownership, compare-and-append versions, lease/fence checks, deterministic idempotency, restart discovery and atomic related records for each transition.

The one-way package edge stays intact: Durable.Hosting exposes an internal ready-sink registration seam; Dag.Hosting implements it and evaluates mappers. Durable.Hosting must never reference Dag.Hosting or invoke a persisted delegate. The durable bridge eagerly decodes **every** successful declared resultful dependency, including unused outputs, before handing the exact declared map to the evaluator. Null, declared types and mapper exceptions retain the already-approved runtime-view policy. Ordinary declared-type decode failure is DAG_INPUT_MAPPING_INVALID before mapper execution; protocol/storage failures retain runtime classifications. The six approved child operations remain their own protocol slice within this lifecycle.

### 4. Claim partitions, including role-free durable hosts

The three child kinds `dag-child-start-v1`, `dag-child-join-v1`, `dag-child-cancel-v1` have a dedicated runtime pump on **every** durable host, independent of dispatcher or DAG role. The internal IMessageDispatcher port pump and external IWorkflowEventDispatcher pump exclude them and reject wrong-lane records after claim. Wrong-lane work is released, never acknowledged as Dispatched; retry/poison rules must not silently release uncertain admission.

`dag-run-ready-v1` is a separate claim partition included **only** on DAG-role hosts with a registered ready sink. A role-free host must not claim it at all. Missing-role/receiver state retains ready work for a capable host; it is not external delivery or successful acknowledgement. Task 1.1 must freeze filters, fencing, poison outcomes and retry wakeups; DR-037/PR-015 and canonical outbox wording must agree before source.

### 5. Bounded FIFO turns and per-turn host-owned capacity (RC-1)

Dag.Hosting owns admission/mapping coordination; Durable.Hosting owns the lease
broker, all provider calls and the role-independent join/cancellation coordinator.
Both acquire through one provider-persisted FIFO, never a reverse package call.
The exact internal Begin/Renew/End/Abandon/turn-bound child adapter signatures are
in the successor contract. No Dag.Hosting provider-port bypass is permitted.

A turn admits at most one atomic provider mutation, with the existing 5s lease and
15s hard cap; release precedes idle/capacity/child waits. Eager decode, user mapping
and normalization run OUTSIDE the local/provider queue. Each node/host has one
physical evaluation, retained until return in its isolated per-run lane. The distinct
internal DagEvaluationParallelismPerRun=1 is NEVER a host-global MaxConcurrentNodes
pool. Round-robin lightweight dispatch never waits on a mapper; free-window selection
and a fenced lowest-Ready-ordinal check preserve authored admission. Zero look-ahead
and one evaluation OR receipt per run/host bound speculation. Completed immutable
receipts survive unrelated version conflicts
and capacity deferral; only short authoritative recheck-and-append takes a turn.
There is no mapper duration budget, lease-based mapping failure or abandoned-worker
restart. Local mixed-purpose FIFO precedes the provider FIFO, with exact OwnerBusy/
LocalQueueFull outcomes and additive predecessor bounds. DG-P19 proves a slow valid
mapper commits exactly once on real storage while joins/cancellation still progress.
The new contract defines production DB clocks/internal fixture clocks, exact pressure
properties/gauges, explicit captured host-limit argument and StartAuthorized-only
persisted node state. Public cancellation originates on a DAG-role handle; role-free
hosts consume committed child cancellation/join work. DG-P20/21 cover these notes.

Capacity authority is the local Dag.Hosting owner of EACH granted admission turn,
not a long-lived elected host or creation-time ceiling. Different hosts' validated
limits may apply in successive turns but never concurrently. StartAuthorized and
parked admissions remain counted after release/takeover. A lower next-turn limit
stops new admission without cancel/forgetting old children. CapacityDeferred commits
no input/start and releases before awaiting change; AdmissionStopped is only the
cancellation/terminal barrier. This explicitly replaces the rejected target's
undefined lifetime-owner assumption, preserving CP-024/025/028 behavior.

Alternatives rejected: indefinite owner renewal (starves remote join/cancel),
Dag.Hosting direct provider acquisition (second storage seam), unfenced join facts
(contradicts serialized state), arbitrary remote options callbacks (reverse edge).

### 6. Null, identity and bytes

Present-null input is not absent input: the durable bridge commits the four UTF-8 bytes `null`, explicit input presence, its declared type contract and the committed-byte hash. The mapper never invokes a codec. Child identities hash ONLY run GUID and ordinal by the approved recipe; workflow/version/fingerprint/types/input bytes bind SEPARATELY on collision, not drawn from the caller's global start-key namespace. A repeated matching commit returns the original receipt; a divergent collision is visible and does not overwrite state.

### 7. Document reconciliation belongs to the contract target

Task 1.1 must enumerate precise sections of docs 03/04/05/06/08/10/12/17, implementation decisions/architecture/phased plan, root guides and the Task 8.0 map. In particular: DR-037, PR-015, CP-022/CP-025 and doc 17 §17.2.6. Doc 11 is explicitly excluded unless the contract changes a nonfunctional obligation; otherwise record the exclusion reason. Preserve dated entries and append a new decision. Refresh every edited Task 7.3 source-map row/digest and the Task 8.0 map pin in the same target. Any additional requirement owner discovered in this audit needs a reviewed process extension, not a silent canonical edit.

## Risks / Trade-offs

- [New public identity/advanced ports] → Freeze exact signatures and baseline/schema impact at 1.1; no source while a proposed shape is unresolved.
- [Separate run/child streams] → Persist start intent first; deterministic child reattachment and terminal join must recover every ambiguous boundary.
- [Changing host limits] → Fence short commit turns, evaluate outside ownership, retain admitted identities, lower limits stop only new admission. Test competing hosts and restart before source approval.
- [Role-free host delivery] → Child pump is always installed, ready claim is role-filtered, external pumps fail closed on all four kinds.
- [Historical records acquire stale provisional wording] → Preserve immutable approved artifacts and document the successor's correction; never repin them to make a new design look historically approved.

## Migration Plan

Process approval/checkpoint/evidence/0.2 activation → contract 1.1/1.2 → atomic canonical/registry 1.3/1.4 review → source/provider slices → implementation approval/Complete. The current process target leaves reshape 8.4/8.5 open. Any future archive is a separate reviewed provenance refreeze. Rollback before approval is withdrawal through the reviewed process in Decision 1, not deletion of provenance.

## Exact settled Task 1.1 contract

The [complete contract](artifacts/task-1-1-run-contract-rc-3-remediation-2026-10-05.md) and its
[exact API catalog](artifacts/task-1-1-exact-api-additions-rc-3-remediation-2026-10-05.md) freeze
all proposed signatures, outcomes, lease/version checks, discovery, protocol frames,
notifications, first-create schema and certification. These and proposal/design/tasks
are whole-file source-pinned; routine fixture refresh cannot change the decision.

### 8. Supersession, withdrawal and archival owner

Atomic 1.3/1.4 owns the four reviewed predecessor supersessions and one active-or-
dated-archived record resolution; duplicates fail closed. Withdrawal removes all
four rows and the change directory TOGETHER in one separately reviewed target,
retaining immutable disposition and evidence. Actual archival is a separate target.

### 9. Review observations: behavior, observers and retained storage

RC-1 and P3-1/P3-2/P3-3 are addressed in one proposal: behavioral scenarios replace
the repeated process-only scenario; dead boot-host observer notices expire/purge
without deleting progression/child truth; compact node-change events plus one
overwritten full checkpoint avoid retained O(nodes*transitions) snapshots, while
honestly retaining full-checkpoint write amplification. Six first-create tables
now include operational FIFO lease requests. The rejected artifacts and packet
stay byte-exact and source/history pinned. No fifth requirement owner is added.

### 10. RC-2 and round-96 dispositions (2026-10-05)

The second rejected packet and all its artifacts stay byte-exact. Mapping never owns a turn; prepared bytes may commit under fresh short fences after revalidation. All six observations are resolved by the new exact signatures/protocol and DG-P19–21, not by source implementation. Only the same four requirement headings are proposed.

### 11. RC-3 isolated lanes, ordinal admission and ready-hint recovery (2026-10-05)

The third rejected freeze is preserved. Section 4.2 of the successor is exact:
one dedicated evaluation lane per run/host, internal bound one, no shared bounded
host-global user-code permit and no mapper on broker/notification workers. Fair
round-robin dispatch isolates healthy runs from another run's stuck mapper; total
threads/captured values scale with evaluated live or retired runs, NOT a universal
process-wide cap. A stuck cancelled mapper retains its own thread until return;
infinite runs/resource exhaustion have no progress promise. This is an explicit
serial-throughput/resource trade-off, not a public option or friend change.

F=max(0,current local child ceiling-admitted nonterminal children/start intents).
Only the lowest Ready ordinal within the first F candidates may be evaluated,
with ZERO look-ahead and one evaluation OR receipt plus one coalesced admission
request per run/host. Serialized successful commit requires F>0, fewer lower Ready
nodes than F AND no lower Ready node (the stronger serial authored tie-break).
CapacityDeferred covers unavailable capacity/ordinal window; immutable receipts
survive that deferral. A newly Ready lower node explicitly invalidates a higher
cached receipt so a single host can map it without a two-receipt deadlock; this
alone allows later precommit reevaluation, never overlapping abandoned workers.
Cross-host duplication is bounded one/run/boot host and acknowledged, not exactly once.

Non-success ChildJoined transitively blocks Pending/Ready descendants in its one
append; success alone readies eligible dependants. Ready hints acknowledge bounded
local wake acceptance BEFORE mapping. Continuous authoritative nonterminal-run
discovery wraps every full pass, with <=5s between passes, recovering acked/lost/
poisoned hints; claims never live across a mapper. DG-P22–24 require actual provider
ordering/isolation/death evidence later. Docs 08/12/17 disposition the distinct
limit, tie-break, duplicates and recovery. No fifth requirement owner is added.

## Open Questions

None within this four-heading contract target. Independent review can reject
these exact proposed choices. No canonical sync, source or baseline update is
authorized until contract and atomic 1.3/1.4 approval/checkpoint.

## Atomic canonical transition preparation (2026-10-05)

The preceding proposed decisions are retained as contract history. RC-3 contract
approval is checkpoint e6802a3 / evidence 578593e; the new
[transition addendum](artifacts/task-1-3-1-4-rc-3-transition-addendum-2026-10-05.md) owns the four review
observations and exact lifecycle policies. Four ApprovedPending rows and four
reshape supersessions now bind the canonical target, with Proposed hashes retained.
One-word mandatory discard is the only delta refinement; all RC-3 packet files
stay byte-exact. 1.3 is prepared complete, 1.4 activation remains unpinned, and
source/archival/withdrawal require their separate reviews.

### 13. First Task 2.1 source slice (2026-10-07)

The canonical transition is independently approved at checkpoint 1770b2b (tree 61b5900c), with verdict e89e7066, direct-child evidence 18bf865 and activation 89e3b18. This bounded uncommitted slice implements only the seven typed identity API additions. Workflow-only append/tail methods reject non-workflow/default identities before reading or mutating storage; DAG storage and its remaining 358 API keys stay pending. The full 2.1 task remains open, as do all pump, bridge and closeout tasks.

Carry-forward evidence binds the actual canonical transition chain and pins every canonical file, including unowned requirement blocks, with a source-pinned aggregate digest. Any later canonical refresh requires an explicitly reviewed mechanical delta sync; routine provenance/pin maintenance cannot silently approve changed canonical text. The new provenance artifact corrects the preamble recipe without changing the approved predecessor. Archival-resolver tightening remains a separate archival review.
