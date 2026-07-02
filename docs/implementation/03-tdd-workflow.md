# 03. TDD Workflow

TDD is not optional in this program. A task's tests are listed **in the task file, before
any implementation guidance** — they are the task's real specification.

## 1. The loop (per task)

1. **Red** — create the test file(s) named in the task; write every listed test case
   (they may be `[Fact]` stubs asserting the final behavior, not placeholders). Run:
   `dotnet test --filter <task filter>`. Every new test MUST fail, and fail for the right
   reason (missing type/behavior — not a typo). If a listed test passes immediately, stop
   and re-read the task: either the behavior exists (task may be obsolete) or the test is
   wrong.
2. **Green** — implement the *minimum* that makes the new tests pass. No speculative
   generality, no extra public surface.
3. **Refactor** — with green tests: remove duplication, tighten access modifiers
   (`internal sealed`), improve names. Tests stay green throughout.
4. **Regression** — run the full test suite of every project you touched, then
   `dotnet build OrcaCore.slnx` (warnings are errors).
5. Only then: DoD checklist, PROGRESS.md, commit.

## 2. Test taxonomy

| Level | Project | Scope | Doubles |
|-------|---------|-------|---------|
| **Unit** | `<Project>.Tests` | One module/class via its interface | Hand-rolled fakes from `TestSupport`; NSubstitute only for one-off narrow stubs |
| **Acceptance** | `OrcaCore.Acceptance.Tests` | Public API only, no internals; each test tagged `[Trait("AC","AC-xxx")]` | Real engines + `Providers.InMemory` |
| **Certification** | `OrcaCore.ProviderCertification` (library of abstract classes) | Port contract invariants (PR-020…024) | None — the provider under test is real |
| **Provider integration** | `<Plugin>.Tests` | Inherits certification classes; adds plugin-specific tests | Testcontainers (Postgres, RabbitMQ, …) |

Rules:

- Unit tests target **behavior through the seam**, not private methods. If a behavior can't
  be tested through an interface, the design is wrong — fix the seam, not the test.
- Acceptance tests are written from the spec text of the AC, not from the implementation.
  One AC may map to several tests; every test that proves an AC carries its trait.
- Certification tests are written **once** against the port contracts and must pass
  unchanged for InMemory, PostgreSql, and every later provider. A provider needing a
  certification-test change means the contract changed — that's a spec-level event, not a
  local edit.

## 3. Naming

- Test class: `<TypeOrFeature>Tests`; acceptance: `<Area>AcceptanceTests`.
- Test method: `<Behavior>_<Condition>_<Expectation>` — e.g.
  `RaiseEvent_DuplicateEventId_ResumesOnlyOnce`,
  `Build_MissingInit_ReportsAllValidationErrors`.
- Arrange/act/assert separated by blank lines; no logic in tests (no ifs/loops except
  data-driven `[Theory]` with `[MemberData]`).
- Assertion style: fluent `Should()` (AwesomeAssertions — the free, Apache-2.0
  FluentAssertions-API fork; see 00 §2) is the default:
  `result.Status.Should().Be(WorkflowStatus.Completed)`,
  `act.Should().ThrowAsync<WorkflowLifecycleException>()`. Plain xUnit `Assert` is allowed
  where it reads better (structural checks); do not mix both styles within one test method.

## 4. Concurrency & time in tests

- Clock: always `FakeTimeProvider`; a test that sleeps (`Task.Delay`, `Thread.Sleep`) is
  rejected. Timer behavior is tested by advancing the fake clock.
- Race tests (AC-006/007, AC-309…): use deterministic coordination — `TaskCompletionSource`
  gates inside fakes to force interleavings; never "run it 1000 times and hope".
  `TestSupport` provides a `Barrier`-style helper for two-caller races.
- Every async test: honor xUnit v3 cancellation token; timeout via test framework config,
  not ad-hoc `Task.WhenAny`.

## 5. Fakes (TestSupport)

Hand-rolled fakes are part of the product's executable documentation:

- `FakeEventStore`, `FakeOutboxStore`, `FakeInboxStore`, `FakeProjectionStore`,
  `FakeTimerScheduler`, `FakeDispatcher`, `FakeResourcePoolStore` — in-memory, observable
  (expose recorded calls), and **failure-injectable** (throw/conflict on Nth call) to test
  crash-boundary behavior (EV-032, DU-020).
- Builders: `AnEnvelope.With(...)`, `ADefinition.StraightLine(...)` — small object mothers
  so tests stay 10–20 lines.

## 6. Coverage & gates

- CI runs `dotnet build` + `dotnet test` (all projects) on every push; coverage collected
  via coverlet and reported (no hard threshold before Phase 2; from Phase 2: new code in
  `src/` ≥ 80% line coverage guideline — a *review* signal, not a build break).
- The phase exit criterion is always: **all AC traits listed in the phase README are green**
  plus zero build warnings.
