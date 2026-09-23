# OrcaCore — Implementation Guide (agent-executable)

> **First-release refactor routing (2026-07-18):** the historical phase task files record how
> the current pre-release implementation was assembled; they do not authorize restoring a
> provisional API that the v1 simplification removed or deferred. For all new work, the
> normative surface is [spec 17](../specs/17-selected-mode-capability-matrix.md), and the active
> exact authoring declarations are mirrored in
> [`17-public-authoring-contract.cs`](../specs/17-public-authoring-contract.cs). The active
> execution checklist is
> [`reshape-developer-facing-interfaces`](../../openspec/changes/reshape-developer-facing-interfaces/tasks.md)
> together with
> [`add-runtime-concurrency-limits`](../../openspec/changes/add-runtime-concurrency-limits/tasks.md).
> Preserve completed phase records as history; add new implementation work to those active task
> graphs.

This folder turns the specification package ([docs/specs/](../specs/README.md)) into an
**executable implementation program**: short, self-contained tasks that an LLM coding agent
(Haiku-level for most tasks, Sonnet/Codex-level where marked) can complete one at a time
without large context windows.

**Ground rules for everything in this folder:**

- **TDD first, always.** Every task starts by writing the listed failing tests, then makes
  them pass, then refactors. No production code before its test exists.
- **Interfaces for behavior seams; concrete values for data and authoring.** Replaceable
  services/ports are interfaces in their owning application, runtime-protocol, or provider tier.
  Approved immutable values, staged builders, definitions, outcomes, handles, and exceptions are
  concrete public contracts. Other implementation collaborators stay internal and are reached
  through DI.
- **No monolith.** The engine is internal modules plus explicit provider adapters. Advanced
  persisted records live in `OrcaCore.Runtime.Protocol`; provider SPIs live in
  `OrcaCore.Provider.Abstractions`; adapters depend inward on those tiers. Application packages
  do not reference provider adapters.
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
| [phases/](../archive/implementation-phases/) | Phase folders: each has a `README.md` (goal, exit criteria, task index) plus one file per task |

## Active change-specific execution plan

The developer-facing API refactor follows the mandatory phased implementation and independent
review gates in
[`developer-facing-interface-refactor-phased-plan-2026-07-14.md`](developer-facing-interface-refactor-phased-plan-2026-07-14.md).
Its OpenSpec task lists remain authoritative; the plan adds dependency ordering, phase exit
evidence, and a required stop-and-review checkpoint after every phase.

Review-E planning remediation has been applied and is recorded in the current
[remediation and Phase 0 status](../review/developer-facing-interface-v1-simplification-review-e-remediation-and-phase-00-status-2026-07-19.md).
The independent planning re-review approved guard retargeting. Tasks 3.1-3.10, all four 3.11
slices, and task 3.12 are complete. After two rejected intermediate guard packets, all findings
were remediated and the exact whole packet received a final immutable approval with no P0-P3
 findings. Sections 4 through 7, including the Section 7A/7B public-surface, durable-messaging, and
 application-catalog remediation, are independently approved and checkpointed. Durable retained
 pre-wait ingress, four self-routing routes, workflow-authored `Publish`, application-registered
 dispatch, exact role-specific hosting, and three complete durable providers are current product
 authority. The separate `harmonize-downstream-capability-specs` change has synchronized canonical
 requirements and is reconciling its remaining active-tree documentation and guard evidence. Task
 8.0 and Section 8 source work remain blocked until that harmonization target is independently
 approved and checkpointed.

The 2026-07-18 simplification amendment/status, 2026-07-19 construction amendment, completed
reviewer prompt, reviews A-E, their earlier consolidated review, and the root-only owner decision
are immutable historical inputs. They do not outrank the live
[matrix](../specs/17-selected-mode-capability-matrix.md),
 [exact companion](../specs/17-public-authoring-contract.cs), canonical requirements, or the live
 OpenSpec changes. `harmonize-downstream-capability-specs` is pending independent approval and its
 deltas have not been synchronized into canonical specs. Root-only `Parallel`, `ForEach`, and
 `While` remain selected, but no historical readiness verdict advances the current gate.

