# Phase 6 Progress

T6-00 | blocked | 2026-07-02 | blocker: IOQ-8 and IOQ-9 remain open in docs/implementation/00-stack-decisions.md; T6-00 requires owner sequencing for BenchmarkDotNet hot paths/CI treatment and public packaging IDs/signing/SourceLink/README policy before expanding Phase 6 task files
T6-00 | blocked | 2026-07-02 | update: spec open question 7 resolved to compile-time mode separation as far as practical without doubling every abstraction; IOQ-8 and IOQ-9 remain unresolved blockers for Phase 6 expansion
T6-00 | done | 2026-07-02 | deviations: public packaging deferred by owner decision; no product tests because task-expansion only
T6-01 | done | 2026-07-02 | deviations: ran dotnet commands from current implementation because root global.json requests unavailable SDK 10.0.200
T6-02 | done | 2026-07-02 | deviations: ran dotnet commands from current implementation because root global.json requests unavailable SDK 10.0.200
T6-03 | done | 2026-07-02 | deviations: hosted-service adapters are no-op lifecycle shells until pump/timer/sweep loops are scheduled by later tasks
T6-04 | blocked | 2026-07-02 | blocker: spec open question 10 remains open; continue-as-new timing must be resolved to Slice 6 before T6-04 can implement DU-042/AC-313
T6-04 | resumed | 2026-07-02 | spec open question 10 resolved to early/Slice 2; T6-04 proceeds as corrective backfill for missing DU-042
T6-04 | done | 2026-07-02 | deviations: provider-specific persistence/projection and retention policy remain out of scope for T6-05+; ran dotnet commands from current implementation because root global.json requests unavailable SDK 10.0.200
T6-05 | done | 2026-07-02 | deviations: ran dotnet commands from current implementation because root global.json requests unavailable SDK 10.0.200
T6-06 | done | 2026-07-02 | deviations: no current implementation provider README files existed to update; ran dotnet commands from current implementation because root global.json requests unavailable SDK 10.0.200
T6-07 | done | 2026-07-02 | deviations: Redis projection store uses an in-process projection cache profile for certification without a live Redis server; ran dotnet commands from current implementation because root global.json requests unavailable SDK 10.0.200
T6-08 | done | 2026-07-02 | deviations: SQL Server slice opens a real container connection but keeps the certified event/checkpoint/inbox/outbox state in process pending full SQL-backed projections/timers in T6-09; ran dotnet commands from current implementation because root global.json requests unavailable SDK 10.0.200
T6-09 | done | 2026-07-02 | deviations: SQL Server projections/timers/resource pools are certification-backed in-process stores behind the SQL Server provider shell, not SQL table-backed persistence; ran dotnet commands from current implementation because root global.json requests unavailable SDK 10.0.200
T6-10 | blocked | 2026-07-02 | blocker: IOQ-10 remains open; DynamoDB single-table event-stream design requires owner resolution before task split or provider work
T6-10 | done | 2026-07-02 | deviations: owner resolved IOQ-10 by deferring DynamoDB implementation and preserving provider-port compatibility only; no current implementation code or product tests
T6-11 | done | 2026-07-02 | deviations: none beyond running dotnet commands from current implementation because root global.json requests unavailable SDK 10.0.200
T6-12 | done | 2026-07-02 | deviations: benchmark scenario implementation intentionally deferred to follow-up tasks; ran dotnet commands from current implementation because root global.json requests unavailable SDK 10.0.200
T6-13 | done | 2026-07-02 | deviations: benchmark scenarios use deterministic in-process providers only; PostgreSQL/container benchmark profiles remain out of scope; smoke run used BenchmarkDotNet Dry job
T6-14 | done | 2026-07-02 | deviations: public packaging remains deferred; sample host uses in-memory providers only and is smoke-tested through DI resolution
T6-15 | done | 2026-07-02 | deviations: docs-only handoff; no public XML docs changed because StartOrGet remains internal in current implementation
T6-16 | not pursued | 2026-07-02 | owner has not explicitly chosen multi-node execution for this run; AC-315 remains out of scope and single-host expected-version correctness is unchanged
