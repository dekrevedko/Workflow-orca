# Phase 2 Progress

T2-00 | done | 2026-07-02 | deviations: generated task files use the v3-gpt path override; execution must pause for expansion review before T2-01
T2-01 | done | 2026-07-02 | deviations: none
T2-02 | done | 2026-07-02 | deviations: resolved IOQ-3 to same-commit-boundary projection writes
T2-03 | done | 2026-07-02 | deviations: none
T2-04 | done | 2026-07-02 | deviations: none
T2-05 | done | 2026-07-02 | deviations: none
T2-06 | done | 2026-07-02 | deviations: added checkpoint load to IWorkflowEventStore because the command pipeline must rehydrate from checkpoint plus stream tail
T2-07 | done | 2026-07-02 | deviations: checkpoint writes now carry durable aggregate metadata needed for checkpoint-only recovery
T2-08 | done | 2026-07-02 | deviations: durable WaitLong is represented by a durable-only builder surface and command-level cold wait registration rather than the core builder
T2-09 | done | 2026-07-02 | deviations: poisoned delivery metadata is exposed through the command result message while the provider inbox stores the stable Poisoned state
T2-10 | done | 2026-07-02 | deviations: none; IOQ-2 remains deferred to Phase 2 exit and the pump uses Channels only
T2-11 | done | 2026-07-02 | deviations: StartOrGet idempotency is implemented in the durable start service for this slice; provider-backed key indexing remains future provider work
T2-12 | done | 2026-07-02 | deviations: retry/delete/purge baseline surface remains deferred; this slice implements the specified AC-512 through AC-515 and AC-517 pause/resume buffering and discard audit tests
