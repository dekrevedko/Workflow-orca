# Issue #2 canonical transition registry — 2026-10-19

Stage: `ContractApprovedCanonicalReviewPending`. Source approval is null; canonical
approval is null. Structural synchronization and provenance eligibility are not
independent approval. Tasks 2.1/2.2 and 3.1–3.5 remain open; 1.4 records the completed
contract checkpoint only.

Contract checkpoint `3c8d33c1c89a3b7d4f1b0cd3a8c49ced5f3e4d21`, sole parent `fa88986aaac219c998b15e96748efdf2f245de51`, tree `b1611b09f2774bab0a84ae2a1492abd71a1e4459`.
Verdict-only direct child `4bd7c3e2f179a92bb5161edc16fbc0950d4b4126` contains only `openspec/changes/add-ephemeral-host-shutdown/artifacts/contract-r1-independent-review-verdict-2026-10-19.md`,
4,012 B / SHA-256 `fdac6ecd148ffa1ef936e1f9e27a5a80751ba554c5fb2aaaca7b7e3da94955ce`. Guards resolve all facts through Git and compare
the committed verdict with the working bytes; lookup errors fail closed.

The verdict explicitly binds F2 into this transition: initialization admitted before
Closed may finish and publish Accepted during drain. Only still-unaccepted starts
losing the Stopping election are aborted, with authoritative original errors and
physical ownership preserved. Queued pre-Init and new/replayed starts remain closed.
The two changed management blocks are the only normative correction from the
checkpoint; the other four blocks and all seven public declarations are unchanged.

Corrected public catalogue: `openspec/changes/add-ephemeral-host-shutdown/artifacts/task-2-1-public-api-2026-10-19.md`, normalized SHA-256 `d1232fdfd991b19945b282f9d05ec888356f3daa65b2c57dabe584da0ad3232b`.
It supersedes the dated contract catalogue for current semantics only; all old
contract artifacts and verdicts remain immutable. No executable baseline changes.

| Capability | Requirement | Corrected canonical/delta SHA-256 |
|---|---|---|
| `developer-facing-surface` | Ephemeral host shutdown has one supported observation contract | `6dc7fce06a29742d54ce23bc63644852d62c8236e7b3506f03733c5f13271ce5` |
| `developer-facing-surface` | Ephemeral host shutdown preserves dependency ownership | `389568673e999728c85dd4bbf2e8984089ecb677d23b18786ce136ee920847e0` |
| `management-and-querying` | Ephemeral host shutdown closes admission atomically | `009db0deff82740ebecb0d5b92027d576b3686316d8a861493142d3eb8409fc9` |
| `management-and-querying` | Completed ephemeral host shutdown proves host-wide quiescence | `1cbcd063c33b69eb7c578338954b5f4cb6cddbb67bf0d84a6d802a745653fc21` |
| `management-and-querying` | Incomplete ephemeral host shutdown preserves truthful ownership | `5f735fd7e411a39157f7241ae5bef7641f0d54cf049724d3ea17e58b8e6829d3` |
| `management-and-querying` | Repeated ephemeral host shutdown observes one irreversible stop | `3ad75931371674f27e36afd2497ec9c4f1f22a0909a018da185535ef1227d4cb` |

Two registry records are reserved solely for these six identities: ephemeral-shutdown-
developer-facing-surface (2) and ephemeral-shutdown-management-and-querying (4).
Both have zero pending canonical operations and remain canonical-review/source pending.
This is separate from the completed issue #1 retention records and every DAG record;
none is rewritten or borrowed as authority.

Numbered routing: MG-066–069, PR-041, NF-032, OB-027, AC-543–554 and doc 17 sections
17.4/17.5/17.7, plus the exact 22 document targets in task 2.1. The authoring-only
declaration companion is ExcludedNoHostingDeclarations; no compiled API, positive/
negative compile fixture, package baseline, source or source test is activated.
Operators use existing stuck-step/lifecycle facts and live reads before Stopping;
Incomplete itself supplies only a count/reason. The three drain recipes, concurrent-
stop caveat and RunAsync exception/finally warning are required guide content.
Issue #1 archive/path/provenance work is explicitly a later target.

Individual occurrence resolution: `openspec/changes/add-ephemeral-host-shutdown/artifacts/task-2-1-occurrence-resolution-2026-10-19.tsv`, 120 rows, SHA-256 `ad0e5b9df73ee245ed849d501760e31d2eb30df882a834ab6d0f4edc074a9283`.
All 81 Retain and 37 Augment matched lines survive; only the two named Edit lines
change. Every augmentation has individually written section context.
