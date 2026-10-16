# Independent review: first Task 2.1 source slice — typed stream identity

Date: 2026-10-07. Change: admit-durable-dag-run-progression.
Base/parent: 89e3b18aed62446583211478f1eb8545e4fb85ed; tree 40a092193bd03149f0f36dc2baf3a1fb89c3db0d.
This is one bounded UNCOMMITTED source target, with 44 commit-real paths and an empty live index. The [raw Git-order manifest](developer-facing-interface-section-08-task-8-4-dag-run-task-2-1-stream-identity-dirty-manifest-2026-10-07.txt) is self-inclusive.

## Approval boundary

Review only the typed stream identity (seven of the approved 365 API keys) and workflow-only port rejection, with the last review's carry-forward protections. Task 2.1 remains OPEN: no run records, codecs, schemas, provider DAG persistence, leases, discovery, transitions, pump, Hosting bridge, runtime capability or DG-P exit is claimed. The other 358 API keys are not implemented or baselined here. Tasks 2.2–3.2 and reshape 8.4/8.5 remain open; all fourteen intentional-red guards retain their disposition. No friend, dependency edge, option or mapper-execution promise changes.

The reviewed transition chain is checkpoint 1770b2b979ca8f8c4b385637ddabe5d28c45dbb2 / tree 61b5900c0a6a87c34429e15c9cad7832d891c615, direct-child evidence 18bf8656fdbcbf061d4f223afca6ed438efee3ea, and checkbox-only activation 89e3b18aed62446583211478f1eb8545e4fb85ed. The transition verdict remains byte-exact (17,572 B / e89e7066308bc906e6b94b073828471f8481a11b919ea711df1e89f55018cca6).

## Source and API proof

RC-3 section 2 and the independently approved API catalog authorize DurableAggregateKind (Workflow=0, DagRun=1), WorkflowStreamId.AggregateKind, nullable DagRunId and ForDagRun(Guid). The workflow constructor, InstanceId, workflow rendering, existing nullability and all old API keys stay exact. Empty DAG GUIDs and default identities fail closed; DAG InstanceId access throws InvalidOperationException. Record equality/hash separate aggregate families even at the same GUID.

Only OrcaCore.api.txt changes: exact approved extraction a8bb583860fd8744b018273911fe65b1c899926d70e501efb773ccfbf5588f3e, from ef13f660beecb09c88c0d22b09abae7b69332158d8fcea4b7da69ad5e0a32678, seven added keys and zero removed/modified keys. All thirteen other assembly baselines are unchanged. The new guard verifies both actual extraction and exact old/new baseline digests, independently of the active freeze.

All three workflow providers validate append/tail identity BEFORE storage or connection access; SQL Server's state engine also defends its internal direct entry points. The shared test fake obeys the same contract. Four Core facts test identity behavior; two shared provider facts test append/tail rejection, unchanged workflow tail and successful same-GUID workflow commit after rejected appends. Certification's four extra executed cases arise from fake and InMemory inheritance; PostgreSQL and SQL Server each add two.

## Carry-forward from the transition review

- Registry: an additive source-pinned approvedDagRunTransitionEvidence record binds the actual checkpoint/tree/evidence/verdict. Actual Git queries require a single-parent DIRECT CHILD that ADDS the verdict; a unique final APPROVE and exact bytes are required. RC-3 approval and all prior rows/history are unchanged.
- Canonical: all fourteen whole spec files (not just owned requirement blocks) are content-pinned in openspec-provenance-checkpoint.json. The aggregate cee7cd376777d5cc99c42e1de04cd692c7cc8270c85deb9a976ed94b7395baa0 terminates in guard source. Synthetic drift/missing checks keep the comparator load-bearing. Canonical and delta specs are byte-unchanged in this source slice. Later refresh requires a separately reviewed mechanical delta sync, not routine metadata maintenance.
- Status: all sixteen guide/disposition blocks and their Task 7.3 rows are refreshed. The dated 00-stack-decisions block is preserved and a new 2026-10-07 current-status note appended. The guides name the actual approved checkpoint and explicitly limit this source target to typed identity.
- Preamble: a NEW dated provenance artifact corrects the recipe to the complete LF-normalized prefix before the first requirement, WITHOUT trimming. The approved predecessor is retained byte-exact in the now-fourteen-entry superseded catalog. No frozen packet or verdict is edited.
- Deferred: archive resolver real-date/undated-copy tightening remains archival work; lane telemetry, UnsafeAccessor/expression ban and shipped-Hosting behavior/evidence audit stay on the separately reviewed 2.3 slice.

The scope artifact is permanently source-pinned. Whole planning/document pins, source provenance for only the four changed packages, declaration accounting and maximal historical current-match pins are refreshed coherently. Accounting is 342 physical sources / 1,415 declarations and 194 active sources / 727 declarations: four Core facts, two shared certification facts, and three guard facts explain the +9 declarations and +2 files.

