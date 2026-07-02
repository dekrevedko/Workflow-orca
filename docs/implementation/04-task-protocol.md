# 04. Task Protocol & Template

## 1. Task sizing rules (context-budget discipline)

A task MUST fit a small agent session:

- ≤ ~10 files created/modified, ≤ ~500 changed lines (tests included);
- reads ≤ 5 existing source files plus ≤ 2 spec sections — all listed explicitly in the
  task file; the agent never explores the repository;
- one concern per task; if a task description needs the word "and" twice, split it;
- no task depends on unwritten future tasks; dependencies point backward only.

If, mid-task, the agent discovers the task cannot fit these limits: stop, write findings to
PROGRESS.md, and propose a split — do not push through.

## 2. Agent execution protocol

```text
1. Read implementation/README.md ground rules (once per session)
2. Read 02-engineering-conventions.md and 03-tdd-workflow.md (once per session)
3. Read the task file end-to-end
4. Read ONLY the files listed under "Read first"
5. RED    — write all listed tests; run task filter; verify correct failures
6. GREEN  — minimal implementation
7. REFACTOR — tighten, dedupe; suite stays green
8. VERIFY — run every command under "Definition of done"; check every box
9. RECORD — append one line to the phase PROGRESS.md:
            "T1-04 | done | <date> | deviations: none" (or the deviation)
10. COMMIT — "T1-04: <summary> (<spec IDs>)" ; STOP (no next task in-session)
```

Escalation rule: anything ambiguous → check the task's "Assumptions/resolved questions"
section → check 00-stack-decisions.md → if still ambiguous, stop and record the question in
PROGRESS.md as `blocked`. Never invent a resolution to a registered open question.

## 3. Task file template

Every task file follows this exact structure (keep under ~120 lines):

```markdown
# T<phase>-<nn>: <imperative title>

**Difficulty**: Haiku | Sonnet        **Depends on**: T<...>
**Spec**: <requirement IDs>           **AC**: <acceptance criteria IDs or "none">

## Goal
<2-4 sentences: the observable capability after this task.>

## Read first
- <file paths, ≤5>
- Spec: <specs/... section anchors, ≤2>

## Deliverables
- <projects/files to create or modify — exact paths>
- <public contracts introduced, described as interface sketches if needed — no bodies>

## Tests to write FIRST
In `<test project>/<file>`:
1. `<TestName>` — <one-line given/when/then>
2. ...
(Traits: [Trait("AC","AC-xxx")] where applicable.)

## Implementation notes
<Constraints, chosen approach, pitfalls. Never restates the spec — links to it.>

## Out of scope
<Explicit non-goals so the agent doesn't wander.>

## Definition of done
- [ ] All new tests green; full affected suites green
- [ ] `dotnet build OrcaCore.slnx` — zero warnings
- [ ] <task-specific checks: e.g. "Abstractions has no new dependency">
- [ ] PROGRESS.md updated; committed as "T<id>: ..."
```

## 4. Phase structure & the Tn-00 expansion task

Each `phases/phase-<n>-<name>/` contains:

- `README.md` — phase goal, entry criteria, **task index** (id, title, 3–6 line summary
  with tests/AC refs and dependencies), exit criteria (the AC trait list that must be green).
- `PROGRESS.md` — created by the first task; append-only log.
- `T<n>-<nn>-<slug>.md` — one file per task.

**`Tn-00` (Sonnet-level, first task of phases 2+)**: expand the phase README's task index
into full task files using the template above, reading the current code to fill "Read
first" sections accurately, and splitting any entry that violates sizing rules. The
expansion is itself reviewed against the phase's spec sections before execution starts.

## 5. Review checkpoints (human or strong-model)

Mandatory review gates — an execution agent must not pass these alone:

- end of every phase (exit criteria + architecture drift check against 01);
- any change to a port interface after Phase 2 (certification suite impact);
- any new third-party dependency (00 banlist/whitelist);
- any resolution of a registered open question.
