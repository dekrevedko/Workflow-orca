# Phase 5 Progress

T5-00 | blocked | 2026-07-02 | blocker: spec open questions 12 (saga track order) and 13 (ephemeral saga depth) remain unresolved in docs/specs/13-phasing-and-open-questions.md and are not resolved in docs/implementation/00-stack-decisions.md; cannot expand saga task files without an explicit resolution
T5-00 | blocked | 2026-07-02 | update: spec open question 13 resolved to in-process-only ephemeral saga with required XML docs/docs labeling; spec open question 12 remains unresolved
T5-00 | resumed | 2026-07-02 | update: spec open question 12 resolved to compensation-heavy saga first; also recorded resolutions for spec open questions 1, 2, and 4
T5-00 | done | 2026-07-02 | deviations: task files use current implementation paths; spec open questions 1, 2, 4, 12, and 13 are resolved in spec and decision log before Phase 5 implementation
T5-01 | done | 2026-07-02 | deviations: compensation command/event contracts are provider-neutral placeholders; runtime decisions and provider serializers remain later Phase 5 tasks
T5-02 | done | 2026-07-02 | deviations: saga definition metadata is compile-time only; runtime execution and durable decision processing remain later tasks
T5-03 | done | 2026-07-02 | deviations: compensation planning records requested/started durable facts; compensation action completion/failure and provider serializer coverage remain later tasks
T5-04 | done | 2026-07-02 | deviations: compensation completion/failure terminal decisions are command-level durable facts; manual recovery remains T5-06
T5-05 | done | 2026-07-02 | deviations: timeout uses explicit CompensateScope policy on the durable timeout command; cancellation remains non-compensating
T5-06 | done | 2026-07-02 | deviations: saga audit is projected on WorkflowInstanceSnapshot with provider-specific PostgreSQL JSON projection coverage; checkpointed saga audit state still relies on replaying tail saga facts after checkpoint
T5-07 | done | 2026-07-02 | deviations: child compensation is durable-command only and enqueues child-compensation-start outbox records; completed-child compensation eligibility is replayed from child completion facts after checkpoint
T5-08 | done | 2026-07-02 | deviations: ephemeral saga exposes in-process-only StartSagaAsync and RequestSagaCompensationAsync APIs with no durable recovery, durable audit, or post-restart operator remediation guarantees
