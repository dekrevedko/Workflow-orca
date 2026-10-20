# Issue #2 contract routing and review scope — 2026-10-18

R1, 2026-10-19: incorporates F1 Closed/drain versus Stopping and the two P3 notes.

This is proposed routing, not a permanent-registry activation or a source approval.
`proposed-amendment-route-2026-10-18.json` enumerates the six exact headings, block
hashes, public-catalog hash and pending tasks. No existing requirement is rewritten
in this contract target. No workflow archive/purge/pause/resume/retry operation is
introduced: StopAsync belongs to the ephemeral host lifetime.

## Named normative transition (tasks 2.1 / 2.2)

| Capability requirement | Numbered owner |
|---|---|
| Ephemeral host shutdown has one supported observation contract | PR-041; doc 17 sections 17.4 / 17.5 / 17.7 |
| Ephemeral host shutdown preserves dependency ownership | PR-041; CR-031 mode qualification; NF-032 |
| Ephemeral host shutdown closes admission atomically | MG-066: Closed versus Stopping, accepted-operation drain matrix; doc 03 Open/Closed/Stopping/Completed vocabulary |
| Completed ephemeral host shutdown proves host-wide quiescence | MG-067: seal/join only in Stopping, exact passive post-Completed access; doc 05 callback/producer ordering; NF-032 |
| Incomplete ephemeral host shutdown preserves truthful ownership | MG-068; OB-027 |
| Repeated ephemeral host shutdown observes one irreversible stop | MG-069: stop entry starts cancellation/join, Closed alone does not; cached Completed observations create no work |

MG-066–069, PR-041, NF-032, OB-027 and AC-543–554 were checked unused at the base.
They are reserved by this proposal, not inserted into the numbered documents now.
Doc 02 qualifies durable persistence lessons, doc 04 explicitly separates the
durable restart rule from ephemeral physical join, and doc 13 preserves the deferred
workflow-management registry. Doc 17 receives the complete seven-type declarations,
namespace/assembly row, reserved singleton rules and the distinction between an
observer budget, workflow/step/wait deadlines, capacity limits and retained-data age.
The authoring-only `17-public-authoring-contract.cs` has no hosting declarations
and is explicitly excluded; its bytes and executable API/compile/package records
remain unchanged through canonical transition.

Canonical synchronization must append the exact two/four ADDED blocks and retain
all earlier blocks. New named amendment records must bind real contract checkpoint
and verdict commits, remain source-pending and preserve earlier Completed records.
Any stage-aware documentation guard adaptation must still reject unapproved compiled
surface; documentation is not permission to broaden the executable API baseline.
No existing approval supplies implicit coverage for these six later requirements.

## Acceptance slots

| Proposed criterion | Owning source/evidence work |
|---|---|
| AC-543 | Exact seven-type API, singleton reservation, constructor restrictions, no new registration/options/fingerprint |
| AC-544 | Timeout ticks/range, zero/infinite, validation/pre-cancel/disposal precedence, public versus hosted cancelled entry |
| AC-545 | Closed rejects only starts/replays/compatible registrations; accepted queries/commands/ingress keep ordinary semantics; token-state closure without cancellation; preserved error precedence |
| AC-546 | Queued/pre-Init/in-flight Init and pre-handle starts; acceptance/abort/Closed election and original errors; no provisional cancellation merely from Closed |
| AC-547 | Natural completion plus delay/timer/retry/resource/yield progress in Closed; Stopping seals and joins root/branch/item work, lanes, capacity release and callback handoffs |
| AC-548 | New and existing output waits/pins during Closed; event ingress satisfying waits; normal outcomes and final notification/cleanup joined across Stopping |
| AC-549 | Noncooperative work retains physical slots and actual dependency owners after finite observation |
| AC-550 | Blocking/throwing cancellation handlers, first CancelAsync task, linked propagation and contained diagnostics |
| AC-551 | Ordered sequential Generic Host drainer and shared budget, concurrent-stop caveat, explicit-host owner and app-gated plain-DI drains in 3.4; RunAsync exception/disposal and raw-disposal negatives |
| AC-552 | Completed-before-dispose sentinels; exact post-Completed live-operation rejection, passive identities/detached data/cached result only; no DI/user work, timers, diagnostics or commits |
| AC-553 | Concurrent/repeated observers, cancellation/deadline/settlement precedence, sticky accounting fault and fresh roots |
| AC-554 | Both retention modes, Closed retention versus frozen Completed metadata, required CI/stress/coverage, RunAsync changed behavior and honest release-status migration |

