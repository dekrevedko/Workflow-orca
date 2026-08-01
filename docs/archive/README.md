# Documentation archive

Superseded documentation, preserved for provenance. **Nothing here is normative, current, or an
approval baseline.**

## Rules

1. **Do not treat any file here as evidence of current behavior.** These documents intentionally
   describe superseded designs — public `RunChild`/`RunChildren`, `WhenFirst`, `WaitLong`, Saga, the
   event-driven prototype, `Quick Engine` naming, and removed provider ports.
2. **Do not copy signatures, names, or diagrams from here into source, guards, or new
   documentation** without an explicit matrix/spec amendment.
3. **Do not edit these files to match a later contract.** They are provenance; rewriting them
   destroys the record of what was decided when.
4. **A document becomes implementation input again only** after an explicit amendment promotes it
   back out of this directory.

For current work use [`../normative-source-map.md`](../normative-source-map.md), which names every
active source and links the two normative trees.

## Contents

| Directory | Was | Why archived |
|---|---|---|
| `architecture/` | `docs/architecture/` | Its own README declared the directory "design exploration… not the first-release approval baseline". 12 files are named as superseded in `docs/specs/README.md` provenance; the remainder are March–April 2026 and reference removed types (`WorkflowEngine`, `EventDrivenWorkflowEngine`, `IOutboxDispatcher`, `Quick Engine`). |
| `requirements/` | `docs/requirements/` | Self-declared "Historical Requirements Tree… no longer the active first-release baseline". Superseded by `docs/specs/`. |
| `plans/` | `docs/plans/` | Roadmaps and acceptance plans; README declared the directory historical. Superseded by the OpenSpec task graphs. |
| `research/` | `docs/research/` | Prior-art and competitor studies (2026-03). Synthesized into `docs/specs/`. |
| `reviews/` | `docs/reviews/` | Single consolidated findings file (2026-03-17), superseded by `docs/review/`. The near-identical directory name was itself a hazard. |
| `durable/` | `docs/durable/` | All five files "Saved on 2026-03-16"; superseded by `docs/specs/06`, `16`, and the durable-driver notes at `docs/`. |
| `implementation-phases/` | `docs/implementation/phases/` | 109 files. `docs/implementation/README.md` declares them "historical phase task files… they do not authorize restoring a provisional API that the v1 simplification removed or deferred." Includes task docs for removed/deferred capabilities (`T2-08-durable-waits-waitlong.md`, `T4B-04-run-external-job-composite.md`, `phase-5-saga/`). The active execution checklists are the OpenSpec task graphs. |

## Path redirects

Frozen review records under `docs/review/` cite pre-archive paths. Records are immutable evidence
and are **not** rewritten, so resolve their links through this table:

| Cited path | Now |
|---|---|
| `docs/architecture/…` | `docs/archive/architecture/…` |
| `docs/requirements/…` | `docs/archive/requirements/…` |
| `docs/plans/…` | `docs/archive/plans/…` |
| `docs/research/…` | `docs/archive/research/…` |
| `docs/reviews/…` | `docs/archive/reviews/…` |
| `docs/durable/…` | `docs/archive/durable/…` |
| `docs/implementation/phases/…` | `docs/archive/implementation-phases/…` |

Git history is preserved — every file was moved with `git mv`, so `git log --follow` works across
the move.

## Not archived

- [`docs/orleans-engine/`](../orleans-engine/README.md) — **planned**, not superseded. A future
  durable-host variant that carries no v1 obligation.
- [`docs/review/`](../review) — dated review records. Frozen, but active provenance.
