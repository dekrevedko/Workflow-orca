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
| 1 | `CLAUDE.md` | `5855c56f8ba36c57823b8983381784ffcc65028b97103fe1c6032b96ed7a9181` |
| 2 | `docs/eks-scheduler-handoff.md` | `48cf025958482433e34b4e52185484ed587936ba56ef4822bc96e1b08b3f9ef2` |
| 3 | `docs/end-to-end-plan.md` | `c2bb0ea26618a65248839ffd75281b9c89dc496467e5a90e9034da48fcae0e01` |
| 4 | `docs/ephemeral-engine-developer-guide.md` | `7e3fe252e1bcb621b03bfb91a6aaf2d48bf5c5ad3997e442ad4fbcb3a90dfa2c` |
| 5 | `docs/ephemeral-engine-diagrams.md` | `6393cfb7fb4155e0fc28ed908384e43126d05be9b5ebda04f6f7aee7aa25463a` |
| 6 | `docs/implementation/00-stack-decisions.md` | `7af028ced9654ec4fde9557cec581e3ec388504278733964220831113a93e011` |
| 7 | `docs/implementation/01-solution-architecture.md` | `b8aee192b60568e49da66d1e5f9fd94df74a1187e63d3e324866d49c0989f989` |
| 8 | `docs/implementation/02-engineering-conventions.md` | `e4a81233359e60e5ca43e596d26a58ff46e75879dded7536e95511e47d908c72` |
| 9 | `docs/implementation/developer-facing-interface-refactor-phased-plan-2026-07-14.md` | `f339914444126a326e14884a0fd68319370855f99cfde42f1d19cb0b69f823b4` |
| 10 | `docs/normative-source-map.md` | `1bc5a4182949cbfbe87870aebf258a9fdfcfb22a8cd627d2c0b0d7fc7e61d949` |
| 11 | `docs/production-readiness.md` | `2812eb9121d455a59ec748dbdbcc4cba4ddd317398c9b8c5470edc44b9a6c1bc` |
| 12 | `docs/project-technical-overview.md` | `9ffd82140d233e112e7dbd1e14454c22c9e20922c878b6bcfb3e833d6344e077` |
| 13 | `docs/specs/01-concept-and-goals.md` | `36826933178462e6d83eb817cec2b51ec77cd93609a5726ad883b888c0960fb4` |
| 14 | `docs/specs/03-domain-model-and-glossary.md` | `a2199d84314d3606ed1de1e772f105ffb6ecdb275174f92f0d8058e9179470ea` |
| 15 | `docs/specs/05-requirements-events-waits-timers.md` | `12bee618af726ada4c64b92fb64fd97118120376875f821bfd4b71b038f6faec` |
| 16 | `docs/specs/06-requirements-durable-execution.md` | `405a27be8f54518c9989b388331e1523986679cea19b3ba52b8a9a61fd52469f` |
| 17 | `docs/specs/09-requirements-management-operations.md` | `b564eb6713e446ba846141ee4561a3f5ec49fa596e580f44625a231da5d6aa2b` |
| 18 | `docs/specs/10-provider-model-and-extensibility.md` | `842ec5cb2cade41f21812e01b7bab1859b310a16ce43a7843fb6672bcf6dde1e` |
| 19 | `docs/specs/12-acceptance-criteria.md` | `09b0b9d861f5d353bbca8a9aed7bcfae11f4b5456a3a71f92852b01a726a46ef` |
| 20 | `docs/specs/13-phasing-and-open-questions.md` | `c94f49b68a771382ba452a17a2d3ccff5e6d0bba7f62b7bffeae509064edc53d` |
| 21 | `docs/specs/14-driving-scenario-eks-job-scheduler.md` | `a1378ecb50869d47a7a11c8e0e937e4cf303cd02dde8bbbd4d406cd609866800` |
| 22 | `docs/specs/16-requirements-durable-driver.md` | `d04d92689108d7540fbbba8d1b6ae15df999cbc667bec538044d0f97590ff8a7` |

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

## Proposed RC-3 DAG scheduling pin refresh (2026-10-05)

Rows disposition isolated run evaluation, zero look-ahead, authored admission and
ready-hint recovery, preserving all three rejected packets/artifacts byte-exact.
No runtime liveness/source approval is claimed by document hashes.