Task 2.1 also owns the engine-guide drain recipes, diagrams with separate Closed
and Stopping edges, exact operation/error table in doc 17, and prerelease/migration
wording that RunAsync now throws on incomplete hosted stop while still disposing.
Task 3.1 adds deterministic barriers for every drain scenario; task 3.4 demonstrates
both Generic Host recipes and the plain-DI submission gate using candidate packages.
No engine implementation or new framework-probe acceptance credit is claimed now.
The completed issue #1 retention change is scheduled for archive and active-path/
provenance refreeze in a later independently reviewed maintenance target, not here.

The throwing step-token callback P3 in issue #1's final verdict is explicitly owned
by callback containment/accounting in source task 3.2 and AC-550; this contract does
not claim that source defect has already been fixed.

## Reverse sweep

`contract-occurrence-scope-2026-10-18.tsv` lists all 61 examined active documents and
their base hashes, including zero-hit files. It covers every numbered spec and the
authoring companion, all current docs except immutable review/archive records and
the separate planned Orleans engine, all 14 canonical specs, README and CLAUDE.
`contract-occurrence-decisions-2026-10-18.tsv` records all 120 path:line matches,
their section, exact normalized line hash and an individually written disposition:
81 Retain, 37 Augment, 2 Edit. This is a future task-2.1 edit plan; every matched
owning-document line is still unchanged in this contract target.

Retain means the matched line remains unchanged. Augment preserves the matched
line and adds the named adjacent/section context. Edit names the actual line to
rewrite. A document can gain a new owned block even when its existing matching
lines are retained or it has no keyword hit. The 24 named document targets include
the two canonical append targets and zero-hit owner files, not just grep inputs.
Task 2.1 must review every resulting individual diff against this plan and record
any reclassification; it must not stamp a default disposition over unseen edits.

The regex is literal review scope (case-insensitive):

```text
shutdown|graceful|host.stop|host lifetime|physical settlement|StopAsync|StartOrGetAsync|IHostedService|dispos(?:e|al|ed|ing|able)|quiescen|WhenSettled|EphemeralEngineHostOptions|IWorkflowDefinitionRegistry|ServiceProvider|\bIHost\b|step dependenc|service provider
```

Guides owned by the transition are the ephemeral guide/diagrams, end-to-end plan,
solution architecture/conventions, technical overview, production readiness,
prerelease/publishing status, normative source map and README. They must distinguish
proposed/canonical/source-qualified/published status. Source task 3.3/3.5 owns removing
implementation-pending wording only when the source and consumer are actually
qualified; canonical synchronization alone must not advertise an available release.
Durable driver drain/restart, DAG quiescent rollover and provider lease/reconciliation
rules are individually retained rather than borrowed as ephemeral completion proof.

## Exact current change boundary

The two changed tracked files are only the provenance fixture and guard pins. The
guard changes current artifact/catalog hashes/count and appends the former current
artifact; it removes no assertion or failure path. Its 17 predecessor artifacts and
the 189-row predecessor subset remain exact. All six new rows are PendingAddition;
semantic approval is false. The current fixture/artifact/directory inventory and
guard pins are one review target, with source/API/registry authority still absent.

Tasks 1.1–1.3 describe preparation only; 1.4 remains open for independent approval
and checkpoint. Tasks 2.1 onward are open. After approval, recheck drift, stage the
entire frozen manifest, commit it and add a verdict-only direct child. No commit,
canonical sync, source implementation, push or issue modification occurs while
this independent review is pending.
