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
5. **Correct historical conclusions with a new dated superseding record.** Existing files under
   both historical-document roots are immutable; only this active archive index and the reusable
   review template are mutable. Add every new review or archive record to `appendOnlyRecords`. During
   its reviewed freeze, set `activeFreezeManifestPath` to the manifest that names every uncommitted
   record. The guard binds each record to that active freeze and, after checkpoint, requires every Git
   addition of that path to reproduce the catalogued normalized bytes. Re-adding identical content is
   allowed; any differing addition fails. Every committed post-baseline path must remain present
   and cataloged; deletion or relocation first requires an explicit reviewed tombstone mechanism. Update this index to route
   readers to the superseding record instead of rewriting the historical file.

For current work use [`../normative-source-map.md`](../normative-source-map.md), which names every
active source and links the two normative trees.

## Contents

| Directory | Was | Why archived |
|---|---|---|
| `architecture/` | `docs/architecture/` | Its own README declared the directory "design exploration… not the first-release approval baseline". 12 files are named as superseded in `docs/specs/README.md` provenance; the remainder are March–April 2026 and reference removed types (`WorkflowEngine`, `EventDrivenWorkflowEngine`, `IOutboxDispatcher`, `Quick Engine`). |
| `requirements/` | `docs/requirements/` | Self-declared "Historical Requirements Tree… no longer the active first-release baseline". Superseded by `docs/specs/`. |
| `plans/` | `docs/plans/` plus later superseded root guides/status records | Roadmaps and acceptance plans; the former directory README declared them historical. It now also preserves the Phase-0 kickoff, pre-v1 ephemeral guide/diagrams, dated durable-driver audit/status, the old end-to-end plan, and the superseded 25-file Orleans plan that contradicted the selected v1 surface. |
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
| pre-2026-08-01 `docs/ephemeral-engine-developer-guide.md` | `docs/archive/plans/ephemeral-engine-developer-guide.md` |
| pre-2026-08-01 `docs/ephemeral-engine-diagrams.md` | `docs/archive/plans/ephemeral-engine-diagrams.md` |
| `docs/durable-driver-status.md` | `docs/archive/plans/durable-driver-status.md` |
| `docs/durable-driver-audit.md` | `docs/archive/plans/durable-driver-audit.md` |
| pre-2026-08-01 `docs/orleans-engine/…` | `docs/archive/plans/orleans-engine-pre-v1/…` |
| pre-2026-08-01 `docs/end-to-end-plan.md` | `docs/archive/plans/end-to-end-plan-pre-v1.md` |

Git history is preserved — every file was moved with `git mv`, so `git log --follow` works across
the move.

## Not archived

- [`docs/orleans-engine/`](../orleans-engine/README.md) — clean **planned** boundary for a future
  durable-host variant that carries no v1 obligation. Its superseded task plan is archived.
- [`docs/review/`](../review) — dated review records. Frozen, but active provenance.
