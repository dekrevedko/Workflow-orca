# T0-04: TestSupport foundation — clock harness, race helper, trait conventions

**Difficulty**: Sonnet        **Depends on**: T0-03
**Spec**: NF-020, NF-012        **AC**: none (enables all race/time ACs later)

## Goal
Give every later test deterministic control over time and interleaving: a fake-clock
harness, a two-caller race coordinator, and the trait/category conventions — in
`OrcaCore.TestSupport`.

## Read first
- [03-tdd-workflow.md](../../03-tdd-workflow.md) §4–§5
- [02-engineering-conventions.md](../../02-engineering-conventions.md) §3 (time rules)

## Deliverables
In `v3/tests/OrcaCore.TestSupport/`:
- `Clock` helper wrapping `FakeTimeProvider` (from
  `Microsoft.Extensions.TimeProvider.Testing`): construct-at-known-instant, `Advance(...)`,
  and an assert-friendly `Now` accessor.
- `RaceCoordinator` — deterministically forces two async callers to reach a gate before
  either proceeds (built on `TaskCompletionSource`), with a timeout that fails the test
  rather than hanging.
- `Traits` static class — canonical trait names/keys: `("AC", "AC-xxx")`,
  `("Category", "Certification")`, `("Category", "Container")` — so filters are typo-proof.
- Marker doc comment on the project: what belongs in TestSupport (fakes, builders, harness)
  and what never does (assertions helpers, production logic).

## Tests to write FIRST
In `v3/tests/OrcaCore.Core.Tests/TestSupport/`:
1. `Clock_Advance_MovesTimeExactly`
2. `RaceCoordinator_TwoCallers_BothReachGateBeforeEitherProceeds`
3. `RaceCoordinator_OneCallerNeverArrives_FailsWithTimeoutNotHang`

## Implementation notes
- `RaceCoordinator` is the backbone of AC-006/007/309 later — bias its API toward reading
  like the acceptance text ("both attempts arrive, then release together").
- Keep TestSupport free of xUnit assertion dependencies where possible; it is a library
  consumed by all test projects and the certification suite.

## Out of scope
- Port fakes (`FakeEventStore` etc. arrive with the ports in Phase 1/2 tasks that
  introduce each port); object-mother builders (arrive with the contracts they build).

## Definition of done
- [ ] Listed tests green; full suite green; zero warnings
- [ ] TestSupport referenced by all test projects
- [ ] PROGRESS.md updated; committed as "T0-04: TestSupport foundation"
