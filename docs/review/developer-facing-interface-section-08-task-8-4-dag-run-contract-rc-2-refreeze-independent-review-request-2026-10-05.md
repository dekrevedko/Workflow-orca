# Independent review: Task 1.1 RC-2 DAG run contract remediation

Date: 2026-10-05. Change: admit-durable-dag-run-progression.
Base/parent: dab498ff54321d802b36ce052100954bbe00487d. Exact 53-path uncommitted target; live index empty.
Manifest: [raw Git-order freeze](developer-facing-interface-section-08-task-8-4-dag-run-contract-rc-2-refreeze-dirty-manifest-2026-10-05.txt).

## Scope and preserved rejections

Review CONTRACT/provenance only. No src/**, canonical openspec/specs/**, compiled
API baseline, friend, test declaration, waiver or disposition changes. Task 1.1
remediation is prepared; 1.2 stays OPEN but checkbox-unpinned; 1.3–3.2 and reshape
8.4/8.5 remain open. Four successor rows remain Proposed/PendingModification;
semanticApprovalEligible=false. No validation result is semantic/source approval.

The original rejected 35-path packet and the second 43-path RC-1 packet remain
byte-exact, cataloged and bound by rejectedFreezes ids reshape-dag-run-contract-rc-1-rejected
and reshape-dag-run-contract-rc-2-rejected. Latest REJECT docs/review/developer-facing-interface-section-08-task-8-4-dag-run-contract-rc-1-remediation-independent-review-verdict-2026-10-04.md is 21654 bytes /
0ef31625a268066ba81d8f849447e15099b93b70a3080c0b8fda4dd087169415. Both reconstruction tables reproduce their historical active
records; the original six contract/API/signature artifacts and RC-1 remediation
record are permanent source-pinned (seven files). Main never checkpointed either
rejected contract. Process chain b55056c -> e99f338 -> dab498f is unchanged.

## RC-2 decision and all six notes

Primary artifacts under openspec/changes/admit-durable-dag-run-progression/artifacts/:

- [Complete contract](../../openspec/changes/admit-durable-dag-run-progression/artifacts/task-1-1-run-contract-rc-2-remediation-2026-10-05.md).
- [Exact API additions](../../openspec/changes/admit-durable-dag-run-progression/artifacts/task-1-1-exact-api-additions-rc-2-remediation-2026-10-05.md).
- [Standalone signature proof](../../openspec/changes/admit-durable-dag-run-progression/artifacts/task-1-1-standalone-signatures-rc-2-remediation-2026-10-05.cs).
- [Dispositions and historical reconstruction](../../openspec/changes/admit-durable-dag-run-progression/artifacts/task-1-1-rc-2-review-remediation-2026-10-05.md).

RC-2: eager output decode, mapping and normalization run OUTSIDE ownership and
both turn queues. One physical evaluation per node/host; global host worker cap
MaxConcurrentNodes; cancelled/token-ignoring workers retain slots until return,
never respawn on a timeout. Completed immutable receipts survive unrelated version
conflict/capacity deferral; a fresh short turn only rechecks binding/readiness/
barrier/capacity/version and appends exact prepared bytes. No mapper time budget
or expiry-failure is introduced. DG-P19 requires a >15s controlled-time valid mapper
to commit exactly once while joins/cancel progress, with no accumulating workers.

The RC-1 lease design stays: bridge-only Begin/Renew/End/Abandon, persistent FIFO,
one append, lease <=5s, absolute15s hard deadline, release before any wait; no
indefinite renewal, direct provider seam, reverse callback or tenth friend.

- Pressure: fifteen exact advanced WorkflowOperationalPressure additions and named
  BCL gauges classify child/unaddressed ready internally, addressed observer notices
  operationally and legacy external records separately. DAG stream/checkpoint/stuck
  pressure uses the existing operational store/scrape API (DG-P20).
- Clock: production DB UTC inside locked transactions; internal instance-scoped
  owning-provider test-clock override for real-storage virtual-time barriers. No
  public clock hook/new friend or changed workflow time semantics.
- Same-host arbitration: shared arrival FIFO, stable local/provider sequences,
  explicit OwnerBusy and LocalQueueFull (1024 limit), retry/cancel behavior and
  combined predecessor bounds. No hidden cancellation/admission priority (DG-P21).
- Limit: OpenChildBridgeForTurn(turn, prepared, maxConcurrentNodes) captures ONE
  explicit local validated value; direct prepared admission uses the same value.
- Cancellation: reachable public DagRunHandle trigger on another DAG-role host;
  role-free durable hosts process committed child cancellation/join, not initiate
  a nonexistent public cancellation API.
- States: remove persisted Running; exact eight-kind transitions use StartAuthorized
  as public Running and logical admission StartedAt; public enums unchanged.

Behavioral scenarios, observer cleanup and compact events from RC-1 remain. Six
first-create tables and both production providers remain proposed only. The isolated
API extraction has 365 additions (7 OrcaCore +193 Runtime.Protocol +165 Provider.Abstractions),
zero removed committed keys; main baselines are unchanged. Both signature proofs
compile, not runtime behavior. Four normative heading identities only; all new
SHALL obligations parse before scenarios. Canonical blocks/preambles/order stay exact.
Sixteen doc blocks now make RC-2 primary; dated entries are appended, not rewritten.
Task 7.3's 22 rows/digest and Task 8.0 map pin refresh in this same target; doc11 and
the authoring C# companion stay explicitly excluded. Superseded provenance catalog11.

## Exact recipes and anchors

Raw manifest is EXACT stdout from git status --porcelain=v1 --untracked-files=all
--no-renames, LF+terminal LF. Check NUL form and real git diff HEAD+untracked union;
copied-index staging must have that exact 53-path set and no phantom entries.

Semantic record: paths minus docs/review/** and the two review/immutable-history
fixtures. Render path<TAB>RAW byte length<TAB>lowercase RAW SHA-256; ordinal-sort
FULL rows; LF-join+terminal LF; UTF-8 no BOM. 41 rows /
6061 bytes / 3b96243953621d7f29a4578c4105cde39fa12491de864c3f8a577a0f3ceba25c.

Active record: exclude ONLY review-manifest-provenance.json; render
XY<TAB>path<TAB>RAW bytes<TAB>RAW SHA; ordinal-sort FULL rows, same LF encoding.
The fixture pins the complete self-inclusive packet. The final handoff carries
that anchor and exact staged tree/parent (tree cannot name itself in this request).
Require every blob equal RAW file bytes and staged/committed diff --check clean.

Provenance: 184 rows /48464 bytes /57620af0af3fe48e53c4fbfae6b3d5ba5a1518af57fcd2cfdba343197c939cc4;
173 synchronized,4 superseded,4 PendingModification,3 bootstrap-only, semantic
approval false. Active inventory22/1808/2869d4ad4541be53adeac5819091605eff85c39b5140abec4438c668ac99b6e2.

## Validation and durable controls

Final handoff states completed results, not tests of unimplemented runtime liveness.
Required Debug/Release nonincremental -warnaserror 0/0; package feed12; Core350,
Ephemeral79,Durable99,Acceptance37,Hosting24,Certification96,PostgreSQL101,
SQLServer72,Integration11; Infrastructure243/243; exactly14 ExpectedRed;
focused OpenSpec15/15 both configs; strict21/21; staged/committed diff check0.
Compile isolated API and standalone proof (only shipped OrcaCore reference).
Rehearse exact checkpoint in verified disposable copy:243/243; clear activeFreeze
but keep history pointer:243/243; check ONLY1.2 with no guard edit:243/243.

Run controls with activeFreeze cleared, verify their named durable diagnostics:
move evaluation inside lease; allow overlapping workers; discard completed receipts;
delete slow-mapper certification; weaken pressure/clock/local FIFO/limit/cancel/state
decision; edit either rejection or a retained rejected artifact; flip semantic flag;
close1.3; falsify proposed status; tamper standalone/API. Control and restore GREEN.
Do not mutate main or live index. Runtime certification rows remain future exits.

## Verdict and sequence

Write NEW immutable developer-facing-interface-section-08-task-8-4-dag-run-contract-rc-2-refreeze-independent-review-verdict-YYYY-MM-DD.md
with ONE final **Verdict:** **APPROVE** or **REJECT**. That later verdict is not one
of these53 paths. Use isolated copies for every mutation and assert disposable root.
The retained rc-2-remediation request/manifest were an unreviewed pre-validation
draft: the focused gate found stale source/fixture successor hashes. This refreeze
corrects them using the authoritative line parser. No reviewer approved the draft,
no contract bytes were changed by this repair, and both draft files remain byte-exact.

After APPROVE+zero drift ONLY, checkpoint53 on stated parent; its only direct child
adds/catalogs verdict, clears freeze keeping history pointer; activation checks1.2
ONLY. The atomic1.3/1.4 review follows separately. No source/archival/checkpoint now.
