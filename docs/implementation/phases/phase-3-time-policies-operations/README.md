# Phase 3 — Time, Policies, Operations (spec Slice 3)

**Goal**: timers (transient + durable), timeout/retry policies, `WhenFirst`, lifecycle
events, stuck detection, statistics/pressure metrics, resource governance (in-process tier).

**Entry criteria**: Phase 2 exit green.
**Exit criteria**: AC-111…113, AC-204/205, AC-507…511, AC-312 green; OQ-5 (observability)
resolved and logged.

## Task index (expanded by T3-00)

| Task | Title | Difficulty | Summary |
|------|-------|-----------|---------|
| T3-00 | Expand index | Sonnet | Template expansion; resolve OQ-5 with the reviewer first |
| T3-01 | Transient timer service (ephemeral) | Haiku | Min-heap over `TimeProvider` + channel of due firings; `Delay` builder primitive (EV-050 transient tier); AC-111 (ephemeral) |
| T3-02 | Durable timers via `ITimerScheduler` | Sonnet | `TimerScheduled` fact → `FireTimer` command; InMemory + Postgres scheduler implementations; certification additions; AC-111 (durable) |
| T3-03 | Timer/event race semantics | Sonnet | Deterministic winner, loser cancellation per policy (EV-051); AC-112 |
| T3-04 | Policy decorator model | Sonnet | Declarative decorators on step/scope/definition: structured retry, timeout, cancellation (CR-006); builder surface + definition-tree metadata |
| T3-05 | Timeout policy enforcement | Haiku | Outcomes: retry/fail/cancel-branch/compensate-hook/operator-hold (EV-052, MG-041); AC-113 |
| T3-06 | Retry policy enforcement | Haiku | Bounded, no duplicate committed outcomes (CR-006); AC-510 |
| T3-07 | `WhenFirst` + residual policies | Sonnet | Deterministic winner; cancelled/ignored/allowed losers observable (CP-004); AC-204, AC-205 |
| T3-08 | Lifecycle event publication | Sonnet | Instance+step events, durability split documented per MG-020/021 (durable ones via outbox); AC-509 |
| T3-09 | Lifetime tracking + stuck detection | Haiku | MG-032/040: thresholds, stuck lifecycle events, queryable flags; AC-507, AC-508 |
| T3-10 | Statistics + pressure metrics | Haiku | Grouped counts, stream/checkpoint/outbox pressure projections (MG-030/031, DU-052); AC-503 extension, AC-312 |
| T3-11 | Resource governance: in-process limits + named pools | Sonnet | Advancement/step concurrency limits, transient pools, pool-key hints (MG-060/061); AC-511 |

## Phase-wide guardrails

- Policies are decorators on the definition tree — never new node types (CR-006).
- All timer tests drive `FakeTimeProvider`; a sleeping test is rejected (03-tdd-workflow §4).
- Durable resource pools (MG-062+) are NOT this phase — they land in Phase 4b with their
  consumer.
