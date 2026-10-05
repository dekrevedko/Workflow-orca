## Why

The approved Task 8.4 contract defines only the six-operation durable DAG child protocol. A run cannot yet be created, recovered, cancelled or terminalized through that seam, and the approved Section 8 map forbids bridge source until a separate run-persistence/progression contract and its canonical amendment are reviewed.

## What Changes

- Prepare a **process-only successor gate** for four exact existing requirement headings. The predecessor blocks remain unchanged; the proposed successors remain pending, not canonically approved. This target does not approve the proposed contract or authorize source.
- Propose first-class DAG run persistence, a fenced run-progression owner, notification-driven recovery and terminal observation, and explicit readiness/mapping-failure/cancellation/child-join transitions.
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

This freeze changes planning artifacts and test-process provenance only. `src/**`, canonical `openspec/specs/**`, approved child-contract artifacts, and existing dated decisions are outside its mutation scope. Independent process approval/checkpoint/evidence is required before Task 1.1 contract work; independent contract approval and atomic canonical/registry sync follow before any bridge source.
