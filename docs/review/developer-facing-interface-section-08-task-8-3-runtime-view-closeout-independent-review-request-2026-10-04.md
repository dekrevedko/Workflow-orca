# Independent review: DAG runtime-view Complete closeout and source-review notes

Date: 2026-10-04. Change: admit-dag-hosting-runtime-view, Tasks 3.1/3.2.

## Authority and next permitted step

Review this exact uncommitted closeout on base `1d8941b7c4e4a96752b33684075d152494057c49`. The source was independently APPROVED, not merely validated. Its exact chain is source checkpoint `4eb2e8d3a68e0ae7d873bd4f54c53735beefb132` (parent `13fe5b996e4758e383ea6ac68d86bdaf10b940b8`, tree `bd378abbadc5ba6989831814be1e67db0c9dcfa9`, thirty-three paths), direct-child evidence `2fdfa59a747a5d1d1667abd63a8def88168a172b`, and Task 2.4-only activation `1d8941b7c4e4a96752b33684075d152494057c49`. The 11,630-byte source APPROVE is unchanged at SHA-256 `b8c6c1da867c940e04330a0725993a77355eef29ae3a54717dc58297fb2740ce`. Evidence and activated Infrastructure each passed 243/243.

This target prepares permanent Complete evidence and records Task 3.1 complete. Task 3.2 remains open for the independent closeout review/checkpoint; only its later checkbox activation is intentionally unpinned. No product source, friend, package edge, public API, canonical spec, codec or child-start code changes. Reshape 8.3 remains open; the 8.4/8.5 bridge is a separate contract/source target. No archival is authorized by this request.

An initial evidence registration mistakenly cleared the required immutable-history manifest pointer. Infrastructure caught it; the still-local evidence object was corrected before activation. The final evidence object is the only referenced child of the checkpoint. This disclosure changes no independently reviewed source bytes or verdict.

## Exact self-inclusive freeze

- Manifest: `docs/review/developer-facing-interface-section-08-task-8-3-runtime-view-closeout-dirty-manifest-2026-10-04.txt`; raw git porcelain v1, all untracked, no renames, with the original Git order; 23 paths, 1686 bytes, SHA-256 `95061a95cf89813a813d4e191b025b8e0ddb94c2c7c25487e8ab91d656fd558d`. It includes this request and itself. Raw and NUL inventories must agree.
- Base: `1d8941b7c4e4a96752b33684075d152494057c49`; index empty. The entry set must equal HEAD diff paths plus untracked, with no phantom modified paths.
- Semantic content record: 19 rows, 2570 bytes, SHA-256 `ee8f32eb2d4338d13a719beb43fb3ed62e2a5ab4d12a0d641d7c9f95fae77176`. Read raw bytes of each manifest file except all docs/review paths and the exact immutable-document-history.json and review-manifest-provenance.json fixture paths. Emit path<TAB>raw byte length<TAB>lowercase SHA-256, sort full rows ordinally, join with LF plus one terminal LF, UTF-8 without BOM.
- Stronger active record: includes request, manifest and immutable-history admission; excludes only exact self-referential review-manifest-provenance.json. Each row is XY<TAB>path<TAB>raw length<TAB>lowercase SHA-256, ordinally sorted and LF terminated. Exact bytes/digest are in activeFreeze and the final handoff.
- The self-inclusive staged tree is reported in the final handoff after rehearsal, with this base as its parent. A request cannot embed its own tree hash without becoming self-referential. Verify the tree yourself before authorizing the checkpoint.

## Claims and all source-review observation fixes

