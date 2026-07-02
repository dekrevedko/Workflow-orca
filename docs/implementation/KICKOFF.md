# Kickoff Prompt

Copy-paste this to start an implementation session (Claude Code / Codex). Swap the task
path for later sessions. Branch: `feature/v3-rebuild`.

```text
You are implementing the OrcaCore workflow engine from scratch, one task per session.

Rules of engagement:
1. Read docs/implementation/README.md fully and obey its ground rules and the "v3/ rule":
   ALL code lives under v3/ (v3/OrcaCore.slnx, v3/src, v3/tests). The legacy code at the
   repository root (src/, tests/, OrcaCore.slnx) is OFF-LIMITS: never read, copy from,
   reference, or modify it. Where a task says "repo root", it means v3/.
2. Read docs/implementation/02-engineering-conventions.md and
   docs/implementation/03-tdd-workflow.md.
3. Execute EXACTLY ONE task, following docs/implementation/04-task-protocol.md:

   docs/implementation/phases/phase-0-skeleton/T0-01-solution-skeleton.md

4. TDD is mandatory: write the task's listed tests first, watch them fail for the right
   reason, then implement the minimum, then refactor.
5. Read ONLY the files the task lists under "Read first" — do not explore the repository.
6. If anything is ambiguous, check the task's notes, then
   docs/implementation/00-stack-decisions.md. If still ambiguous, STOP and record the
   question in the phase PROGRESS.md as "blocked" — never resolve a registered open
   question yourself.
7. Finish by: checking every Definition-of-Done box, updating the phase PROGRESS.md,
   and committing as "T0-01: <summary>". Then STOP — do not start another task.
```

## Model routing

| Session | Model class |
|---------|-------------|
| **T0-01 (first session)** | **Sonnet** — protocol smoke test; skeleton mistakes poison everything downstream |
| Tasks marked `Haiku` | Haiku-class |
| Tasks marked `Sonnet`, all `Tn-00` expansions | Sonnet-class |
| Phase-exit reviews, port-interface changes, open-question resolutions | Sonnet-class or human (04-task-protocol §5) |

## Session checklist for the operator

Before starting a session: confirm branch `feature/v3-rebuild` is checked out and clean.
After a session: skim the commit + PROGRESS.md line; on `blocked`, resolve the question in
00-stack-decisions.md (or the spec) before re-running the task.
