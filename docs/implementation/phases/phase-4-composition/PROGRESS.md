# Phase 4 Progress

T4-00 | done | 2026-07-02 | deviations: generated task files use current implementation paths per this run's workspace override
T4-01 | done | 2026-07-02 | deviations: partitioner helpers are public Core definition contracts for later builder use
T4-02 | done | 2026-07-02 | deviations: ForEach body uses the existing TState step context; item data is observable through work-item snapshots
T4-03 | done | 2026-07-02 | deviations: WaitAllThenFail defers item failures inside ForEach while preserving fail-fast behavior outside ForEach
T4-04 | done | 2026-07-02 | deviations: lineage query support is exposed through projection filters; child start authoring remains out of scope
T4-05 | done | 2026-07-02 | deviations: RunChild is exposed through durable command processor metadata commands; authoring API remains out of scope
T4-06 | done | 2026-07-02 | deviations: RunChildren materializes the initial child-start window only; later throttled dispatch remains out of scope
T4-07 | done | 2026-07-02 | deviations: durable throttling is represented on the persisted group materialization event
T4-08 | done | 2026-07-02 | deviations: resume tokens are deterministic from child group ids and recorded as durable events
T4-09 | done | 2026-07-02 | deviations: residual cancellation intent emits external-message outbox records; compensation remains out of scope
