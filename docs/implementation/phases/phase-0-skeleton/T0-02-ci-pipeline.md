# T0-02: CI pipeline (build + test on push/PR)

**Difficulty**: Haiku        **Depends on**: T0-01
**Spec**: NF-012        **AC**: none

## Goal
Every push and pull request builds the solution with warnings-as-errors and runs all test
projects with coverage collection, on Linux.

## Read first
- [00-stack-decisions.md](../../00-stack-decisions.md) §2 (coverage default)

## Deliverables
- `.github/workflows/ci-v3.yml`: checkout → setup .NET 10 → `dotnet build OrcaCore.slnx`
  → `dotnet test OrcaCore.slnx --collect:"XPlat Code Coverage"` → upload coverage
  artifact. Named `ci-v3` and path-filtered to `**` so it does not collide with any
  legacy workflow building the root solution.
- Concurrency group cancelling superseded runs on the same ref.
- A `docker` service note: Testcontainers-based provider tests (Phase 2+) run on the same
  Linux runner; add an `if` guard placeholder so they are skipped when Docker is absent
  (environment variable convention `ORCA_SKIP_CONTAINER_TESTS`).

## Tests to write FIRST
None (infrastructure). Verification is the pipeline itself passing on a pushed branch.

## Implementation notes
- No coverage threshold gate — collection and artifact only (per 00 §2).
- Keep the workflow minimal; matrix builds, release packaging, and benchmarks are Phase 6.

## Out of scope
- Publishing, versioning, release workflows, coverage badges.

## Definition of done
- [ ] Workflow green on the branch
- [ ] Failing test on a scratch commit turns the workflow red (verified, then reverted)
- [ ] PROGRESS.md updated; committed as "T0-02: CI pipeline"
