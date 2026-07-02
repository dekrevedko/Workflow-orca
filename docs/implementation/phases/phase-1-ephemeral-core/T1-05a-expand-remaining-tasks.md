# T1-05a: Expand T1-06…T1-15 into detailed task files

**Difficulty**: Sonnet        **Depends on**: T1-05
**Spec**: none (planning task)        **AC**: none

## Goal
Turn the Phase 1 README's index entries for T1-06…T1-15 into full task files following the
template in [04-task-protocol.md](../../04-task-protocol.md) §3, grounded in the code that
now exists after T1-01…T1-05.

## Read first
- [04-task-protocol.md](../../04-task-protocol.md) (template + sizing rules)
- [README.md](README.md) of this phase (index rows T1-06…T1-15, guardrails, exit criteria)
- The current `v3/src/` tree layout (folder names and public types only — no deep dives)
- Spec sections cited by each index row (one at a time, while writing that row's file)

## Deliverables
- One task file per index row: `T1-06-execution-lane.md` … `T1-15-yield.md`, each following
  the template exactly: Difficulty, Depends-on, Spec/AC refs copied from the index row,
  Goal, **Read first with real current paths (all under `v3/`)**, Deliverables, **Tests to
  write FIRST** (enumerated test names incl. the AC-trait acceptance tests), Implementation
  notes, Out of scope, Definition of done.
- Any index row that cannot fit the sizing rules (≤10 files, ≤500 lines) is split into
  `Tn-xxa`/`Tn-xxb` files and the README index updated to match.
- `PROGRESS.md` line for this task.

## Rules for expansion
- Every test listed must trace to a spec requirement or AC named in the row — no invented
  scope; no dropped ACs (cross-check the phase exit criteria list).
- "Read first" lists ≤5 real files that exist right now; if a needed file doesn't exist,
  the dependency order is wrong — fix the index, don't hand-wave.
- Keep each task file under ~120 lines.
- Do NOT implement anything in `v3/src` or `v3/tests` in this task.

## Definition of done
- [ ] Ten (or more, if split) task files exist and follow the template
- [ ] Every AC in the phase exit criteria is owned by exactly one task's test list
- [ ] README index updated for any splits; dependency graph is backward-only
- [ ] Reviewed per 04-task-protocol §5 before T1-06 starts (record reviewer in PROGRESS.md)
- [ ] PROGRESS.md updated; committed as "T1-05a: expand phase-1 task index"
