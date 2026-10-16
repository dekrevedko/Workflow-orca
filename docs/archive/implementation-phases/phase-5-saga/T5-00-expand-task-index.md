# T5-00: Expand Phase 5 saga task index

**Difficulty**: Sonnet        **Depends on**: Phase 4b exit
**Spec**: SG-001..030, CP-035        **AC**: none

## Goal
Expand the Phase 5 README index into executable task files. The expansion must encode
the resolved saga decisions: Phase 5 implements compensation-heavy saga semantics first,
and ephemeral saga is an in-process-only limited mode.

## Read first
- `docs/implementation/phases/phase-5-saga/README.md`
- `docs/implementation/00-stack-decisions.md`
- `docs/implementation/04-task-protocol.md`
- Spec: `docs/specs/07-requirements-saga.md`
- Spec: `docs/specs/12-acceptance-criteria.md` saga and AC-616 sections

## Deliverables
- `docs/implementation/phases/phase-5-saga/T5-01-saga-contracts-lifecycle.md`
- `docs/implementation/phases/phase-5-saga/T5-02-saga-builder.md`
- `docs/implementation/phases/phase-5-saga/T5-03-compensation-decision-layer.md`
- `docs/implementation/phases/phase-5-saga/T5-04-compensation-failure-terminal-outcomes.md`
- `docs/implementation/phases/phase-5-saga/T5-05-timeout-cancellation-interaction.md`
- `docs/implementation/phases/phase-5-saga/T5-06-durable-audit-operator-recovery.md`
- `docs/implementation/phases/phase-5-saga/T5-07-child-compensation.md`
- `docs/implementation/phases/phase-5-saga/T5-08-ephemeral-saga-limited-mode.md`

## Tests to write FIRST
No product tests. This is a task-expansion task.

## Implementation notes
All task files must use explicit repository-root source and test paths. Each task must keep compensation
APIs out of regular workflow builders unless it is explicitly introducing saga-specific
surface. T5-08 must require XML documentation and implementation docs that describe
ephemeral saga as in-process only.

## Out of scope
Implementing saga code, adding acceptance tests, changing provider ports, or starting T5-01.

## Definition of done
- [ ] Every Phase 5 index item has a full task file
- [ ] Legacy workspace path check over `docs/implementation/phases/phase-5-saga` returns no matches
- [ ] Source/test path check over Phase 5 task files shows paths only under the repository root or docs paths
- [ ] PROGRESS.md updated; committed as "T5-00: expand saga phase tasks"
