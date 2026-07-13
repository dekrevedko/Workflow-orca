# OrcaCore — Implementation Guide (agent-executable)

This folder turns the specification package ([docs/specs/](../specs/README.md)) into an
**executable implementation program**: short, self-contained tasks that an LLM coding agent
(Haiku-level for most tasks, Sonnet/Codex-level where marked) can complete one at a time
without large context windows.

**Ground rules for everything in this folder:**

- **TDD first, always.** Every task starts by writing the listed failing tests, then makes
  them pass, then refactors. No production code before its test exists.
- **Program to interfaces, not implementations.** Consumers depend on contracts in
  `OrcaCore.Abstractions`; concrete types are `internal` and reached through DI. A task that
  makes a caller depend on a concrete class is wrong even if tests pass.
- **No monolith.** The engine is internal modules + plugins. Infrastructure (Postgres,
  RabbitMQ, Redis, MS SQL, DynamoDB, ZeroMQ) lives in plugin projects that depend only on
  `OrcaCore.Abstractions`.
- **Small steps.** One task ≈ one agent session: ≤ ~10 files touched, ≤ ~500 changed lines,
  every task file tells the agent exactly which files to read (never "explore the repo").

## Document map

| Doc | Contents |
|-----|----------|
| [00-stack-decisions.md](00-stack-decisions.md) | Decided stack, library whitelist/banlist, and the **open questions register** |
| [01-solution-architecture.md](01-solution-architecture.md) | Projects, module boundaries, plugin model, dependency rules |
| [02-engineering-conventions.md](02-engineering-conventions.md) | C#/.NET conventions, API design rules, error primitives |
| [03-tdd-workflow.md](03-tdd-workflow.md) | Test taxonomy, naming, AC traceability, the red‑green‑refactor loop |
| [04-task-protocol.md](04-task-protocol.md) | Task file template, agent execution protocol, progress tracking |
| [phases/](phases/) | Phase folders: each has a `README.md` (goal, exit criteria, task index) plus one file per task |

## Phases at a glance (mapped to spec slices)

| Phase | Spec slice | Contents | Detail level here |
|-------|-----------|----------|-------------------|
| 0 | — | Repo skeleton, quality gates, primitives | Fully detailed tasks |
| 1 | Slice 1 | Ephemeral engine core | Fully detailed for T1‑01…05; T1‑06…15 expanded by [T1‑05a](phases/phase-1-ephemeral-core/T1-05a-expand-remaining-tasks.md) |
| 2 | Slice 2 | Durable event-sourced core + **PostgreSQL plugin** | Task index |
| 3 | Slice 3 | Timers, policies, lifecycle events, observability, governance | Task index |
| 4 | Slice 4 | `ForEach`, `RunChild(ren)` | Task index |
| 4b | Slice 4b | DAG front-end, external-job composite, durable resource pools | Task index |
| 5 | Slice 5 | Saga | Task index |
| 6 | Slice 6 | RabbitMQ + remaining plugins, hosting, production readiness | Task index |

Note the deliberate deviation from the spec's slice ordering: the **PostgreSQL plugin lands
at the end of Phase 2**, not Phase 6. Reason: the provider ports (PR-010…016) must be proven
against a real database while they are still cheap to change; the certification suite
(PR-024) gets its first real consumer immediately.

## Why later phases are indexes, not detailed task files

Detailed task files for Phases 2+ are **generated at phase start** (the first task of every
phase, `Tn-00`, expands the phase README's task index into task files using the template in
[04-task-protocol.md](04-task-protocol.md); Phase 1's remainder is expanded mid-phase by
its own [T1-05a](phases/phase-1-ephemeral-core/T1-05a-expand-remaining-tasks.md)). Writing
all ~80 detailed files up front would
desynchronize from reality as earlier phases evolve, and expansion-at-start keeps every
detailed instruction consistent with the code that actually exists. `Tn-00` tasks are
Sonnet-level; most execution tasks are Haiku-level (marked per task).

## Workspace: the repository-root rule

The active implementation lives at the **repository root** on the
**`feature/v3-rebuild`** branch. The superseded prototype is preserved under
`archive/legacy-poc/`.

**Hard rules for every agent session:**

- All implementation work happens at the repository root: `OrcaCore.slnx`, `src/`, `tests/`,
  `Directory.Build.props`, and `Directory.Packages.props`.
- **Never read, reference, copy from, or modify** `archive/legacy-poc/` for active behavior.
  The spec package and current root implementation are the sources of truth.
- `docs/specs/` and `docs/implementation/` are read-only inputs (task files update only
  their phase `PROGRESS.md`).
- Where any task file says "repo root", use the repository root.

The very first task is **[T0-01](phases/phase-0-skeleton/T0-01-solution-skeleton.md)**
(solution skeleton).

## Entry point — starting implementation

Kick off a session with the prompt in **[KICKOFF.md](KICKOFF.md)** (kept there so it is
versioned and copy-pasteable). Every subsequent session uses the same prompt with the task
path swapped. To find the next task: open the current phase's `PROGRESS.md` — the next
pending entry in the phase README's task index is the target. When a phase's exit criteria
are green, the next phase begins with its `Tn-00` expansion task (Sonnet-level). Task order
within a phase follows the index; tasks whose dependencies are done may run in any order.

Model routing: use the task's **Difficulty** marker — Haiku-class models for `Haiku` tasks,
Sonnet-class for `Sonnet` tasks and all expansion tasks (`Tn-00`, T1-05a). T0-01 is
Sonnet-marked deliberately: it is the protocol smoke test, and a misconfigured skeleton
poisons every later task.

## How an agent executes one task (summary; full protocol in 04)

1. Read: this README's ground rules → [02-engineering-conventions.md](02-engineering-conventions.md)
   → [03-tdd-workflow.md](03-tdd-workflow.md) → the task file → only the files the task lists.
2. Write the task's listed tests; run them; confirm they fail for the right reason.
3. Implement the minimum to go green; refactor; re-run the full affected test suite.
4. Run the task's Definition-of-Done commands; check every DoD box.
5. Update the phase `PROGRESS.md` (one line: task id, status, notable deviations).
6. Stop. Do not start the next task in the same session.

## Traceability

Every task cites the spec requirement IDs (`CR-`, `EV-`, `DU-`, …) it implements and the
acceptance criteria (`AC-`, `JS-AC-`) it must turn green. Acceptance tests carry
`[Trait("AC", "AC-xxx")]` so coverage of the catalog in
[specs/12-acceptance-criteria.md](../specs/12-acceptance-criteria.md) is queryable at any time.
