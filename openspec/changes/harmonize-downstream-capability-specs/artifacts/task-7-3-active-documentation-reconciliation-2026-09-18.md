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
| 1 | `CLAUDE.md` | `a062ddcd06ffefe934ac6d8c57ed35f4d5352933c9211c543266399e5e38e960` |
| 2 | `docs/eks-scheduler-handoff.md` | `48cf025958482433e34b4e52185484ed587936ba56ef4822bc96e1b08b3f9ef2` |
| 3 | `docs/end-to-end-plan.md` | `c2bb0ea26618a65248839ffd75281b9c89dc496467e5a90e9034da48fcae0e01` |
| 4 | `docs/ephemeral-engine-developer-guide.md` | `7e3fe252e1bcb621b03bfb91a6aaf2d48bf5c5ad3997e442ad4fbcb3a90dfa2c` |
| 5 | `docs/ephemeral-engine-diagrams.md` | `6393cfb7fb4155e0fc28ed908384e43126d05be9b5ebda04f6f7aee7aa25463a` |
| 6 | `docs/implementation/00-stack-decisions.md` | `b754bb49f39ac1b33cd415b1a0c94d7dc5fbbf3983ff3d7b52f56d11a479fe2b` |
| 7 | `docs/implementation/01-solution-architecture.md` | `1d77afa2b5bd707854e7d15664e6d1a8eb6732190dcddbfd2d1e261b042f58ba` |
| 8 | `docs/implementation/02-engineering-conventions.md` | `e4a81233359e60e5ca43e596d26a58ff46e75879dded7536e95511e47d908c72` |
| 9 | `docs/implementation/developer-facing-interface-refactor-phased-plan-2026-07-14.md` | `bab0317a5fbb0258764dd05a7896cd2f406e5f705aaa85f84f2d455cbb7aab4b` |
| 10 | `docs/normative-source-map.md` | `1bc5a4182949cbfbe87870aebf258a9fdfcfb22a8cd627d2c0b0d7fc7e61d949` |
| 11 | `docs/production-readiness.md` | `2812eb9121d455a59ec748dbdbcc4cba4ddd317398c9b8c5470edc44b9a6c1bc` |
| 12 | `docs/project-technical-overview.md` | `62075350bba4adb0ad7f1698d8e7d271eb8f0e00df7071ab0adcb81355b8dc40` |
| 13 | `docs/specs/01-concept-and-goals.md` | `36826933178462e6d83eb817cec2b51ec77cd93609a5726ad883b888c0960fb4` |
| 14 | `docs/specs/03-domain-model-and-glossary.md` | `d6a7b30d3e5285dd9c7c3c5e3bc1ec1de4ebe6befe14ed10aec31f728e98b5f9` |
| 15 | `docs/specs/05-requirements-events-waits-timers.md` | `a2ed8dd5f8e1fb0893a0a1f8cea412508584c39d1066e5488d8d87f99cc03769` |
| 16 | `docs/specs/06-requirements-durable-execution.md` | `aaa23cfe12900c5e6627ca9309275e69407edd84454a631d6ad70d951fcd0411` |
| 17 | `docs/specs/09-requirements-management-operations.md` | `b564eb6713e446ba846141ee4561a3f5ec49fa596e580f44625a231da5d6aa2b` |
| 18 | `docs/specs/10-provider-model-and-extensibility.md` | `ef401ba322643c8bd185792b7414cdf72fc9efaae4eadc126208a10c12c6e75e` |
| 19 | `docs/specs/12-acceptance-criteria.md` | `c4a08fde9c6f453a2a109f959ef0bb369fce68d4115e051304af484276f5639b` |
| 20 | `docs/specs/13-phasing-and-open-questions.md` | `c94f49b68a771382ba452a17a2d3ccff5e6d0bba7f62b7bffeae509064edc53d` |
| 21 | `docs/specs/14-driving-scenario-eks-job-scheduler.md` | `a1378ecb50869d47a7a11c8e0e937e4cf303cd02dde8bbbd4d406cd609866800` |
| 22 | `docs/specs/16-requirements-durable-driver.md` | `eb2d2093a1e5994227c2fc849eceafea4b0759fdb284bf3f74e1db2ce45aa861` |

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