1. Schema 9 adds a separate runtimeViewCompleteEvidence record and source activation coordinate. Exact source constants bind stage, four implementation tasks, source checkpoint/tree, direct-child evidence, terminal added APPROVE, immutable request/manifest hashes and executable paths. Earlier Proposed, ApprovedPending and authoring Complete rows remain byte-exact history; canonical and both delta specs are untouched. The activation validator reproduces exactly the one-checkbox diff. Source review is not conflated with approval of this closeout.
2. Reflection is now explicitly covered in shipped Hosting and in the linked harness. Metadata rejects System.Reflection APIs/types, Type lookup/invocation, Activator and Delegate.DynamicInvoke; only exact SDK assembly-attribute constructors and Type.GetTypeFromHandle for public typeof tokens are exempt. The compiled private NodePlans reflection probe is rejected, and neutralizing the assertion fails its own negative test. This is a static metadata policy, not detection of arbitrary dynamically generated obfuscation. No product uses or adds reflection.
3. Before running its 34 behavior assertions, the actual linked-adapter harness must obey exactly the same three non-public types and fifteen decoded signatures as the shipped adapter, plus reflection rejection. It receives no extra test friend. A harness-only PlanToken read with coherently refreshed behavior hash fails exact equality. Future bridge behavior must use the shipped Hosting assembly after 8.4/8.5 wire it; this closeout does not claim that future evidence already exists.
4. Bad host arguments continue failing closed as DAG_INPUT_MAPPING_INVALID; behavior is unchanged. Open Task 8.4 now explicitly requires the bridge to supply exactly declared successful direct resultful outputs, eagerly decoded/detached by declared type, with no missing, extra, foreign or resultless entries. Task 8.0's map and the design carry that handoff; its exact hash is pinned. Codec/materialization, commit and child start remain durable-runtime owned.
5. Current numbered/binding/guide docs now name real source approval/checkpoint. Dated entries, the source validation artifact, old contracts, source request/manifest/verdict and harness bytes remain unchanged. Decision 22 appends, never rewrites, a dated entry. All affected Task 7.3 rows/digest and numbered/map/handoff pins are refreshed. The source activation did not alter docs.
6. Provenance remains 180 rows / 47,347 B / `40d4d8c0b9144ea087d7b36d33df35d4736942ae615f50126f7ba136b20da8c1`: 173 synchronized, four superseded predecessors, three bootstrap-only, zero pending. Declaration accounting stays 340/1,405 physical and 192/717 active: no new Fact or source file is added. Product and public/source baseline fixtures are unchanged.

## Validation and inspection

Non-incremental Debug/Release warn-as-error builds: zero warnings/errors. Core 350, Ephemeral 79, Durable 99, Acceptance 37, Hosting 24, ProviderCertification 96; real PostgreSQL 101, SQL Server 72, Integration 11. Infrastructure 243/243; fourteen intentional ExpectedRed failures separately. Focused source checks pass in both configurations; canonical/provenance checks green. Strict OpenSpec 20/20. Eight green package fixtures and the one existing dag-hosting expected red; green compile fixtures verify 26 source and 26 package forbidden-member diagnostics. Final frozen and committed-state/activation results accompany the handoff.

Read-only JetBrains InspectCode 2026.2.3.1 scoped only to the changed DagRuntimeViewBoundaryGuards.cs, Release/net10.0, solution analysis enabled and no-build against fresh outputs: six RedundantNameQualifier warnings, zero errors/notes, all cosmetic explicit product qualification. The retained closeout SARIF and pinned artifact document scope/triage. The original 59-finding source report is unchanged; the different scopes are not comparable and no automatic cleanup ran.

The normalized closeout artifact is independently source-pinned. Its verification claims do not constitute independent approval.

## Independent controls to replay

| Control | Compiling isolated mutation | Expected / observed |
|---|---|---|
| C1 | Complete evidence stage becomes ApprovedPending | RED exact Complete binding |
| C2 | Evidence becomes activation, fixture and constant coherently edited | RED single-parent direct-child rule |
| C3 | Source task 2.2 reopened | RED complete task reference |
| C4 | Request edited and fixture/source hashes coherently updated | RED reviewed checkpoint blob comparison |
| C5 | Closeout artifact edited | RED permanent artifact pin |
| C6 | Reflection assertion neutralized | RED compiled private-reflection self-test |
| C7 | Harness-only PlanToken read, behavior digest repinned | RED exact harness member set |
| C8 | CP-020 current status falsified | RED durable numbered block pin |
| C9 | Activation and source constant point at an older activation | RED source-evidence parent requirement |
| A1 | Freeze-cleared future Task 3.2-only checkbox activation | GREEN, no guard source edit |

All nine negative controls compile and fail their intended focused checks in a verified detached disposable copy; all edits are restored byte-exact. Staged/committed rehearsal checks include new artifacts, which worktree-only diff --check misses. The final handoff states exact tree/anchors and final controls.

## Reviewer instructions and checkpoint sequence

Review both normative trees, actual source authority, all prior history, metadata/reflection hardening, bridge-input obligation, status pins and isolated controls. Write a new immutable APPROVE/REJECT verdict; do not edit this request/manifest or any reviewed source packet. The verdict is an additional path for a subsequent evidence commit, not part of this freeze.

Only after independent APPROVE: recheck zero drift, checkpoint exactly this manifest with the reported staged tree and current base parent; make its only-child evidence commit adding/cataloguing the byte-exact verdict and clearing the active freeze (retain the immutable-history manifest pointer); then activate Task 3.2 only. A later reshape 8.4/8.5 bridge target remains separately reviewed, and no archive is mixed into closeout.