## Validation reproduced by the author

Debug and Release non-incremental warnaserror builds: zero warnings/errors. Pack: twelve exact current-source packages. Core 354, Ephemeral 79, Durable 99, Acceptance 37, Hosting 24, ProviderCertification 100; PostgreSQL 103, SQL Server 74, Integration 11, zero skipped. Infrastructure 247/247 (three new guards), exactly fourteen intentional expected-red, total 261. Focused identity guards 3/3 in Release and Debug. OpenSpec strict 21/21. Green compile/package fixtures pass; dag-hosting remains the sole expected-red package fixture. Worktree and committed-rehearsal diff checks must both be clean.

ReSharper InspectCode 2026.2.3.1, net10.0/Release, --no-build --no-updates, exact changed C# scope: 320 findings (206 warnings / 114 notes), zero errors. Findings are existing large-file/style notes plus approved-namespace/private-field suggestions and intentional exception-test calls, not compiler warnings. No cleanup or unrelated refactoring was performed. Diagnostic SARIF/logs live in ignored .codex-run and are not checkpoint content.

Author caveats: initial candidate capture exposed nullability-metadata changes; fixed BEFORE baseline update, yielding the exact approved hash. Initial certification exposed the fake tail-reader gap; fixed and rerun. An earlier guard pass used stale compiled documentation pins after a status-note edit; the final validation rebuilds both configurations. No failed attempt is represented as green.

## Eight isolated negative controls

All mutations run only in a guarded disposable clone, with main unmodified. Three-guard baseline is green; source mutations compile first. All eight controls are RED:

| Control | Intended durable detector |
|---|---|
| Edit the unowned first durable-runtime requirement | Canonical content mismatch naming the path |
| Coherently rehash that file row and aggregate in the fixture | Guard-source aggregate pin |
| Drop a canonical row | Complete ordered inventory / named missing path |
| Use activation as evidence, with fixture AND source pointer changed | Actual Git single-parent/direct-child check |
| Neutralize the inventory comparator | Its synthetic expected-failure checks |
| Permit an empty DAG GUID | Core exact exception contract |
| Change legacy workflow text | Core compatibility assertion |
| Remove InMemory workflow-tail family validation | Shared certification; fixed fake stays GREEN |

Restore was applied after every control. The final copy is rebuilt from frozen files, not reused mutated binaries. Full committed checkpoint and freeze-cleared evidence-state rehearsals are reported with their exact tree/counts in the final handoff. No activation is rehearsed or authorized: this partial slice has NO completion checkbox to activate.

## Four freeze anchors and exact recipes

1. Unchanged base HEAD/tree above and empty live index.
2. Manifest: exact git status --porcelain=v1 --untracked-files=all --no-renames, LF/final LF; 3003 B / 268d3728fddf4225e266af65fe3bd09d1aeaa93f5dd5cd14cd27a5239dc17a49. Entry set is cross-checked against git diff --name-only HEAD plus untracked, not status alone.
3. Semantic content record: omit docs/review/** and ONLY review-manifest-provenance.json and immutable-document-history.json; path<TAB>RAW file byte length<TAB>lowercase RAW SHA-256. Ordinal-sort complete rows, LF-join with one final LF, UTF-8 without BOM. 40 rows / 5289 B / cd5c653a61a4fc0c1a2ec17bfdc8cbed8dbc1529c31582490538b675c7c2dde3.
4. Active record: omit ONLY review-manifest-provenance.json; XY<TAB>path<TAB>RAW length<TAB>RAW SHA-256, same ordering/encoding. The fixture and final handoff state its bytes/hash. It includes this request, so its own digest and the self-inclusive staged tree are reported in the handoff, not embedded recursively here. Confirm both with a copied-index rehearsal and raw-file/blob comparisons.

Provenance: 184 rows / 48504 B / fd6ad0449e706d5ecf3f9dab552a9ad5fc3ef1be95df13304afba91e1d9ca8c8; 173 synchronized, eight superseded, three bootstrap, zero pending and semanticApprovalEligible=true (synchronization only). Whole canonical content: fourteen rows / 1635 B / cee7cd376777d5cc99c42e1de04cd692c7cc8270c85deb9a976ed94b7395baa0.

## Verdict and subsequent sequence

Write a dated immutable APPROVE or REJECT naming this exact bounded source freeze. Do not modify the request, manifest or older evidence. A verdict written into main is an extra untracked path, NOT part of this approved checkpoint.

Only after APPROVE: confirm zero drift, checkpoint exactly the manifest paths/tree, then make its sole direct-child evidence commit adding/cataloging the byte-exact verdict and clearing activeFreeze while retaining the history pointer. Do NOT check 2.1, 2.4, reshape 8.4/8.5 or any later task on this partial slice. There is no activation commit. The next source target is the remaining 2.1 run protocol/provider work, independently reviewed before its checkpoint.
