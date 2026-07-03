# T4B-00: Expand Phase 4b task index

**Difficulty**: Sonnet        **Depends on**: Phase 4 exit
**Spec**: JS-001, JS-002, JS-005, JS-006, JS-007, MG-062..064        **AC**: none

## Goal
Expand the Phase 4b README index into executable task files and resolve spec open question
15 before implementation starts. The resolution is child-instance-per-node DAG compilation
using `RunChildren`-style orchestration.

## Read first
- `docs/implementation/phases/phase-4b-dag-external-jobs/README.md`
- `docs/implementation/04-task-protocol.md`
- `docs/implementation/00-stack-decisions.md`
- `docs/specs/13-phasing-and-open-questions.md`
- `docs/specs/14-driving-scenario-eks-job-scheduler.md`
- Spec: `docs/specs/09-requirements-management-operations.md` section 9.7
- Spec: `docs/specs/12-acceptance-criteria.md` management and scenario AC references

## Deliverables
- `docs/implementation/phases/phase-4b-dag-external-jobs/PROGRESS.md`
- `docs/implementation/phases/phase-4b-dag-external-jobs/T4B-01-durable-pool-store.md`
- `docs/implementation/phases/phase-4b-dag-external-jobs/T4B-02-acquisition-as-wait.md`
- `docs/implementation/phases/phase-4b-dag-external-jobs/T4B-03-ticket-expiry-pool-operations.md`
- `docs/implementation/phases/phase-4b-dag-external-jobs/T4B-04-run-external-job-composite.md`
- `docs/implementation/phases/phase-4b-dag-external-jobs/T4B-05-dag-builder-validation.md`
- `docs/implementation/phases/phase-4b-dag-external-jobs/T4B-06-run-cancellation-dag-observability.md`

## Tests to write FIRST
No product tests. Validate the generated task files against the task protocol by inspection
and by searching for legacy workspace paths.

## Implementation notes
Use `v3-gpt/` paths throughout this run. Log the spec open question 15 resolution in
`docs/implementation/00-stack-decisions.md` and mark it resolved in
`docs/specs/13-phasing-and-open-questions.md`.

## Out of scope
Any code changes under `v3-gpt/`.

## Definition of done
- [ ] Task files T4B-01 through T4B-06 exist
- [ ] Spec open question 15 is marked resolved in spec document 13
- [ ] Decision is logged in `docs/implementation/00-stack-decisions.md`
- [ ] Each task uses explicit `v3-gpt/` paths
- [ ] PROGRESS.md updated; committed as "T4B-00: expand dag external jobs task index"
