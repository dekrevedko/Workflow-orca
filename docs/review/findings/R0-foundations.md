# R0 — Foundations & Architecture — Findings

> Baseline recorded 2026-07-03 (synthesis pass — see [SUMMARY.md](SUMMARY.md)):
>
> - **Build:** `dotnet build v3-gpt/OrcaCore.slnx -warnaserror` → passed, 0 warnings, 0 errors
> - **Test:** `dotnet test v3-gpt/OrcaCore.slnx` → run 1: 892 passed / 1 failed (flaky hosting
>   test, unreproducible ×8 isolated + full re-run) / 16 skipped; run 2: **894 / 0 / 16**.
>   All container suites (PostgreSQL, SQL Server, RabbitMQ, Redis) executed against Docker.
> - **Commit reviewed:** `0ba803b` + uncommitted working-tree remediation changes (~2,800 lines)

## Findings

_(most-severe first, per docs/review/README.md §5)_

### [P2] CI runs all test kinds in one lane with a token coverage floor — `.github/workflows/ci.yml:30`
- **Requirement/convention:** audit remediation item #8 (split unit / container / integration lanes); 03 TDD workflow
- **Evidence:** One `dotnet test v3-gpt/OrcaCore.slnx` step runs unit, Testcontainers, and integration suites together; the coverage gate enforces `MIN_CORE_ENGINE_LINE_RATE=0.20` while measured engine coverage is ~0.9.
- **Failure scenario:** A container-infrastructure hiccup fails the whole pipeline including fast unit feedback; a change that halves engine coverage still passes the 0.20 gate.
- **Recommendation:** Split into a fast unit lane and a container/integration lane (trait-based filter already exists — container tests are tagged); raise the engine floor to ~0.80; add `--logger trx` so failing test names survive (the run-1 flake's identity was lost to output filtering).
- **Confidence:** CONFIRMED

### [P3] Repo root still carries the superseded prototype solution — `OrcaCore.slnx` (root)
- **Requirement/convention:** none — general (01 solution architecture: one active lineage)
- **Evidence:** Root `OrcaCore.slnx` references `src/OrcaCore.Runtime` etc. (the pre-v3 prototype); `v3` and `v3-cursor` lineages are deleted in the working tree but the root prototype remains alongside `v3-gpt/`.
- **Failure scenario:** New contributors (or CI matrix additions) build/test the wrong lineage; R10 exists precisely because the two drifted.
- **Recommendation:** After the lineage-consolidation commit lands, either delete the root prototype or mark it archived in the root README; keep exactly one buildable solution path.
- **Confidence:** CONFIRMED

## Coverage note

Verified in the synthesis pass: solution/project graph (12 src + 13 test projects, dependency
directions clean — Abstractions ← Core ← Engines ← Providers/Hosting), central package
management (`Directory.Packages.props`) with no banlisted packages (xUnit v3, AwesomeAssertions,
Dapper-accepted-per-rescan, Testcontainers; no FluentAssertions v8+, no MediatR/AutoMapper-class
libraries), `-warnaserror` clean with analyzers on, NF-001/002 (net10.0 pin via `global.json`).
Not exhaustively re-reviewed here: per-csproj reference-graph audit (spot-checked only) — the
earlier R8 pass covered conventions in depth.
