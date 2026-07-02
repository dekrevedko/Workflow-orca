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
