# Independent review: Task 1.1 RC-3 DAG run contract remediation

Date: 2026-10-05. Change: admit-durable-dag-run-progression.
Base/parent: dab498ff54321d802b36ce052100954bbe00487d. Exact 61-path uncommitted target, empty live index.
Manifest: [raw Git-order freeze](developer-facing-interface-section-08-task-8-4-dag-run-contract-rc-3-remediation-dirty-manifest-2026-10-05.txt). The accompanying
final handoff states the complete simulated tree AFTER this self-inclusive packet
is frozen; compare that tree/parent before staging. No main checkpoint exists.

## Scope and immutable rejected evidence

CONTRACT/provenance only. No src/**, canonical openspec/specs/**, compiled API
baseline, friend, test declaration, waiver, expected-red or disposition change.
1.1 remediation prepared; 1.2 OPEN but checkbox-unpinned, 1.3–3.2 and reshape
8.4/8.5 open. Four successors remain Proposed/PendingModification, semantic
approval false. No test result supplies contract approval or bridge source authority.

All THREE rejected packets are byte-exact and bound by rejectedFreezes ids
reshape-dag-run-contract-rc-1-rejected, -rc-2-rejected and -rc-3-rejected.
The latest verdict docs/review/developer-facing-interface-section-08-task-8-4-dag-run-contract-rc-2-refreeze-independent-review-verdict-2026-10-05.md: 24510 bytes / 3cbba061ca4f819ae310d997434de57b12efd2b7bf6c7ad1071712a19dc6ae6e.
Its 53 recorded rows reconstruct the 52-row historical active record exactly:
8311 bytes / 7980b02f023451ca63f437e0d69bf2782f94a3c8c69188d0aa90b2641d853a4e, rehearsed tree b81c0da8585f4c93fb1faa9e6a16768ee6a209b3.
It was never checkpointed in main. Earlier reconstructions remain in their original
immutable artifacts. Eleven rejected contract/API/signature/remediation artifacts
are permanently guard-source pinned; the unreviewed RC-2 pre-validation draft
request/manifest are retained as disclosed history, not a reviewed target.
Current superseded provenance catalog:12, including rejected RC-2 provenance unedited.

## Primary successor artifacts

- [Complete RC-3 contract](../../openspec/changes/admit-durable-dag-run-progression/artifacts/task-1-1-run-contract-rc-3-remediation-2026-10-05.md).
- [Exact compiler-extracted API additions](../../openspec/changes/admit-durable-dag-run-progression/artifacts/task-1-1-exact-api-additions-rc-3-remediation-2026-10-05.md).
- [Standalone signature proof](../../openspec/changes/admit-durable-dag-run-progression/artifacts/task-1-1-standalone-signatures-rc-3-remediation-2026-10-05.cs).
- [Dispositions and third rejected-freeze reconstruction](../../openspec/changes/admit-durable-dag-run-progression/artifacts/task-1-1-rc-3-review-remediation-2026-10-05.md).

## RC-3 decision and all three P3s

1. Distinct bound: internal DagEvaluationParallelismPerRun=1, one dedicated isolated
   worker lane/run/boot host, lightweight round-robin dispatch that never waits on a
   mapper. No host-global MaxConcurrentNodes/shared permit/ThreadPool user-code pool.
   A stuck cancelled mapper retains its OWN lane until return. Total physical workers
   scale with dispatched live/retired runs; infinite runs/resource exhaustion/process
   starvation have no universal progress guarantee. This serial-throughput/resource
   trade-off is explicit, not a new public option or hidden global admission rule.
2. Bounded speculation: compute F=max(0,current local child ceiling-admitted children/
   start intents). Evaluate only the lowest Ready authored ordinal in the first F
   candidates, none at F=0, zero look-ahead. One evaluation OR receipt and one coalesced
   admission request per run/host. A fan-out cannot create a receipt per Ready node.
3. Fenced authored commit: successful n needs F>0, lower Ready count<F AND count=0,
   a deliberately stronger serial tie-break. Otherwise CapacityDeferred has zero
   input/start effects and retains the receipt. New lower readiness invalidates a
   higher speculative receipt for later precommit replay, preventing single-host lane
   deadlock; time, unrelated versions and full capacity do not evict it. No overlapping
   replacements. Cross-host duplicates are explicitly one/run/boot host, not exactly once.
4. Exact non-success ChildJoined transition atomically blocks all reachable Pending/
   Ready descendants; success alone readies eligible dependants (CP-024/AC-611).
5. Unaddressed ready hints ack validated/coalesced bounded local WAKE acceptance before
   mapping, not admission success. No held/renewed claim across user code. Continuous
   authoritative nonterminal discovery wraps each full pass with <=5s between passes,
   recovering acked/lost/poisoned hints and host death. Terminal waits still subscribe/
   recheck; no finite outage-independent discovery bound or poison-created run failure.
6. Sixteen proposed doc blocks name RC-3 as primary and RC-1/RC-2 as rejected history.
   The live-mapping-owner scenario is retitled. Docs08 CP-025/027,12 AC-609/611/617,
   and17's distinct-limit taxonomy disposition scheduling/duplicates explicitly.
   Dated decisions are appended, not rewritten; Task7.3's22 rows/digest and map refresh.

DG-P22/23/24 require future REAL PostgreSQL/SQL Server authored ties on one/two hosts,
slow/stuck cross-run isolation/bounds and ready-wake/host-death recovery. These are
contract exits, NOT implemented runtime tests or proof supplied by this target.
RC-1's bridge-only Begin/Renew/End/Abandon, 5s lease/absolute15s/one append FIFO and
RC-2's out-of-turn decode/map/normalize/immutable receipts remain. All six round-96
operational notes remain resolved. The public proposal stays365 additive keys
(7 OrcaCore/193 Runtime.Protocol/165 Provider.Abstractions), zero removed keys;
only ignored isolated copies compile proposed signatures. No source/canonical sync.

## Exact record recipes and anchors

Raw manifest: exact stdout from git status --porcelain=v1 --untracked-files=all
--no-renames, LF with terminal LF. Verify NUL form plus git diff HEAD paths union
untracked paths, and copied-index staging: exact61 real paths, no phantom entries.

Semantic record: remove docs/review/** and ONLY the review-manifest and immutable-
history fixtures. Render path<TAB>RAW byte length<TAB>lowercase RAW SHA-256;
ordinal-sort FULL rows, LF-join with terminal LF, UTF-8 without BOM. 46
rows / 7003 bytes / efa5c8e2fb85761b3add51014d75c75576518ce9d8c349d05d9cb2c609231f97.

Active record: exclude ONLY review-manifest-provenance.json. Render
XY<TAB>path<TAB>RAW byte length<TAB>lowercase RAW SHA-256; same full-row ordinal
sort/encoding. The fixture pins this complete packet. The final handoff supplies
its exact value plus manifest/all-file values and the simulated tree/parent.
Every staged blob must equal its raw working bytes; staged/committed diff check0.

Provenance: 184 rows/48464 bytes/af9fdd1000063e9924fb1efd03d7cac349e1a72af9d9bf81f3b9a499abf60b66:
173 synchronized,4 superseded,4 PendingModification,3 bootstrap-only, false semantic
approval. Active inventory22/1808/2869d4ad4541be53adeac5819091605eff85c39b5140abec4438c668ac99b6e2. Canonical blocks,
preambles/order and every predecessor/other-change requirement remain unchanged.
All new SHALL paragraphs must parse as requirement text, never scenario text.

## Validation, controls and activation rehearsal

The final handoff reports completed results. Required: Debug/Release nonincremental
-warnaserror0/0; feed12; Core350,Ephemeral79,Durable99,Acceptance37,Hosting24,Cert96,
PostgreSQL101,SQLServer72,Integration11; Infrastructure243/243, exactly14 intentional
ExpectedRed; focused OpenSpec15/15 both configs; strict21/21; staged/committed diff0.
Both isolated signature proofs compile0/0, exact baseline extraction365/zero removals.
Verified disposable checkpoint rehearsal243/243; clear active freeze while preserving
history pointer243/243; then check ONLY1.2 without a guard edit243/243.

With activeFreeze cleared, targeted controls must fail on their durable pins:
restore a global mapper cap; share a run lane; weaken round-robin fairness or F=0;
allow look-ahead/multiple receipts; allow wall-clock admission or skip the fenced
lower-ordinal check; discard receipts for capacity/time; drop DG-P22/23/24; omit
failed-child closure; hold a ready claim through mapping or remove rediscovery;
tamper rejected RC-2 artifact/provenance/verdict; edit docs08/12/17; alter signatures/
API; flip semantic approval; close1.3 or reshape8.4 early. Control/restore GREEN.
These are DOCUMENT/GUARD enforcement controls, not future scheduler behavior tests.
All mutation scripts assert disposable root/base and never alter main/live index.

## Verdict and sequence

Create a NEW immutable developer-facing-interface-section-08-task-8-4-dag-run-contract-rc-3-remediation-independent-review-verdict-YYYY-MM-DD.md
with exactly ONE final **Verdict:** **APPROVE** or **REJECT** line. The later verdict
is NOT part of these61 paths. Existing requests/manifests/verdicts are immutable.
Only after independent APPROVE and zero drift: checkpoint exactly61 on stated
parent; its only child adds/catalogs verdict and clears freeze keeping the history
pointer; activation checks1.2 ONLY. Atomic1.3/1.4 review follows separately.
No checkpoint, source, canonical sync or archival is authorized by this request.
