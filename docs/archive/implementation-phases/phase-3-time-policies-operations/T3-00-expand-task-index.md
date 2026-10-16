# T3-00: Expand Phase 3 task index

**Difficulty**: Sonnet        **Depends on**: Phase 2 exit
**Spec**: EV-050..052, CR-006, CP-004, MG-020..041, MG-060..061, DU-052, PR-014        **AC**: none

## Goal
Expand the Phase 3 README task index into path-safe, executable task files before timer,
policy, and operations implementation begins. Resolve IOQ-5 first so implementation tasks
have a stable observability boundary.

## Read first
- `docs/implementation/phases/phase-3-time-policies-operations/README.md`
- `docs/implementation/00-stack-decisions.md`
- `docs/implementation/04-task-protocol.md`
- Spec: `docs/specs/05-requirements-events-waits-timers.md`
- Spec: `docs/specs/09-requirements-management-operations.md`

## Deliverables
- `docs/implementation/phases/phase-3-time-policies-operations/PROGRESS.md`
- `docs/implementation/phases/phase-3-time-policies-operations/T3-01-*.md` through `T3-13-*.md`
- `docs/implementation/00-stack-decisions.md` updated for IOQ-5
- All paths in generated tasks are explicit repository-root paths.

## Tests to write FIRST
No product tests. Review the generated task files for path safety, dependency order, AC
ownership, registered open-question handling, and sizing.

## Implementation notes
IOQ-5 is resolved as: core, engines, and providers may use BCL `ILogger`,
`ActivitySource`, and `Meter`; no OpenTelemetry package dependency is added outside Hosting.
Durable timer provider implementation is split from durable timer contracts because the
combined README entry would exceed the task sizing limits.

## Out of scope
Any source code, product tests, package additions, or Phase 3 runtime implementation.

## Definition of done
- [ ] T3-01 through T3-13 task files exist
- [ ] Phase 3 PROGRESS.md created and updated
- [ ] IOQ-5 removed from the open-question register
- [ ] `dotnet build OrcaCore.slnx` - zero warnings
- [ ] `dotnet test OrcaCore.slnx` passes
- [ ] PROGRESS.md updated; committed as "T3-00: expand phase-3 task index"
