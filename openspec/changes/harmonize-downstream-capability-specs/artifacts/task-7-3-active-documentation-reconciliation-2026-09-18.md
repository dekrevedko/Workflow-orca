# Task 7.3 — active documentation reconciliation

**Date:** 2026-09-18
**Change:** `harmonize-downstream-capability-specs`
**Owner:** Task 7.3

## Scope

Task 3.1 found 23 stale active documents. Task 7.3 reconciles the 22 documents below; Task 7.4 separately owns `docs/orleans-engine/README.md`. No archived or immutable review record is rewritten.

Each SHA-256 is over the complete source after CRLF/CR normalization to LF. The executable guard
requires the numbered rows, paths, and hashes exactly as recorded here.

| # | Active source | Normalized SHA-256 |
|---:|---|---|
| 1 | `CLAUDE.md` | `2ed3489d3479d03003d39096c0da8b864d73a7fd913f6385448c8a57097bf0c4` |
| 2 | `docs/eks-scheduler-handoff.md` | `48cf025958482433e34b4e52185484ed587936ba56ef4822bc96e1b08b3f9ef2` |
| 3 | `docs/end-to-end-plan.md` | `ef67c5f0ad8f1600167efb5928e64c30879c98837cad8e8a86276a0c6c377292` |
| 4 | `docs/ephemeral-engine-developer-guide.md` | `091ac6a1fe608e65433c3f7355d4b327f0a2ed049b9ef7b09781da1fa8c78786` |
| 5 | `docs/ephemeral-engine-diagrams.md` | `3f4e32e272c0acb5c073bbc44be5af5315c50ec887721d433d0817d4f5add62d` |
| 6 | `docs/implementation/00-stack-decisions.md` | `1b39a38f880bc6f37d1ce8fd3fbf46361f9f4d1cdde1ff3f78e5c648bedc4471` |
| 7 | `docs/implementation/01-solution-architecture.md` | `79307afab283a4e0afd4a750800f9c83f6eeadf6e62fbb5c5b04916f9428131e` |
| 8 | `docs/implementation/02-engineering-conventions.md` | `44f2c645a8c4dbcd2f8b774cfcdd689d69cbdfb1435a6cf2153cb405c593f78d` |
| 9 | `docs/implementation/developer-facing-interface-refactor-phased-plan-2026-07-14.md` | `d7ffed1592a748662805d039ac99f32e8f070eab7fc7d957cc83ee96342f572a` |
| 10 | `docs/normative-source-map.md` | `7cb1cf7d4678bc897198627ab261e2e50c6040b83a310cf87ce7758e07e2ac40` |
| 11 | `docs/production-readiness.md` | `8d3ffb9b52ef4b6bfc282023abd27c753bcf530ddfd36bf53df1d25e74910929` |
| 12 | `docs/project-technical-overview.md` | `c2c837223cd25bdaffec8fcab999b6ba0c38c2171ee7673fe1f79c014103a5c4` |
| 13 | `docs/specs/01-concept-and-goals.md` | `36826933178462e6d83eb817cec2b51ec77cd93609a5726ad883b888c0960fb4` |
| 14 | `docs/specs/03-domain-model-and-glossary.md` | `5750f167b24f5d3b66368583690c011372c706d9f3ad26dbafb3520571ef09bc` |
| 15 | `docs/specs/05-requirements-events-waits-timers.md` | `c546e72793148ce5e34f0868b36170316e0ca9d34b830ad8bb2b73267df427f2` |
| 16 | `docs/specs/06-requirements-durable-execution.md` | `c726bcc03d1e5ae8f7afc34b4a54ed50ef63da54f9c50f486779292197f00a08` |
| 17 | `docs/specs/09-requirements-management-operations.md` | `faa6626fb9dddd964ae412a63adf5005766f597674dc757167c4b9f3432ce274` |
| 18 | `docs/specs/10-provider-model-and-extensibility.md` | `3fb89caac6345f6acc9c9f48be738b13aa29b9923fa8ae7bc63618026ef89dde` |
| 19 | `docs/specs/12-acceptance-criteria.md` | `46e576a1102919383c2647323275e4534da45334c9408011add9d8674cb834aa` |
| 20 | `docs/specs/13-phasing-and-open-questions.md` | `6d018a7e3b6806956bbc4199568dc7474064a779334b583385df873ff3b48a08` |
| 21 | `docs/specs/14-driving-scenario-eks-job-scheduler.md` | `a1378ecb50869d47a7a11c8e0e937e4cf303cd02dde8bbbd4d406cd609866800` |
| 22 | `docs/specs/16-requirements-durable-driver.md` | `2f7e9845b13d25c5e9cb11898d669c911368a08d4c87c9f2843a4ff56e258ffe` |

## Reconciled contract

