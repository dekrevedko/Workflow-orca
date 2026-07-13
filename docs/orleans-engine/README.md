# Orleans Engine Package (agent-executable)

**Goal**: an opt-in third execution engine — `OrcaCore.Engine.Orleans` — that hosts durable
workflow instances as Orleans virtual-actor grains, reusing the existing durable
event-sourced core (aggregate, provider ports, inbox/outbox, projections). Orleans replaces
the **concurrency, lifecycle, and distribution** layer (instance lanes), never the
**persistence** layer. The package ends with the engine **complete and e2e-ready**: the
driving scenario (customer approval with `WaitLong`, timeout, and restarts) passes on a
multi-silo cluster backed by PostgreSQL.

**Current-state prerequisite (read before estimating):** the durable runtime today
publicly exposes registration and `StartOrGetAsync`; delivery/resume/complete/fail command
processing is `internal` to `Engine.Durable`
([DurableCommandProcessor.cs:391](../../v3-gpt/src/OrcaCore.Engine.Durable/Execution/DurableCommandProcessor.cs)),
and the start-idempotency port is lookup-only. An external engine therefore **cannot yet**
drive full wait/resume flows through public surfaces. Phase O1 closes exactly this gap with
two review-gated seam additions (OT1-01a public delivery dispatch, OT1-03a atomic start
reservation) before any task claims end-to-end behavior. "Reuse" in this package means:
durable semantics and Module interfaces unchanged, plus those two narrow, explicitly gated seams.

This folder is **self-contained** in the sense that matters for agents: no task requires
unbounded repository exploration. Every file an agent reads — including the existing durable/hosting
sources some tasks legitimately study — is explicitly listed in that task's "Read first"
section; the documents below plus [plan/](plan/README.md) carry everything else.

## Document map (read in this order, once per session)

| Doc | Contents |
|-----|----------|
| [01-architecture.md](01-architecture.md) | Scope, grain topology, turn model, port reuse, serialization boundary, project layout, stack delta, decisions & open questions (OOQ) |
| [02-requirements.md](02-requirements.md) | `OE-xxx` requirements — the normative spec for this engine |
| [03-acceptance-criteria.md](03-acceptance-criteria.md) | `OE-AC-xxx` catalog incl. the capstone e2e scenario |
| [04-traceability.md](04-traceability.md) | Requirement to task to acceptance-criterion matrix plus current self-audit notes |
| [plan/README.md](plan/README.md) | Phases O0–O5, complete task index, exit criteria |

Repo-wide inputs that still apply verbatim (do not re-derive):

- [implementation/02-engineering-conventions.md](../implementation/02-engineering-conventions.md)
- [implementation/03-tdd-workflow.md](../implementation/03-tdd-workflow.md)
- [implementation/04-task-protocol.md](../implementation/04-task-protocol.md) — task template, sizing, execution protocol
- [implementation/00-stack-decisions.md](../implementation/00-stack-decisions.md) — banlist stays in force; Orleans packages are whitelisted **only** as stated in [01-architecture.md §6](01-architecture.md)

## Workspace rule

All implementation work happens under **`v3-gpt/`** (the sole active lineage — the
`v3/` naming in older protocol text reads as `v3-gpt/`). Task files write explicit
`v3-gpt/...` paths. Never read, copy from, or modify the legacy root `src/` / `tests/`.

## Execution instruction (kickoff prompt)

Start every agent session with:

```text
You are implementing one task of the OrcaCore Orleans engine.

1. Read docs/orleans-engine/README.md, then docs/implementation/02-engineering-conventions.md
   and docs/implementation/03-tdd-workflow.md.
2. Read the task file: docs/orleans-engine/plan/<TASK>.md end-to-end.
3. Read ONLY the files listed under "Read first" in the task file.
4. TDD: write the listed tests first, watch them fail for the right reason, implement the
   minimum to go green, refactor, keep the affected suites green.
5. Run every command under "Definition of done"; check every box.
6. Append one line to docs/orleans-engine/plan/PROGRESS.md:
   "<TASK> | done | <date> | deviations: <none or one line>"
7. Commit as "<TASK>: <summary> (<OE/OE-AC ids>)". STOP — do not start another task.

Escalation: ambiguity → task "Assumptions" section → 01-architecture.md §7 open questions
(OOQ). Never resolve a registered OOQ inside a task; record `blocked` in PROGRESS.md instead.
```

To find the next task: open [plan/PROGRESS.md](plan/PROGRESS.md) (initialized by OT0-01); the
next pending entry in the [plan/README.md](plan/README.md) task index is the target. Phases
O2–O5 each begin with an `OTn-00` expansion task (Sonnet-level) that turns the index entries
into full task files against the code that exists at that moment.

## Review gates (human or strong-model; execution agents never pass these alone)

- end of every phase — exit criteria + drift check against [01-architecture.md](01-architecture.md);
- any change to an existing provider port or `OrcaCore.Engine.Durable` public type;
- any new package beyond the whitelist in [01-architecture.md §6](01-architecture.md);
- resolution of any OOQ.

## Traceability

Every task cites the `OE-` requirements it implements and the `OE-AC-` criteria it turns
green; [04-traceability.md](04-traceability.md) is the review matrix used to catch missing
links before execution. Acceptance tests carry `[Trait("AC", "OE-AC-xxx")]`. Where an OE requirement
restates a product-spec guarantee it cites the original (`DU-`, `PR-`, `CR-`, `EV-`) so the
two catalogs stay consistent.
