# R1 — Abstractions / Contracts — Findings

> **Baseline:** `dotnet build OrcaCore.slnx -warnaserror` → passed, 0 warnings (2026-07-03,
> working tree on `feature/v3-rebuild` incl. uncommitted remediation changes).
> Full `dotnet test OrcaCore.slnx` → 892 passed / 1 failed (flaky hosting test, see
> SUMMARY) / 16 skipped, all container suites executed against Docker.
> Scope: `src/OrcaCore.Abstractions/**` against 03 (glossary), 04 CR-011/013/015/020/021/022,
> 05 EV-001/002/021, 06 DU-011/012, 10 PR-010…016/PR-050.

## Findings

### [P2] Durable event type names derive from CLR type names — rename breaks stored streams — `src/OrcaCore.Abstractions/Serialization/WorkflowEventCodec.cs:115`
> **FIXED 2026-07-03:** explicit string discriminators per entry (pinned to current names),
> frozen-table guard test `WorkflowEventCodecTests.EventTypeNames_AreFrozenStreamDiscriminators`.
- **Requirement/convention:** DU-011/DU-012 (durable engine facts must replay from committed streams); codec doc-comment itself promises "stable provider event type names"
- **Evidence:** `WorkflowEventCodecEntry<TEvent>(...) : WorkflowEventCodecEntry(typeof(TEvent), typeof(TEvent).Name)` — the persisted `event_type` discriminator is the CLR class name.
- **Failure scenario:** Any future rename of a `WorkflowEvent` subclass (e.g. `WorkflowChildCompletedEvent` → `WorkflowChildFinishedEvent`) silently changes the stored discriminator; existing streams then throw `InvalidOperationException("Workflow event type '…' is not supported.")` on rehydration — instance is unrecoverable without data migration.
- **Recommendation:** Give each codec entry an explicit string constant (`Entry(context.X, "workflow.child.completed.v1")`) and add a repository-guard test asserting the name table never changes for existing entries. Small change; the descriptor table already centralizes the mapping.
- **Confidence:** CONFIRMED (traced serialize/deserialize path; no alias mechanism exists)

### [P2] CR-022 monotonic version/epoch absent from the public instance surface — `src/OrcaCore.Abstractions/Instances/WorkflowInstanceSnapshot.cs:8`
> **FIXED 2026-07-03:** `WorkflowInstanceSnapshot.StreamVersion` added, populated by the durable
> aggregate and persisted by all projection stores (PG migration 004, SQL Server migration 005);
> certification-enforced round-trip. Ephemeral snapshots report null by design.
- **Requirement/convention:** CR-022 ("Instances SHALL carry … a monotonic version/epoch for optimistic concurrency"); carried from R3 coverage note, never resolved
- **Evidence:** `WorkflowInstanceSnapshot` exposes identity, status, timestamps, waits, outcomes — but no stream version/epoch and no `StartIdempotencyKey`. Durable code tracks `StreamVersion` internally (aggregate/checkpoint) but does not surface it; ephemeral instances have no epoch at all.
- **Failure scenario:** An operator tool (or future management compare-and-act command, MG-xxx) cannot express "act only if the instance hasn't changed since I read it" — no optimistic-concurrency token crosses the API boundary.
- **Recommendation:** Add `StreamVersion`/`Epoch` (and optional `StartIdempotencyKey`) to the snapshot, populated by durable projections; ephemeral can expose its per-instance mutation counter. Additive, no breaking change.
- **Confidence:** CONFIRMED

### [P3] Legacy + lease claim overload pairs on outbox/timer ports — `src/OrcaCore.Abstractions/Providers/ProviderPorts.cs:77`
> **DEFERRED 2026-07-03:** dozens of test call sites use the short overload and a default-interface
> forward would need a wall-clock (banned by the repository guard). Certification covers lease
> semantics; revisit only if a provider ships legacy-only claims.
- **Requirement/convention:** none — general API design (R0 lens 6: closed, minimal port surface)
- **Evidence:** `IWorkflowOutboxStore.ClaimAsync(int maxCount, …)` coexists with `ClaimAsync(OutboxClaimRequest, …)`; same for `ITimerScheduler.ClaimDueAsync` (lines 173/181).
- **Failure scenario:** A provider implementing only the legacy overload silently loses lease-recovery semantics; two entry points to certify per port.
- **Recommendation:** Delete the non-lease overloads (all shipped providers already implement the lease form) or default-interface-forward legacy → lease with an infinite lease.
- **Confidence:** CONFIRMED

### [P3] `EventEnvelope.Payload` is `object?` on the shared surface while the durable boundary uses `byte[]` — `src/OrcaCore.Abstractions/Events/EventEnvelope.cs:33`
> **FIXED 2026-07-03:** convention documented on the property.
- **Requirement/convention:** EV-001 (canonical envelope), PR-016 (serialization ports)
- **Evidence:** Ephemeral delivery carries the live `object?`; durable events carry `byte[] Payload` (`WorkflowEvent.cs:372`) via `IWorkflowPayloadSerializer`. The dual convention is real but undocumented on the envelope.
- **Failure scenario:** An author assumes payload reference identity survives a durable wait; it does not (round-trips through the payload serializer). Confusion risk only — the closed source-gen serializer context means there is no polymorphic-deserialization hole (NF-040 verified).
- **Recommendation:** One doc-comment sentence on `Payload` stating the ephemeral-live / durable-serialized split.
- **Confidence:** CONFIRMED

## Coverage note

Verified: CR-011 (closed `StepResult` family, sealed records), CR-012 (StepContext exposes state +
resumed event + `TimeProvider` only — no runtime surface), CR-013 (`ValueTask` + `CancellationToken`
on `IStep`), CR-020/021 (snapshot-only instance surface, no live internals), EV-001/002 (envelope
with `EventId`/`EventName`/`CorrelationId`/`BranchId`), PR-010…016 (all seven ports present as
single-purpose interfaces), PR-050 (`Result`/`Option`/`Validation` primitives; `default(Result<T>)`
failure case handled explicitly at `Result.cs:49`), NF-040 (STJ **source-generated** closed
serializer context `OrcaCoreJsonSerializerContext` — no untrusted polymorphic type resolution;
stack-decision "STJ source generators" now satisfied, closing the R8 finding). Strongly-typed ids
use `Guid.CreateVersion7()` (closing the R8 v7-id finding for ids; ephemeral timer tokens not
re-checked here). Not reached: DU-020…033 behavioral semantics (R4 scope), EV-021 wait-record
runtime behavior (R3/R4 scope).
