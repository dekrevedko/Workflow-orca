# OT0-01: Create the Orleans engine project skeleton

**Difficulty**: Sonnet        **Depends on**: none (green v3-gpt build)
**Spec**: OE-001, OE-003        **AC**: none directly

## Goal
`OrcaCore.Engine.Orleans` exists, builds inside the solution with the pinned Orleans
packages, and the dependency rules are locked in before any behavior lands.

## Read first
- `v3-gpt/Directory.Packages.props`
- `v3-gpt/Directory.Build.props`
- `v3-gpt/src/OrcaCore.Engine.Durable/OrcaCore.Engine.Durable.csproj` (as csproj template)
- [01-architecture.md](../01-architecture.md) §5–6

## Deliverables
- `v3-gpt/Directory.Packages.props`: pin the **exact reviewed Orleans 10.x version**
  (10.2.1 as of 2026-07-04 — verify current patch on NuGet at execution time and record the
  chosen pin in PROGRESS.md) for `Microsoft.Orleans.Server`, `Microsoft.Orleans.Sdk`,
  `Microsoft.Orleans.Serialization.SystemTextJson`, `Microsoft.Orleans.TestingHost`
  (versions only here — CPM). Never a floating/"latest" version.
- `v3-gpt/src/OrcaCore.Engine.Orleans/OrcaCore.Engine.Orleans.csproj` — references:
  `OrcaCore.Abstractions`, `OrcaCore.Core`, `OrcaCore.Engine.Durable` (projects);
  `Microsoft.Orleans.Server`, `Microsoft.Orleans.Sdk`,
  `Microsoft.Orleans.Serialization.SystemTextJson` (packages).
- Root namespace `OrcaCore.Engine.Orleans`; one placeholder public type
  `OrleansEngineMarker` (internal-empty, XML-doc'd) so the project is non-empty.
- Add project to `v3-gpt/OrcaCore.slnx`.
- Ensure `docs/orleans-engine/plan/PROGRESS.md` exists with its header line.

## Tests to write FIRST
None (skeleton task — the compiler and solution build are the test). Do not add a test
project here; that is OT0-02.

## Implementation notes
- Use `dotnet` CLI for scaffolding (`dotnet new classlib`, `dotnet sln ... add` /
  slnx-aware `dotnet sln add`).
- `TreatWarningsAsErrors` etc. arrive via `Directory.Build.props` — do not restate.
- Do NOT add Orleans packages to any other project. Do NOT add clustering/reminders
  packages (OOQ-1/OOQ-4).

## Out of scope
Grains, envelopes, DI, tests — later tasks.

## Definition of done
- [ ] `dotnet build v3-gpt/OrcaCore.slnx` — zero warnings
- [ ] `Microsoft.Orleans.*` referenced ONLY by `OrcaCore.Engine.Orleans`
      (verify: `grep -r "Microsoft.Orleans" v3-gpt/src --include=*.csproj`)
- [ ] PROGRESS.md exists and is updated; committed as "OT0-01: Orleans engine skeleton (OE-001, OE-003)"
