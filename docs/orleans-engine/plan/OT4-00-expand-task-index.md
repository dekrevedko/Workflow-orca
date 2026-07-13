# OT4-00: Expand Phase O4 task index (hosting polish & operations)

**Difficulty**: Sonnet        **Depends on**: Phase O3 exit (review gate passed)
**Spec**: OE-053, OE-070; DU-002 (feature matrix)        **AC**: OE-AC-046 (hardening)

## Goal
Turn the Phase O4 index entries in [plan/README.md](README.md) (OT4-01…OT4-04) into full
task files per the protocol template.

## Read first
- [plan/README.md](README.md) Phase O4 index
- `v3-gpt/src/OrcaCore.Engine.Orleans/Hosting/` (options + extension as they now exist)
- Existing diagnostics conventions: the `ActivitySource`/`Meter` naming used by
  `Engine.Durable` (locate under `Abstractions/Diagnostics` or engine diagnostics; read ≤2 files)
- `docs/architecture/quick-vs-durable-engine-feature-matrix.md` (for OT4-04)

## Deliverables
- `OT4-01…OT4-04` task files, sized within protocol limits. OT4-04 is docs-only and must
  list the exact docs to touch (engine README under `v3-gpt/src/OrcaCore.Engine.Orleans/`,
  feature-matrix column, `docs/README.md` code-map row).

## Implementation notes
- Options validation follows the repo's `IOptions` + plain-record pattern; no new
  validation frameworks.
- Diagnostics names must extend the existing scheme (IOQ-5 resolution), not invent a new one.

## Out of scope
Executing any OT4 task; certification/e2e (Phase O5).

## Definition of done
- [ ] All OT4 task files exist, paths verified; expansion reviewed before execution
- [ ] PROGRESS.md updated; committed as "OT4-00: expand Phase O4 tasks"
