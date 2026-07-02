# Phase 2 — Durable Event-Sourced Core + PostgreSQL Plugin (spec Slice 2)

**Goal**: the durable engine per spec §6 (hybrid event-sourced core), all provider ports
with the in-memory reference implementation, the certification suite, and the first real
plugin (PostgreSQL) proving the ports.

**Entry criteria**: Phase 1 exit green.
**Exit criteria**: AC-301…311, AC-314, AC-316, AC-114, AC-504…506, AC-512…515, AC-517 green;
certification suite passes for **both** InMemory and PostgreSql providers; OQ-1/OQ-3 resolved
and logged in 00-stack-decisions.

## Task index (expanded by T2-00)

| Task | Title | Difficulty | Summary |
|------|-------|-----------|---------|
| T2-00 | Expand this index into task files | Sonnet | Per 04-task-protocol §4; split anything oversized |
| T2-01 | Durable contracts: workflow events, commands, stream/version types | Haiku | Event/command catalogs per spec DU-011/012 (closed hierarchies); causation metadata |
| T2-02 | Provider ports in Abstractions | Sonnet | `IWorkflowEventStore`, inbox/outbox/projection stores, `ITimerScheduler`, unit-of-work commit shape (PR-010…016, PR-020). **Resolve OQ-3 here** (projection tx vs chain) |
| T2-03 | Port fakes + certification suite skeleton | Sonnet | `Fake*` stores in TestSupport (failure-injectable); abstract certification classes for append/expected-version, atomic commit, dedup (PR-024) |
| T2-04 | InMemory provider implements all ports | Haiku | Passes certification; becomes the executable port documentation (PR-030) |
| T2-05 | Aggregate + deterministic decision layer | Sonnet | Rebuild from checkpoint+tail; command→events decisions for start/step/complete/fail (DU-011, DU-013, NF-020) |
| T2-06 | Command pipeline + expected-version commit | Sonnet | Per-instance lane (Channels) + optimistic append; conflict → retry/reject policy (CR-041, DU-022); AC-309 |
| T2-07 | Checkpoints + rehydration | Haiku | Save/load, replay tail, crash-restores-committed-only (DU-010/020); AC-301, AC-302, AC-316 |
| T2-08 | Durable waits + `WaitLong` + cold eviction | Sonnet | `WaitStatus`/`WaitMode`, durable builder surface gains `WaitLong`, evict-after-commit, lazy resume (EV-040/041); AC-303, AC-304, AC-504…506 |
| T2-09 | Inbox: restart-safe dedup + transactional consumption | Sonnet | `Received/Applied/DuplicateIgnored/Poisoned` (+`DiscardedOnResume` state), commit-before-ack (DU-030, EV-032); AC-305, AC-114 |
| T2-10 | Outbox: derive-in-commit + pump | Sonnet | Records derived from committed events in same boundary; Channels-based pump: claim → dispatch → mark; retry-delay + observer + poison hooks (DU-031…033); AC-310. **Dataflow decision OQ-2 reviewed at end** |
| T2-11 | Version binding + `StartOrGet` | Haiku | Version as early durable fact; incompatible-change rejection; idempotent start (DU-040/041/053); AC-306, AC-307, AC-311 |
| T2-12 | Durable management: pause/resume (replay & discard), retry, delete/purge/retention baseline | Sonnet | MG-011…013, DU-050/051; AC-512…515, AC-517 |
| T2-13 | Durable query surface over projections | Haiku | Summaries/active-waits/pending-events projections back `Where/List/Count/GetActiveWaits` across hot+cold (DU-070, EV-011); AC-308 |
| T2-14 | PostgreSQL plugin: schema + event/checkpoint stores | Sonnet | **Resolve OQ-1 first** (schema shape); Npgsql, single-tx commit boundary; certification green vs Testcontainers |
| T2-15 | PostgreSQL plugin: inbox/outbox/projections + claim semantics | Sonnet | Row-lock claim (`FOR UPDATE SKIP LOCKED`), retention-safe purge; AC-314 [provider] via certification |

## Phase-wide guardrails

- The ephemeral engine is untouched this phase (except shared Core extractions that keep
  its tests green).
- Every `[provider]`-tagged AC lands in the certification suite, not in a provider's
  private tests.
- No RabbitMQ, no timers (Phase 3), no history-pressure metrics (Phase 3): the outbox pump
  dispatches to `FakeDispatcher`/InMemory dispatcher only.
