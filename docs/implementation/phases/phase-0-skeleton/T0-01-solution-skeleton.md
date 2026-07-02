# T0-01: Solution, projects, build props, package management

**Difficulty**: Haiku        **Depends on**: none
**Spec**: NF-001, NF-002, NF-010        **AC**: none

## Goal
Create the full solution skeleton with the project set and dependency edges from the
architecture doc, shared build properties, and central package management — so every later
task inherits strict compiler settings without thinking about them.

## Read first
- [01-solution-architecture.md](../../01-solution-architecture.md) §1–§3
- [00-stack-decisions.md](../../00-stack-decisions.md) §2 (Defaults)

## Deliverables
All paths below are under **`v3/`** (the implementation workspace — see README "v3/ rule";
the repository root contains off-limits legacy code):
- `v3/OrcaCore.slnx`, created and populated via `dotnet` CLI only.
- `v3/src/` projects: `OrcaCore.Abstractions`, `OrcaCore.Core`, `OrcaCore.Engine.Ephemeral`,
  `OrcaCore.Engine.Durable`, `OrcaCore.Providers.InMemory`, `OrcaCore.Hosting`
  (all `classlib`, net10.0). Plugin folders come later — do NOT create them now.
- `v3/tests/` projects: `OrcaCore.TestSupport` (classlib), plus xUnit v3 test projects:
  `OrcaCore.Core.Tests`, `OrcaCore.Engine.Ephemeral.Tests`, `OrcaCore.Engine.Durable.Tests`,
  `OrcaCore.Acceptance.Tests`; and `OrcaCore.ProviderCertification` (classlib referencing
  xunit.v3 as a library).
- Project references exactly per the dependency-rules diagram (01 §2); each `src` project
  grants `InternalsVisibleTo` to its matching `.Tests` project only (not Acceptance).
- `v3/Directory.Build.props`: net10.0, LangVersion latest, Nullable enable,
  TreatWarningsAsErrors true, AnalysisLevel latest, ImplicitUsings enable. (Placing it in
  `v3/` scopes it to the rebuild — it must not affect the legacy root solution.)
- `v3/Directory.Packages.props`: central versions for `xunit.v3`, `AwesomeAssertions`,
  `coverlet.collector`, `Microsoft.Extensions.DependencyInjection.Abstractions`,
  `Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Extensions.Options`,
  `Microsoft.Extensions.TimeProvider.Testing`. `AwesomeAssertions` is referenced by all
  test projects; the original `FluentAssertions` package must never appear (00 §3 banlist).
- `v3/.editorconfig` with file-scoped-namespace preference and naming rules per conventions.
- Repo `.gitignore` extended for the `v3/` build outputs (root .gitignore already exists —
  append, don't replace).

## Tests to write FIRST
This is the one task with no behavior to test. The "test" is structural:
1. Every test project contains one placeholder `SkeletonTests.ProjectWiring_Compiles_AndRuns`
   fact asserting `true` — proving discovery and run wiring per project. (Deleted in T0-03+.)

## Implementation notes
- Use `dotnet new`, `dotnet sln add`, `dotnet add reference`, `dotnet add package` — no
  hand-written csproj beyond property cleanup.
- Verify the dependency rules by attempting NO forbidden references — the deliverable is the
  absence of edges as much as their presence.

## Out of scope
- Any domain type, any port, any plugin project, CI (T0-02).

## Definition of done
- [ ] `dotnet build v3/OrcaCore.slnx` — zero warnings
- [ ] `dotnet test v3/OrcaCore.slnx` — all placeholder facts run and pass
- [ ] `Providers.InMemory` references ONLY `Abstractions`; `Core` references ONLY `Abstractions`
- [ ] Nothing outside `v3/` modified except the root `.gitignore` append
- [ ] PROGRESS.md created with this task's line; committed as "T0-01: solution skeleton"
