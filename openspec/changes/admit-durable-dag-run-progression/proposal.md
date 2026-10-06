## Why

The approved Task 8.4 contract defines only the six-operation durable DAG child protocol. A run cannot yet be created, recovered, cancelled or terminalized through that seam, and the approved Section 8 map forbids bridge source until a separate run-persistence/progression contract and its canonical amendment are reviewed.

## What Changes

- Refine the independently checkpointed four-heading process gate into the complete Task 1.1 contract proposal. New SHALL obligations belong above the first scenario; canonical and predecessors remain unchanged, and successors remain pending, not approved.
- Propose first-class DAG run persistence, bounded provider-FIFO mutation turns with a Dag.Hosting admission coordinator and Durable.Hosting lease/join/cancel broker, notification-driven recovery and terminal observation, and explicit readiness/mapping-failure/cancellation/child-join transitions.
- Propose dedicated runtime routing for `dag-child-start-v1`, `dag-child-join-v1` and `dag-child-cancel-v1` on every durable host; reserve `dag-run-ready-v1` for DAG-role hosts. Neither external dispatcher may claim or acknowledge these records.
- **BREAKING, proposed:** explicitly amend stream/record identity and provider boundaries for a `dag-run-v1` aggregate. Today's `WorkflowStreamId` renders only `workflow-{id}`; a `dag-run-{guid}` stream is not ordinary port reuse. Task 1.1 must freeze the precise public/advanced API and provider-schema changes before approval.
- Use the real `IWorkflowEventStore.AppendAsync(ProviderCommitBatch)` boundary, not the nonexistent `IWorkflowCommitStore`. Evolve its types/ports explicitly where the run aggregate cannot be represented today; never pretend a cross-stream atomic commit exists.
- Reapply the current fenced owner's host-owned concurrency limit after restart, preserve already admitted children, and distinguish `CapacityDeferred` from `AdmissionStopped`. Persist present-null mapped inputs as explicit fixed-codec bytes.
- Preserve the approved child contract, its rejected predecessor and every review packet byte-exact. Record these five review-note corrections in this new target, not by editing history.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `durable-persistence-and-outbox`: modify exactly "Workflow store persists first-class durable records", "Outbox delivery is adapter-driven and at-least-once", and "First-release provider ownership and schemas are exact" for the proposed DAG aggregate, runtime claim partition and provider parity.
- `durable-runtime`: modify exactly "Durable DAG progression is runtime owned" for the proposed full run lifecycle, fenced admission and recoverable notification direction.

## Impact

The future contract affects `OrcaCore` stream identity, `OrcaCore.Runtime.Protocol`, advanced provider ports, InMemory/PostgreSQL/SQL Server persistence and certification, `OrcaCore.Durable.Hosting` and `OrcaCore.Dag.Hosting`. It must reconcile DR-037, PR-015, CP-022/CP-025 and doc 17 §17.2.6 before source. It adds no friend, codec permission, public application child API or package edge. The current nine friends and durable-owned codec remain unchanged.

This freeze changes planning artifacts, proposed numbered/binding/guide documentation and test-process provenance only. `src/**`, canonical `openspec/specs/**`, approved child-contract artifacts, and existing dated decisions are outside its mutation scope. The process chain b55056c -> e99f338 -> dab498f is landed. Independent contract approval and atomic canonical/registry sync still precede any bridge source.

## RC-1 review remediation (proposed, 2026-10-04)

The rejected contract is retained. Bridge-level lease access, an absolute 15-second
turn cap and FIFO fairness prevent live admission owners starving remote joins or
cancellation. Operational waiter schema/ports are explicitly catalogued, not
smuggled through a public provider bypass. Behavioral scenarios, dead-observer
cleanup and compact event storage address all three P3s. Independent approval is
still required; no source or canonical synchronization is authorized.

## RC-2 review remediation (proposed, 2026-10-05)

Decode, mapping and normalization run outside lease turns. Retained immutable results
commit under short fenced rechecks, with one physical evaluation per node/host and
a per-run evaluation bound (the global worker ceiling in the rejected RC-2
proposal is superseded by RC-3 below). The FIFO lease protocol stays unchanged. Exact pressure,
clock, local arbitration, explicit limit and cancellation/state semantics resolve
all round-96 notes within the same four proposed headings. Both rejected packets
remain immutable; no source/canonical/API baseline change is authorized.

## RC-3 review remediation (proposed, 2026-10-05)

Distinct internal single-worker run lanes, fair round-robin dispatch and no shared
host-global mapper permits isolate healthy runs. MaxConcurrentNodes remains ONLY
the per-run child ceiling. Evaluate the lowest Ready ordinal in the free window,
retain one evaluation OR receipt/run/host with zero look-ahead, and recheck authored
order under the fence before successful admission. Newly Ready lower nodes may
invalidate higher speculative receipts; capacity/time/unrelated versions do not.
Non-success joins block descendants; ready hints ack bounded wake acceptance before
mapping, with continuous authoritative discovery recovering lost/poisoned hints.
DG-P22–24 certify these rules later; the third rejected packet stays byte-exact.
No canonical/source/baseline changes or additional heading owner is authorized.
