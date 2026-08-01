# Phase 3 Progress

T3-00 | done | 2026-07-02 | deviations: IOQ-5 resolved to BCL diagnostics in core/engines/providers with OpenTelemetry SDK wiring deferred to Hosting; T3-02 split into contract/decision and provider implementation tasks to fit task sizing
T3-01 | done | 2026-07-02 | deviations: none
T3-02 | done | 2026-07-02 | deviations: ephemeral delay uses TimeProvider-driven transient timers and makes no durability claim
T3-03 | done | 2026-07-02 | deviations: durable timer support is command/event-level only; provider scheduling remains T3-04
T3-04 | done | 2026-07-02 | deviations: timer scheduler certification covers InMemory and PostgreSQL; SQL Server coverage was added later with its provider slice
T3-05 | done | 2026-07-02 | deviations: timer/event loser handling is deterministic and observable through runtime state rather than a separate public race policy type
T3-06 | done | 2026-07-02 | deviations: spec open question 6 resolved to fluent builder calls plus explicit metadata objects; no attributes or reflection
T3-07 | done | 2026-07-02 | deviations: baseline timeout enforcement fails the instance on timeout; saga compensation hooks remain Phase 5 scope
T3-08 | done | 2026-07-02 | deviations: retry enforcement is immediate bounded retry for the covered baseline; delayed durable retry scheduling remains tied to durable timer capabilities
T3-09 | done | 2026-07-02 | deviations: LetRemainingComplete records the deterministic winner immediately and waits for residual branches to complete before parent continuation in the in-instance ephemeral runtime
T3-10 | done | 2026-07-02 | deviations: Durable lifecycle publication starts with outbox-backed lifecycle-event records derived from committed workflow facts; ephemeral lifecycle events are queryable in-process only
T3-11 | done | 2026-07-02 | deviations: Ephemeral stuck-step detection is evaluated from TimeProvider-controlled execution duration; stuck-instance detection is an explicit management evaluation over last-progress metadata
T3-12 | done | 2026-07-02 | deviations: Pressure metrics cover provider-owned stream event count, checkpoint count, pending/retryable outbox count, and active projected instance count
T3-13 | done | 2026-07-02 | deviations: In-process governance uses process-local semaphores for advancement, global step execution, and configured named pools; durable ticket pools remain Phase 4b scope
