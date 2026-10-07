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
| 1 | `CLAUDE.md` | `b872c7e4cba50ca4dba607e62ed0fd6529e5996d6b5cf3d28425b594692ba20d` |
| 2 | `docs/eks-scheduler-handoff.md` | `48cf025958482433e34b4e52185484ed587936ba56ef4822bc96e1b08b3f9ef2` |
| 3 | `docs/end-to-end-plan.md` | `c2bb0ea26618a65248839ffd75281b9c89dc496467e5a90e9034da48fcae0e01` |
| 4 | `docs/ephemeral-engine-developer-guide.md` | `7e3fe252e1bcb621b03bfb91a6aaf2d48bf5c5ad3997e442ad4fbcb3a90dfa2c` |
| 5 | `docs/ephemeral-engine-diagrams.md` | `6393cfb7fb4155e0fc28ed908384e43126d05be9b5ebda04f6f7aee7aa25463a` |
| 6 | `docs/implementation/00-stack-decisions.md` | `3bcc3f54a75f22dd593982948997f7497c290e07845de543902be5399b45ecd2` |
| 7 | `docs/implementation/01-solution-architecture.md` | `e1b25e9d029b660939c90279d86002e9c743b39722ff3b7f595a1524e581a433` |
| 8 | `docs/implementation/02-engineering-conventions.md` | `e4a81233359e60e5ca43e596d26a58ff46e75879dded7536e95511e47d908c72` |
| 9 | `docs/implementation/developer-facing-interface-refactor-phased-plan-2026-07-14.md` | `42f97187b2e62ea4df522317ff7562b74644024289aea470ebef68e5b3d86dad` |
| 10 | `docs/normative-source-map.md` | `1bc5a4182949cbfbe87870aebf258a9fdfcfb22a8cd627d2c0b0d7fc7e61d949` |
| 11 | `docs/production-readiness.md` | `2812eb9121d455a59ec748dbdbcc4cba4ddd317398c9b8c5470edc44b9a6c1bc` |
| 12 | `docs/project-technical-overview.md` | `84ce04a1ee32d129c22e6a1b069bf56391bc4a0ea0e196b289e1daf2105489fc` |
| 13 | `docs/specs/01-concept-and-goals.md` | `36826933178462e6d83eb817cec2b51ec77cd93609a5726ad883b888c0960fb4` |
| 14 | `docs/specs/03-domain-model-and-glossary.md` | `00de9287ac0f48722c340860eaab99b87e95b534f269980bc74aa77ac9f17728` |
| 15 | `docs/specs/05-requirements-events-waits-timers.md` | `2ca82d049dab545710948a952e4ef76972ef21532fb607b83d11b4a4461e744d` |
| 16 | `docs/specs/06-requirements-durable-execution.md` | `0c65b56473ea10468d7798aca3287d5ef049817649e6f91e89080f39ae079778` |
| 17 | `docs/specs/09-requirements-management-operations.md` | `b564eb6713e446ba846141ee4561a3f5ec49fa596e580f44625a231da5d6aa2b` |
| 18 | `docs/specs/10-provider-model-and-extensibility.md` | `0654c51c4aa216a0af98bfc880095e3d504b00dc0e1bdc87d13162e3cfb5aede` |
| 19 | `docs/specs/12-acceptance-criteria.md` | `4d1e796a571055d592d8eb3b817666e63ad7823878684e32805035173e4b219c` |
| 20 | `docs/specs/13-phasing-and-open-questions.md` | `c94f49b68a771382ba452a17a2d3ccff5e6d0bba7f62b7bffeae509064edc53d` |
| 21 | `docs/specs/14-driving-scenario-eks-job-scheduler.md` | `a1378ecb50869d47a7a11c8e0e937e4cf303cd02dde8bbbd4d406cd609866800` |
| 22 | `docs/specs/16-requirements-durable-driver.md` | `b13d8061b63438de9f198c17d16c3fb3ce9eb6b7cf900fc093fb48dcfe0809a2` |

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
