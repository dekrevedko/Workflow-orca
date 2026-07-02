# Phase 0 — Repository Skeleton & Quality Gates

**Goal**: an empty-but-strict codebase where every later task lands on rails: solution,
projects, build props, CI, test wiring, and the functional primitives.

**Entry criteria**: none (greenfield).
**Exit criteria**: CI green on a clean clone; `dotnet build` zero warnings;
`dotnet test` runs all test projects; `Result/Option/Validation` fully unit-tested;
dependency rules of [01-solution-architecture.md](../../01-solution-architecture.md)
physically true (verified by project references).

## Task index

| Task | Title | Difficulty |
|------|-------|-----------|
| [T0-01](T0-01-solution-skeleton.md) | Solution, projects, build props, package management | Haiku |
| [T0-02](T0-02-ci-pipeline.md) | CI pipeline (build + test on push/PR) | Haiku |
| [T0-03](T0-03-functional-primitives.md) | `Result<T>` / `Option<T>` / `Validation<T>` | Haiku |
| [T0-04](T0-04-test-support-foundation.md) | TestSupport project: fake clock harness, race helper, trait conventions | Sonnet |
