# T2-00: Expand Phase 2 task index

**Difficulty**: Sonnet        **Depends on**: T1-15
**Spec**: DU-010..013, DU-020..022, DU-030..033, DU-040..041, DU-050..053, DU-070, PR-010..024, PR-030        **AC**: none

## Goal
Expand the Phase 2 README task index into path-safe, executable task files before durable implementation begins.
Each task must fit the protocol sizing limits or be split before execution.

## Read first
- `docs/implementation/phases/phase-2-durable-core/README.md`
- `docs/implementation/04-task-protocol.md`
- `docs/implementation/00-stack-decisions.md`
- Spec: `docs/specs/06-requirements-durable-execution.md`
- Spec: `docs/specs/10-provider-model-and-extensibility.md`

## Deliverables
- `docs/implementation/phases/phase-2-durable-core/PROGRESS.md`
- `docs/implementation/phases/phase-2-durable-core/T2-01-*.md` through `T2-15-*.md`
- All paths in generated tasks use ``.

## Tests to write FIRST
No product tests. Review the generated task files for path safety, dependency order, AC ownership, and sizing.

## Implementation notes
Resolve no IOQs in this task. IOQ-1 and IOQ-3 must remain explicitly assigned to their implementation tasks.

## Out of scope
Any source code, tests, provider implementation, package additions, or durable API surface.

## Definition of done
- [ ] T2-01 through T2-15 task files exist
- [ ] Phase 2 PROGRESS.md created and updated
- [ ] `dotnet build OrcaCore.slnx` - zero warnings
- [ ] `dotnet test OrcaCore.slnx` passes
- [ ] PROGRESS.md updated; committed as "T2-00: expand phase-2 task index"
