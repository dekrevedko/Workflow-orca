# T6-12: Add BenchmarkDotNet project skeleton

**Difficulty**: Haiku        **Depends on**: T6-11
**Spec**: NF-030, NF-031        **AC**: none

## Goal
Add the benchmark project in the owner-approved location and wire CI to build it without
running benchmarks on normal PRs. This creates the benchmark surface but not the full
scenario suite.

## Read first
- `v3-gpt/OrcaCore.slnx`
- `v3-gpt/Directory.Packages.props`
- `v3-gpt/Directory.Build.props`
- `.github/workflows/ci.yml`
- `docs/implementation/00-stack-decisions.md`
- Spec: `docs/specs/11-non-functional-requirements.md` section 11.4

## Deliverables
- Add `v3-gpt/benchmarks/OrcaCore.Benchmarks/OrcaCore.Benchmarks.csproj`
- Add `v3-gpt/benchmarks/OrcaCore.Benchmarks/Program.cs`
- Add `v3-gpt/benchmarks/OrcaCore.Benchmarks/README.md`
- Update `v3-gpt/Directory.Packages.props`
- Update `v3-gpt/OrcaCore.slnx`
- Update CI so PRs build the benchmark project but do not run benchmarks

## Tests to write FIRST
No product tests. Verification is build and CI command shape.

## Implementation notes
Use BenchmarkDotNet only in the benchmark project. Do not add BenchmarkDotNet references to
production or test projects. Normal PR CI must not run benchmarks.

## Out of scope
Benchmark scenario implementation, performance thresholds, package publishing, and release
automation.

## Definition of done
- [ ] `dotnet build v3-gpt/benchmarks/OrcaCore.Benchmarks/OrcaCore.Benchmarks.csproj` passes with zero warnings
- [ ] `dotnet build v3-gpt/OrcaCore.slnx` passes with zero warnings
- [ ] CI builds benchmark project and does not run benchmarks in normal PR flow
- [ ] PROGRESS.md updated; committed as "T6-12: benchmark project skeleton (NF-030)"
