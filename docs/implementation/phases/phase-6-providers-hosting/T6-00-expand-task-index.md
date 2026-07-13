# T6-00: Expand Phase 6 providers and hosting task index

**Difficulty**: Sonnet        **Depends on**: Phase 5 exit
**Spec**: PR-015, PR-040, DU-042, DU-050, DU-051, NF-030, NF-040        **AC**: none

## Goal
Expand the Phase 6 README index into executable task files. Record the owner decisions for
IOQ-8 and IOQ-9 before implementation begins: benchmarks are scoped to selected hot paths
and built-only in PR CI, while public package publishing is deferred for this run.

## Read first
- `docs/implementation/phases/phase-6-providers-hosting/README.md`
- `docs/implementation/00-stack-decisions.md`
- `docs/implementation/04-task-protocol.md`
- Spec: `docs/specs/10-provider-model-and-extensibility.md`
- Spec: `docs/specs/11-non-functional-requirements.md` sections 11.4 and 11.5

## Deliverables
- Update `docs/implementation/00-stack-decisions.md`
- Update `docs/implementation/phases/phase-6-providers-hosting/README.md`
- Add task files `T6-01` through `T6-16` under `docs/implementation/phases/phase-6-providers-hosting/`
- Update `docs/implementation/phases/phase-6-providers-hosting/PROGRESS.md`

## Tests to write FIRST
No product tests. This is a task-expansion and decision-recording task.

## Implementation notes
All generated task files must use `` implementation paths. Do not add production
code, test code, package references, or solution entries in this task.

## Out of scope
Starting T6-01, adding benchmarks, adding providers, changing `` code, or publishing
packages.

## Definition of done
- [ ] IOQ-8 is resolved in `docs/implementation/00-stack-decisions.md`
- [ ] IOQ-9 is resolved as deferred in `docs/implementation/00-stack-decisions.md`
- [ ] Every Phase 6 index item has a full task file or an explicit optional gate task
- [ ] Source/test path check over Phase 6 task files shows paths only under `` or docs paths
- [ ] PROGRESS.md updated; committed as "T6-00: expand providers hosting phase tasks"