- Durable external ingress is `IWorkflowEventIngress` over one fixed-codec `WorkflowInboundEvent`;
  the caller creates its global `EventId` and supplies its required `CorrelationId`.
- The closed route union is direct, correlation, definition fanout, and start-or-deliver.
- `EventId` plus the complete normalized-envelope fingerprint is global identity before route state.
- Accepted durable events remain owned before a wait exists; later claim needs no broker redelivery or hot instance.
- Only `Accepted` and `Duplicate` authorize source acknowledgement; unresolved ownership becomes observable poison.
- Definition fanout freezes the complete current nonterminal target set once and deduplicates per target.
- Durable authored `Publish` commits through the workflow-event outbox and reaches only the
  application-registered `IWorkflowEventDispatcher`; the engine consumes but does not create that
  implementation.
- Exact engine/provider/DAG roles remain separate; PostgreSQL and SQL Server are the production durable providers.
- `orcacore-json-v1` remains the nonreplaceable durable codec.
- Broad statistics/enumeration and public archive/purge remain absent from application handles; provider/operator ports own retained statistics and maintenance.

## Numbered owners

`docs/specs/05-requirements-events-waits-timers.md` now owns the exact four-route ingress, retained
pre-wait acceptance, global identity, closed acceptance result, broker-ack boundary, portable
dynamic waits, and transactional Publish requirements. `docs/specs/12-acceptance-criteria.md`
retains the unrelated timeout/collation clauses and maps the Section 7B obligations to real product,
provider, hosting, and engine tests rather than to the Markdown reconciliation guard itself.

## Disposition

All 22 Task 7.3 sources are reconciled. The separate Orleans future-hosting note remains open under
Task 7.4, and repository-wide recurring stale-negative enforcement remains open under Task 7.5.

Only the owner of a reviewed change that intentionally edits one of these 22 sources may refresh its
recorded hash. Task 7.4 or Section 8 may refresh a row only in the same frozen target that
intentionally edits the source and updates the artifact row, guard-source artifact digest, and
review evidence; neither may perform a mechanical follow-up refresh for an earlier unreviewed edit.

## Task 8.1 intentional source refresh (2026-09-23)

The final normative-source classification audit found that this active map still labelled its
review/archive counts as current, although later immutable records had changed them. Task 8.1
changed only its classification snapshot: at base `b660d2d5b58d4b06663dd24f6a94a9c71ab37342`,
`docs/review/` contains 269 files and `docs/archive/` contains 200 (including 40 under `plans/`).
The map now says these are dated counts, not live invariants, and distinguishes the 19 normative
package files from the expressly non-normative semantic appendix. Row 10 alone is refreshed above;
the other 21 Task 7.3 source hashes and the approved Section 7B contract wording are unchanged.
This refresh belongs to the same frozen Task 8.1 target and requires its independent exit review.

## Reshape Task 8.0 intentional source refresh (2026-09-26)

The harmonization closeout is now independently approved, checkpointed, and activated.
Reshape Task 8.0 updates the phased plan's live gate status while keeping Section 8 source work
blocked on Task 8.0's own independent review and checkpoint. Only row 9 is refreshed above;
the other 21 source hashes and the historical Task 8.1 review record remain unchanged. The
new row, this artifact digest, and its guard-source pin belong to the same Task 8.0 frozen target.

## Task 8.2 authoring-friend source candidate (2026-09-28)

The approved `OrcaCore -> OrcaCore.Dag` authoring edge is now compiled in the Task 8.2
source candidate, constrained by compiled non-public type- and member-reference checks; it
grants no durable runtime or child-start access. Rows 1, 6, 7, 12, 14, and 18 are refreshed
above with their active source edits in the same frozen implementation target. The other 16
rows and the historical Task 8.0 review record are unchanged. This status does not claim
independent approval of the source candidate or a Task 8.2 checkpoint.

## Task 1.1 runtime-view contract proposal refresh (2026-10-02)

The separate `admit-dag-hosting-runtime-view` contract target intentionally edits rows 1, 6, 7,
12, 14 and 18 to distinguish the compiled authoring friend from a proposed ninth runtime-view
friend, with codec/committed-byte/child work retained on the durable bridge. The other sixteen
source rows remain unchanged. Docs 08 and 17, outside this 22-source catalog, are explicitly
amended in the same target for runtime-view and successful-null wording. This refresh is
proposal-only: no canonical sync, friend attribute or source authority is implied.

## Proposed complete DAG run contract refresh (2026-10-04)

The Task 1.1 target intentionally refreshes every edited source row for the full
stream/provider/lifecycle proposal. Doc 08 and doc 17 carry separate proposed
status pins outside this 22-source catalog. The canonical four blocks and main
source remain unchanged; no proposal text is claimed approved or compiled.

## Proposed RC-1 DAG run contract pin refresh (2026-10-04)

