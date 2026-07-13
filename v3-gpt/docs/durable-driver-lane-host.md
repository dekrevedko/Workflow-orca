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