## Phases at a glance (mapped to spec slices)

| Phase | Spec slice | Contents | Detail level here |
|-------|-----------|----------|-------------------|
| 0 | — | Repo skeleton, quality gates, primitives | Fully detailed tasks |
| 1 | Slice 1 | Ephemeral engine core | Fully detailed for T1‑01…05; T1‑06…15 expanded by [T1‑05a](../archive/implementation-phases/phase-1-ephemeral-core/T1-05a-expand-remaining-tasks.md) |
| 2 | Slice 2 | Durable event-sourced core + **PostgreSQL plugin** | Task index |
| 3 | Slice 3 | Timers, policies, lifecycle events, observability, governance | Task index |
| 4 | Slice 4 | Historical `ForEach` and child-workflow implementation record; v1 public child members are deferred | Historical task index |
| 4b | Slice 4b | Historical DAG/external-job/resource-pool implementation record; only `OrcaCore.Dag` and scoped leasing remain in the v1 public baseline | Historical task index |
| 5 | Slice 5 | Historical Saga implementation record; Saga is deferred from the first public release | Historical task index |
| 6 | Slice 6 | RabbitMQ + remaining plugins, hosting, production readiness | Task index |

Note the deliberate deviation from the spec's slice ordering: the **PostgreSQL plugin lands
at the end of Phase 2**, not Phase 6. Reason: the provider ports (PR-010…016) must be proven
against a real database while they are still cheap to change; the certification suite
(PR-024) gets its first real consumer immediately.

## Why later phases are indexes, not detailed task files

Detailed task files for Phases 2+ are **generated at phase start** (the first task of every
phase, `Tn-00`, expands the phase README's task index into task files using the template in
[04-task-protocol.md](04-task-protocol.md); Phase 1's remainder is expanded mid-phase by
its own [T1-05a](../archive/implementation-phases/phase-1-ephemeral-core/T1-05a-expand-remaining-tasks.md)). Writing
all ~80 detailed files up front would
desynchronize from reality as earlier phases evolve, and expansion-at-start keeps every
detailed instruction consistent with the code that actually exists. `Tn-00` tasks are
Sonnet-level; most execution tasks are Haiku-level (marked per task).

## Workspace: the repository-root rule

The active implementation lives at the **repository root** on the currently checked-out
branch. The former `v3-gpt` workspace was promoted here in commit `666bc1e6`; the
superseded pre-promotion prototype is preserved under `archive/legacy-poc/`.

**Hard rules for every agent session:**

- All implementation work happens at the repository root: `OrcaCore.slnx`, `src/`, `tests/`,
  `Directory.Build.props`, and `Directory.Packages.props`.
- Do not create or use a parallel `v3-gpt/` workspace. The name is historical; root-relative
  paths are the only active implementation paths.
- **Never read, reference, copy from, or modify** `archive/legacy-poc/` for active behavior.
  The spec package and current root implementation are the sources of truth.
- Preserve operational context directories such as `.agents/`, `.claude/`, `.codex/`, and
  `.codex-run/` when present. They are agent/run metadata, not implementation artifacts to
  archive or delete during cleanup.
- `docs/specs/` and `docs/implementation/` are read-only inputs (task files update only
  their phase `PROGRESS.md`).
- Where any task file says "repo root", use the repository root.

The historical implementation program began with
**[T0-01](../archive/implementation-phases/phase-0-skeleton/T0-01-solution-skeleton.md)** (solution skeleton). That
bootstrap task and its completed phase records are provenance, not an active starting point.

## Entry point — current implementation work

Do not start from the historical **[KICKOFF.md](KICKOFF.md)** prompt or select work from an old
phase `PROGRESS.md`. Start with the
[developer-facing refactor phased plan](developer-facing-interface-refactor-phased-plan-2026-07-14.md),
then execute only an unblocked task from the active
[`reshape-developer-facing-interfaces`](../../openspec/changes/reshape-developer-facing-interfaces/tasks.md)
or
[`add-runtime-concurrency-limits`](../../openspec/changes/add-runtime-concurrency-limits/tasks.md)
graph. Respect each graph's dependency and independent-review gates; historical completion
records do not advance them.

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
