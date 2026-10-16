# Phase 3 — Time, Policies, Operations (spec Slice 3)

**Goal**: timers (transient + durable), timeout/retry policies, `WhenFirst`, lifecycle
events, stuck detection, statistics/pressure metrics, resource governance (in-process tier).

**Entry criteria**: Phase 2 exit green.
**Exit criteria**: AC-111…113, AC-204/205, AC-507…511, AC-312 green; **AC-513's
timer-firing clause** re-verified now that durable timers exist (the event clause was gated
in Phase 2); IOQ-5 (observability) resolved and logged.

## Task index (expanded by T3-00)

| Task | Title | Difficulty | Summary |
|------|-------|-----------|---------|
| T3-00 | Expand index | Sonnet | Template expansion; resolve IOQ-5 with the reviewer first |
| T3-01 | Delay definition primitive | Haiku | Add `Delay` authoring and definition metadata without runtime scheduling; structural tests for EV-050 |
| T3-02 | Transient timer service (ephemeral) | Haiku | Min-heap over `TimeProvider` + channel of due firings; AC-111 (ephemeral) |
| T3-03 | Durable timer contracts and decisions | Sonnet | `TimerScheduled` fact → `FireTimer` command; aggregate command decisions; AC-111 contract path |
| T3-04 | Durable timer provider implementations | Sonnet | InMemory + Postgres scheduler implementations; certification additions; AC-513 timer-firing clause |
| T3-05 | Timer/event race semantics | Sonnet | Deterministic winner, loser cancellation per policy (EV-051); AC-112 |
| T3-06 | Policy decorator model | Sonnet | Declarative decorators on step/scope/definition: structured retry, timeout, cancellation (CR-006); builder surface + definition-tree metadata |
| T3-07 | Timeout policy enforcement | Haiku | Outcomes: retry/fail/cancel-branch/operator-hold baseline (EV-052, MG-041); AC-113 |
| T3-08 | Retry policy enforcement | Haiku | Bounded, no duplicate committed outcomes (CR-006); AC-510 |
| T3-09 | `WhenFirst` + residual policies | Sonnet | Deterministic winner; cancelled/ignored/allowed losers observable (CP-004); AC-204, AC-205 |
| T3-10 | Lifecycle event publication | Sonnet | Instance+step events, durability split documented per MG-020/021 (durable ones via outbox); AC-509 |
| T3-11 | Lifetime tracking + stuck detection | Haiku | MG-032/040: thresholds, stuck lifecycle events, queryable flags; AC-507, AC-508 |
| T3-12 | Statistics + pressure metrics | Haiku | Grouped counts, stream/checkpoint/outbox pressure projections (MG-030/031, DU-052); AC-503 extension, AC-312 |
| T3-13 | Resource governance: in-process limits + named pools | Sonnet | Advancement/step concurrency limits, transient pools, pool-key hints (MG-060/061); AC-511 |

## Phase-wide guardrails

- Policies are decorators on the definition tree — never new node types (CR-006).
- All timer tests drive `FakeTimeProvider`; a sleeping test is rejected (03-tdd-workflow §4).
- Durable resource pools (MG-062+) are NOT this phase — they land in Phase 4b with their
  consumer.