The reviewed replacement target refreshes rows for the explicitly proposed lease
coordinator/FIFO/cleanup/storage clarifications. All prior dated review packets
and rejected contract artifacts remain unchanged. No canonical/source implementation
is claimed by these row hashes.

## Proposed RC-2 DAG run contract pin refresh (2026-10-05)

Rows reflect out-of-turn evaluation and all round-96 operational clarifications.
Both rejected packets and their artifacts remain byte-exact. No canonical/source
implementation or runtime liveness proof is claimed by these hashes.

## Proposed RC-3 DAG scheduling pin refresh (2026-10-05)

Rows disposition isolated run evaluation, zero look-ahead, authored admission and
ready-hint recovery, preserving all three rejected packets/artifacts byte-exact.
No runtime liveness/source approval is claimed by document hashes.

## Approved RC-3 atomic-transition pin refresh (2026-10-05)

Status rows identify approved RC-3 contract/source pending and the safe-discovery
addendum; duplicate scheduling section removed. No source behavior is claimed.

## Owner-authorized GitHub prerelease source refresh (2026-10-16)

The owner reopened prerelease distribution and explicitly changed the engine coverage gate to 75%.
Only rows 1 and 11 change: repository CI guidance and the pre-production distribution note.
The approved Section 7B contracts and the other twenty rows remain unchanged. This artifact
and its guard-owned digest are refreshed in the same publishing target. Validation and scope
are recorded in docs/github-publishing-status.md; no product API or phase exit is claimed.

## Issue #1 Section 4 source and guide refresh (2026-10-17)

Only the ephemeral guide and diagram rows change with the implemented retention policy
and operation pins. The other twenty rows and all historical review records stay unchanged.
The same Section 4 target updates this maintained table and its guard digest. Doc 17 is
outside this catalog and has its own signature guard. Source approval and fresh package-
consumer qualification remain separate gates.

## Owner-authorized NuGet.org distribution refresh (2026-10-16)

The owner requested NuGet.org publishing configuration after the GitHub prerelease was published.
Row 11 now records the trusted publishing workflow and the remaining account/policy setup.
Only that distribution note and its maintained hash are refreshed; the other twenty-one
source pins and the approved Section 7B contracts remain unchanged. This records publishing
configuration, not completed account setup, live registry availability, or a product phase exit.

## Issue #1 final integration (2026-10-18)

This pending merge combines the Section 4 retention documentation with master
`1c86c23` publishing updates. Each maintained row records its actual combined
source. Historical review evidence remains immutable; the final review covers
this table, its guard pin, the consumer results and the two-parent merge.

## Issue #2 canonical transition (2026-10-19)

Only rows whose active documents this target intentionally changes are refreshed.
The same target updates their current table hashes and guard-owned digest. Contract
3c8d33c / verdict 4bd7c3e requires F2 initialized-start acceptance during Closed and
Stopping-only abort. Guides mark shutdown implementation/package pending, document
the three drain recipes and count-only diagnosis, and preserve all Section 7B
obligations. This table is maintained source accountability, not a rewritten verdict
or independent approval; canonical review is pending and source remains blocked.

## Issue #2 Section 3 source candidate (2026-10-19)

The canonical transition is approved at 2e51fbd / 6253f6c. This maintained table
refreshes only the current guides and doc 17 rows changed by the coherent shutdown
source/candidate-package target. They describe implemented candidate behavior and
all three owner/drain recipes, while explicitly retaining independent source review
and publication gates. Historical verdicts and canonical approval evidence are
unchanged. No released-package shutdown capability is inferred from source status.

## DAG Task 2.2 pump source candidate (2026-11-01)

Only row 1 changes: CLAUDE.md now records the independently checkpointed Task 2.1
protocol/provider data plane and this pending Task 2.2 pump target. Mapping, the
capable Dag.Hosting ready sink/scheduler and full run bridge remain Task 2.3.
All other source rows and historical review records remain unchanged.

## DAG Tasks 2.4/3.1 evidence closeout candidate (2026-11-15)

Rows 1, 6, 7, 9, 12, 15, 16, 22 refresh only the current DAG source/evidence status in the
edited numbered and guide documents. The previously reviewed requirements,
API baselines, canonical sync, and expected-red dispositions are unchanged.

## DAG Task 8.6 Complete activation (2026-11-16)

The maintained rows now pin actual current documents directly. The facade candidate's historical live-byte projection is retired in favor of immutable checkpoint blobs. The two real implementation/merge review chains and owner activation direction replace pending-review status; no other Section 8 completion is claimed.

Row 1 refreshes the CLAUDE.md status edited in the activation target; rows 4, 6, 7, 10, 14, 17, 18, 19 and 20 consolidate the exact document bytes already reviewed at merge `1e960fc4bf67ad6d1a8c053602d4a58964b47d79`, previously reached through the retired live-byte adapters, rather than admitting new or unreviewed document edits.
