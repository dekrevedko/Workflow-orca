# Phase 4b Progress

> **Superseded historical evidence (2026-07-18):** these completion records describe the
> provisional implementation that existed before the v1 simplification. They are not approval
> of their API names or semantics and must not be used as executable tasks. Current authority is
> [document 17](../../../specs/17-selected-mode-capability-matrix.md) and the active
> `reshape-developer-facing-interfaces` change. In particular, public `RunExternalJob` and
> `RunChildren` are deferred, `WaitLong` is removed, and force/time-based lease release is forbidden.

T4B-00 | done | 2026-07-02 | deviations: resolved spec open question 15 to child-instance-per-node via RunChildren and generated task files with current implementation paths per this run's workspace override
T4B-01 | done | 2026-07-02 | deviations: PostgreSQL resource-pool store is a separate provider-owned port implementation; commits deferred per user instruction
T4B-02 | done | 2026-07-02 | deviations: cross-instance grant wake-up is represented through pool-store grant results; durable acquisition records cold waits and terminal release facts
T4B-03 | done | 2026-07-02 | deviations: expiry is explicit scan-driven and audible; force-release is the operator path that reclaims capacity
T4B-04 | done | 2026-07-02 | deviations: RunExternalJob is exposed as durable command/event/outbox contracts; real dispatcher adapters remain out of scope
T4B-05 | done | 2026-07-02 | deviations: Core DAG compile output is a provider-neutral child batch to preserve project boundaries; durable acceptance adapts it to RunChildren
T4B-06 | done | 2026-07-02 | deviations: DAG reconstruction uses root RunChildren history plus child projections; cancellation records external stop intent before terminal Cancelled
Phase 4b exit | done | 2026-07-02 | deviations: exit AC filter and zero-warning build passed; project-reference architecture check matches documented boundaries
