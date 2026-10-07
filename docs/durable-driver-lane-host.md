# Durable Driver Lane Host (DR-P2)

How registered durable definitions advance without any caller issuing kernel commands, and
what the multi-host story honestly is. Normative requirements: `docs/specs/16` (DR-030..037).

## Advancement model

- The interpreter runs one **advancement segment** per invocation: from the persisted
  execution position to the next suspension point (wait, timer, external job, queued pool
  acquisition), terminal state, or segment budget (`DurableDriverBudget`, default 256
  commands / 30s). Each kernel command is one atomic commit.
- Every commit that leaves the instance runnable carries an internal **`continue` outbox
  record in the same commit boundary** (DR-034). The committing host usually continues
  in-process immediately; the record is the restart-safety net.
- The **continuation pump** (`DurableContinuationPump`, hosted by
  `OrcaCoreContinuationPumpHostedService`) claims only `continue`-kind records
  (kind-partitioned claims, DR-037), drives the referenced instance, and marks the record
  processed. Stale or duplicate claims are harmless: the driver reloads the committed
  position and no-ops. The external message dispatcher pump claims with `continue`
  excluded and never hands internal records to a transport.
- Repeated drive failures follow the poison path: bounded retries paced by the pump
  interval, then the instance parks (`Parked`, reason `Poison`) with a diagnostic
  (DR-036). Poison parks need an operator re-arm; version-binding parks re-arm by
  registering the missing definition version.

## Multi-host contention model (DR-035, stated honestly)

Multiple lane hosts against one store distribute **independent instances** as competing
consumers over the claim-based pumps (timers, outbox, continuation). Correctness comes
from expected-version append (DU-022): there is no per-instance ownership lease and no
cluster-wide single activation of one instance. Hot contention on a *single* instance
across hosts resolves by conflict-retry — append rejection, reload, retry — and is bounded
by the in-process lane only within one host. Workloads that need cluster-wide single
activation of one instance are the Orleans host's territory (DU-060 direction), not this
host's.

## Shutdown

Graceful drain (DR-033): a stop request ends claiming immediately; the in-flight batch
finishes its current commits inside `ContinuationDrainTimeout` (default 30s) instead of
being torn mid-advancement. Whatever the drained host did not finish, a surviving host's
pump completes via the pending continuation records.

<!-- dag-run-contract-proposal:start -->
#### Approved durable DAG run contract; source pending (2026-10-05)

Propose a role-independent child runtime pump and DAG-role-only readiness. Internal IMessageDispatcher remains disjoint; role-free hosts never claim ready; terminal waits subscribe/recheck.

Full exact independently approved contract: [Task 1.1 RC-3](../openspec/changes/admit-durable-dag-run-progression/artifacts/task-1-1-run-contract-rc-3-remediation-2026-10-05.md), with its exact API catalog. Earlier contract links below are rejected history. Approved RC-3 checkpoint e6802a3 / evidence 578593e; source pending. The four-block canonical transition is prepared for independent review, not yet checkpointed.

Rejected RC-1 predecessor (resolved lease parts retained by RC-3): Dag.Hosting owns admission/mapping coordination; Durable.Hosting owns bridge-only lease access and role-free committed child-join/cancel coordination. Provider FIFO grants one bounded mutation turn (5s lease, 15s absolute cap), then release before idle/child/capacity waits. New arrivals cannot starve prior joins/cancellation. Expired boot-host observer hints have bounded cleanup; compact changed-node events retain one current full checkpoint, not a full snapshot per event. The earlier linked contract is the rejected predecessor; the superseded rejected contract/API catalog was [RC-1 remediation](../openspec/changes/admit-durable-dag-run-progression/artifacts/task-1-1-run-contract-rc-1-remediation-2026-10-04.md). No source, canonical sync or new friend is claimed.

Rejected RC-2 refinement (2026-10-05; superseded by RC-3 below): read/decode/evaluate/normalize immutable inputs outside all lease turns; retain one physical evaluation per node/host and completed receipts across unrelated version conflicts/capacity deferral. Short commit turns only recheck and append. No mapper time budget or expiry failure exists. Exact operational pressure/gauges, provider production/test clocks, same-host FIFO outcomes, explicit captured host-limit parameter, reachable DAG-role cancellation and StartAuthorized-only persisted state were in the rejected RC-2 successor. DG-P19–21 require real-storage evidence before source approval.
RC-3 approved contract (2026-10-05; source pending): MaxConcurrentNodes remains ONLY the PER-RUN child/start-intent ceiling, never a host-global mapper pool. The distinct internal DagEvaluationParallelismPerRun=1 uses dedicated isolated run lanes and fair round-robin dispatch; a stuck mapper retains its own lane, not another run's permit. Evaluate only the lowest Ready ordinal in the free child window, zero look-ahead, one evaluation OR receipt/run/host; successful admission rechecks authored order under the fence, never mapper-completion order. Cross-host evaluation duplicates are allowed one/run/boot host. A newly Ready lower node invalidates a higher speculative receipt to avoid single-host order deadlock; capacity/time/unrelated versions do not. Non-success joins transitively block descendants. Ready hints ack bounded local wake acceptance before mapping and continuous wrapping discovery recovers acked/lost/poisoned hints. DG-P22–24 require real-storage ordering, isolation and wake/death evidence later, not current acceptance credit. Docs 08 CP-025/027, doc 12 AC-609/611/617 and doc 17's distinct-limit taxonomy keep their existing obligations. No public option, new friend or source implementation is claimed; only the four reviewed canonical blocks are prepared for sync.

Transition addendum: [1.3/1.4 P3 dispositions](../openspec/changes/admit-durable-dag-run-progression/artifacts/task-1-3-1-4-rc-3-transition-addendum-2026-10-05.md). Mapping may be evaluated once per run/boot host on competing hosts and after the named precommit invalidation; no exactly-once mapper invocation is promised.

<!-- dag-run-contract-proposal:end -->
