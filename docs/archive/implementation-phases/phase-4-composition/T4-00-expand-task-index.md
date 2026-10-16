# T4-00: Expand Phase 4 task index

**Difficulty**: Sonnet        **Depends on**: Phase 3 exit
**Spec**: CP-010..013, CP-020..026, CP-030, DU-033        **AC**: none

## Goal
Expand the Phase 4 README index into executable task files. Each task must fit the task
protocol and use explicit repository-root source and test paths.

## Read first
- `docs/implementation/phases/phase-4-composition/README.md`
- `docs/implementation/04-task-protocol.md`
- `docs/specs/08-requirements-composition.md`
- `docs/specs/12-acceptance-criteria.md`
- `docs/specs/06-requirements-durable-execution.md`

## Deliverables
- `docs/implementation/phases/phase-4-composition/T4-01-deterministic-partitioners.md`
- `docs/implementation/phases/phase-4-composition/T4-02-ephemeral-foreach-dispatch.md`
- `docs/implementation/phases/phase-4-composition/T4-03-foreach-join-failure-residual-policies.md`
- `docs/implementation/phases/phase-4-composition/T4-04-child-lineage-model.md`
- `docs/implementation/phases/phase-4-composition/T4-05-runchild-single-child-wait-join.md`
- `docs/implementation/phases/phase-4-composition/T4-06-runchildren-dynamic-fanout.md`
- `docs/implementation/phases/phase-4-composition/T4-07-durable-throttling.md`
- `docs/implementation/phases/phase-4-composition/T4-08-exactly-once-parent-resume.md`
- `docs/implementation/phases/phase-4-composition/T4-09-whenany-residuals-mixed-outbox.md`

## Tests to write FIRST
No product tests. Validate the generated files against the task protocol by inspection.

## Implementation notes
Use the current repository-root project layout and existing composition, durable aggregate, command
processor, provider, and acceptance test locations in each task's "Read first" list.

## Out of scope
Any code changes under `src/` or `tests/`.

## Definition of done
- [ ] Task files T4-01 through T4-09 exist
- [ ] Each task uses explicit repository-root paths
- [ ] PROGRESS.md updated; committed as "T4-00: expand composition task index"
