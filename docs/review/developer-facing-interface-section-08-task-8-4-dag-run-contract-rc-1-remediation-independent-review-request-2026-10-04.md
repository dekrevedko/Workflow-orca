# Independent review: Task 1.1 RC-1 durable DAG run contract remediation

Date: 2026-10-04. Change: admit-durable-dag-run-progression.
Base/parent: `dab498ff54321d802b36ce052100954bbe00487d`. Uncommitted exact 43-path target; live index empty.
Manifest: [raw Git-order freeze](developer-facing-interface-section-08-task-8-4-dag-run-contract-rc-1-remediation-dirty-manifest-2026-10-04.txt).

## Authorization and immutable rejected history

Review CONTRACT only, not source/canonical sync/archival. Task 1.1 is prepared;
1.2 remains open and UNPINNED for checkbox-only activation. 1.3–3.2 and reshape
8.4/8.5 stay open. Four Proposed successor rows remain pending; semantic approval
is false. No src/**, canonical openspec/specs/**, public baseline, friend or test
declaration changes. All current compiled behavior and expected-red dispositions
are unchanged. A standalone documentary .cs file is not product source.

The previous 35-path request/manifest/verdict are byte-exact, cataloged and bound
by rejectedFreezes id reshape-dag-run-contract-rc-1-rejected. REJECT verdict:
15,033 bytes / 5f2d075197e5390648d8b65fc4b2c0b3f8543f1dca06739eb4bf1fb137231229; see the complete actual hash in the remediation
record and registry. The old run contract/API/signature artifacts are retained
and permanently pinned in guard source. No approval is inferred from validation.
Process chain remains b55056c -> e99f338 -> dab498f; the evidence adds the process
APPROVE byte-exact, and activation changes only task 0.2. This rejected contract
has never been checkpointed. Its old rehearsed tree is historical-only evidence.

## The proposed fix, including every P3 note

Read [complete replacement contract](../../openspec/changes/admit-durable-dag-run-progression/artifacts/task-1-1-run-contract-rc-1-remediation-2026-10-04.md),
[exact API additions](../../openspec/changes/admit-durable-dag-run-progression/artifacts/task-1-1-exact-api-additions-rc-1-remediation-2026-10-04.md),
[standalone signatures](../../openspec/changes/admit-durable-dag-run-progression/artifacts/task-1-1-standalone-signatures-rc-1-remediation-2026-10-04.cs),
and [review disposition / rejected-row reconstruction](../../openspec/changes/admit-durable-dag-run-progression/artifacts/task-1-1-rc-1-review-remediation-2026-10-04.md).

- RC-1: Dag.Hosting owns mapping/admission coordination. Durable.Hosting owns lease
  broker/provider calls and role-independent join/cancel coordination. Exact bridge
  Begin/Renew/End/Abandon and captured-turn child adapter methods forbid provider bypass.
- Provider FIFO is persistent and fair, with one live request per owner/run, stable
  sequence, expiring waiting heartbeat and retained consumed request receipts. One
  grant permits one append. Lease <=5s, absolute turn deadline15s; renewal cannot
  extend it. Release before idle/capacity/child waits; independent watchdog discards
  late mapper results, without pretending it can terminate arbitrary user code.
- New admissions cannot overtake earlier remote joins/cancellation. The healthy-
  service bound includes K earlier turns and dead-waiter expiry. DG-P14/15/16 require
  real-storage controlled-time liveness/idle/captured-fence/takeover tests. Different
  local host ceilings may apply in successive admission turns, never concurrently.
- P3-1: behavioral scenarios replace the repeated process scenario in all four
  proposed blocks. Every canonical statement/scenario stays byte-exact; new SHALL
  text is above the first scenario. No fifth owner or hand-synced canonical content.
- P3-2: exact observer subscription owner column, <=30s subscription expiry and
  <=10s sweep define dead-host notice cleanup after active claim expiry. Progression,
  child/poison/admission truth is never purged. Observer maintenance is separately
  accounted, not business messaging pressure (DG-P17).
- P3-3: created-only snapshot plus compact changed-node/value-write frames, constant-
  size operation receipts and one overwritten current full checkpoint bound retained
  space. Full-checkpoint WRITE amplification is explicitly disclosed (DG-P18).

Six first-create provider tables, explicit lease/cancel-waiter/observer maintenance
ports, exact baseline lines and closed outcomes are proposed, not implemented.
The isolated API proof retains every existing key and extracts 350 additive keys;
the standalone signatures compile with only shipped OrcaCore, using repo SDK pin.
WorkflowStreamId compatibility, the 21 WorkflowEventCodec entries, fixed business
codec, null/exception rules, child GUID recipe (run GUID+ordinal ONLY, separate
binding check), role partitions and one-way package edges remain as reviewed.
No tenth friend, codec widening or public child API is proposed.

All sixteen explicitly proposed document blocks are clarified and pinned; dated
decisions retain their old wording and append the clarification. The 22 Task 7.3
rows/digest and Task 8.0 map pin refresh in this same target. Doc 11 NF-002 and the
doc17 C# authoring companion are explicitly excluded. Six current planning files
plus the remediation record are whole-file source-pinned; the earlier three
contract artifacts are separately permanent. The superseded provenance catalog
is now ten entries, keeping the rejected current artifact unchanged.

## Reproducible freeze recipes

Raw manifest is EXACT git status --porcelain=v1 --untracked-files=all --no-renames
stdout (LF, terminal LF). Check NUL form plus git diff --name-only HEAD and untracked
union; copied-index staging must have the identical real path set, no phantoms.

Semantic record: manifest paths minus docs/review/** and the two self/immutable-history
fixtures. Render path<TAB>RAW byte length<TAB>lowercase RAW SHA-256, ordinal-sort
FULL ROWS, LF-join with one terminal LF, UTF-8 without BOM. 36 rows /
5120 bytes / `994198bc70d52d6329cbe27e90bb9803a844c31cc6233dc2a7512e82b31d1b50`.

Active record: exclude ONLY review-manifest-provenance.json. Render
XY<TAB>path<TAB>RAW byte length<TAB>RAW SHA-256; ordinal-sort whole rows and use the
same LF encoding. The activeFreeze pins its bytes/hash and the complete self-inclusive
request/manifest. Do not substitute path-only sort or normalized bytes for these RAW
records. The exact simulated tree/parent and all-file anchors are in the final handoff;
the tree cannot appear in its own request without a self-reference. Verify every
staged blob equals raw file bytes and committed diff --check including new files.

Provenance independently recomputed: 184 rows / 48464 bytes /
`cde64d8dabf2f67a4ea51bbe0aabafd52271177a34705a1d67b187a95ce9eb6d`; 173 synchronized,4 superseded,4 PendingModification,3
bootstrap-only; semanticApprovalEligible=false. Directory record 22
/1808 bytes / `2869d4ad4541be53adeac5819091605eff85c39b5140abec4438c668ac99b6e2`.
Canonical inventory/preambles unchanged. The old rejected artifact is unedited.

## Validation and controls to reproduce

The final handoff reports the completed run, not implementation of this proposed seam.
Required lanes: Debug/Release --no-incremental -warnaserror 0/0; exact package feed;
Core350/Ephemeral79/Durable99/Acceptance37/Hosting24/Certification96; PostgreSQL101/
SQLServer72/Integration11; Infrastructure243/243; exactly14 ExpectedRed; focused
OpenSpec guards15/15; strict OpenSpec21/21; diff --check on worktree AND staged copy.
Rehearse checkpoint in a verified short-path disposable copy, then clear activeFreeze
but KEEP history pointer, check ONLY1.2 without any guard edit, and run243/243.

Negative controls with activeFreeze cleared: weaken FIFO/deadline/idle-release or
remote-cancellation contract text; delete BeginTurn in standalone; alter API catalog;
replace a behavioral scenario by process-only text; alter observer cleanup/compact
events; edit a retained rejected artifact/verdict; flip semantic eligibility; close
1.3 early; falsely call a proposed doc approved. Each must fail its intended durable
pin/registry check. 1.2-only activation is positive, not an early-close negative.

Interim failures are disclosed: the unadmitted verdict/null history pointer made
the pre-packet history guard red; the final packet resolves admission. An overlapping
build/test run locked DLLs; final validation runs sequentially. The isolated API proof
initially omitted DagRunSubscriptionId; it was fixed and recaptured before this freeze.
Neither a failed interim run nor prototype compilation is counted as runtime evidence.

## Verdict and checkpoint sequence

Write a NEW immutable verdict named
developer-facing-interface-section-08-task-8-4-dag-run-contract-rc-1-remediation-independent-review-verdict-YYYY-MM-DD.md,
one final **Verdict:** **APPROVE** or **REJECT**. Use disposable copies for mutations;
do not edit main, any frozen packet, or the live index. This is a later evidence
file, NOT part of the 43-path target. Approval must bind the exact handoff tree.

After independent APPROVE and zero drift ONLY: checkpoint these43 paths on the
stated parent; its only direct-child evidence adds/catalogs the byte-exact verdict,
clears activeFreeze, preserves history pointer. Activation checks1.2 ONLY. Then
the atomic1.3/1.4 target gets a separate review/checkpoint. No source/archival now.
