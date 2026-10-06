# Independent review: complete durable DAG run contract, Task 1.1 / approval 1.2

Date: 2026-10-04. Change: admit-durable-dag-run-progression.
Base/parent: `dab498ff54321d802b36ce052100954bbe00487d`. This exact target is uncommitted; index empty.
Scope: the 35 paths in [raw dirty manifest](developer-facing-interface-section-08-task-8-4-dag-run-contract-dirty-manifest-2026-10-04.txt).

## Authorization boundary

Review one complete CONTRACT target, not source. Task 1.1 is prepared/checked;
1.2 is open and deliberately UNPINNED for its checkbox-only post-approval
activation. Tasks 1.3–3.2 and reshape 8.4/8.5 remain open. All four successor
rows stay Proposed; four pending modifications and semantic approval false
are intentional. No source, canonical spec, public baseline, friend, provider
schema or compiled test declaration changes land here. The documentary .cs
file is an isolated signature proof, not product source.

This approval may authorize the contract checkpoint only. Independent APPROVE,
zero-drift checkpoint, direct-child evidence and 1.2 checkbox activation precede
the separately reviewed atomic 1.3/1.4 canonical/registry transition. Source
and provider work follows that transition's approval/checkpoint. No archive.

## Process approval chain already landed

1. Checkpoint `b55056c0d9136f509296167ae5984881e7b5de97`, parent
   `095bbfd1a6b559f8f1be47fb8d4bcd4749d40854`, exact approved 14-path tree
   `5880c8595f7b4f34026ec90f287a17bd3d63e6c6`.
2. Its direct-child evidence `e99f338d9319b9af5459f5685c69585493773d7c`
   adds the process APPROVE verdict byte-exact: 12,342 LF bytes, SHA-256
   `f54a6ba50a1ea1ce0d18d2f904a388a1ab91145d7ea59f65da1fb89acc70370c`;
   catalogs it and clears activeFreeze without losing the history pointer.
3. Activation/base `dab498ff54321d802b36ce052100954bbe00487d` checks 0.2 ONLY.
   The activated base passes the Infrastructure lane (241 ordinary guards plus
   the two product-authoring compilation guards, run as sequential slices).

No push is claimed. The earlier child protocol remains an immutable six-call
contract, not a complete run lifecycle. Its results gain CapacityDeferred only
as an explicitly proposed, reviewed extension in this target.

## Full contract and signature evidence

- [Run contract](../../openspec/changes/admit-durable-dag-run-progression/artifacts/task-1-1-run-contract-2026-10-04.md): exact persistence, lifecycle, fencing, claims, identity, schema and certification.
- [Exact API additions](../../openspec/changes/admit-durable-dag-run-progression/artifacts/task-1-1-exact-api-additions-2026-10-04.md): existing before/after signatures and compiler-extracted baseline additions, keyed by declaring type.
- [Standalone signatures](../../openspec/changes/admit-durable-dag-run-progression/artifacts/task-1-1-standalone-signatures-2026-10-04.cs): complete advanced/internal blocks that compile using only shipped OrcaCore.

The isolated API proof copies the three current declaring packages, adds the
proposed types/signatures OUTSIDE main source and builds them with the repository
SDK pin. Existing baseline declarations are compared by owning type + exact
formatted signature; none is removed. Existing WorkflowEventCodec's 21 entries
are byte-for-byte unchanged and enumerated. The new DagRunRecordCodecV1 is a
separate fixed protocol codec. Main src/** and all API baselines remain untouched.
Both the three-owner proof and standalone proof compile with zero warnings/errors.

The complete proposed seam includes:

- WorkflowStreamId's distinct DAG kind and identity, retaining every workflow
  signature/rendering; an explicit ProviderCommitBatch DAG branch, never a
  fabricated workflow InstanceId; real IWorkflowEventStore.AppendAsync ownership.
- Immutable run input/graph creation and exact caller-key reopening, checkpoint
  plus tail, operation idempotence, owner lease/fence/version, startup discovery,
  admission, mapping failure/transitive blocking, authoritative joins,
  cancellation, integrity/poison reconciliation and subscribe/recheck waits.
- Closed creation/mutation/cancellation outcomes, especially CapacityDeferred
  versus AdmissionStopped. A current fenced owner's per-run host ceiling retains
  StartAuthorized and parked child slots on takeover; no creation-time authority.
- Eager declared-type decoding of EVERY successful resultful dependency,
  including unused outputs, to DAG_INPUT_MAPPING_INVALID on ordinary failure;
  typed present-null fixed bytes, codec/exception ownership on the durable side.
- Approved child GUID recipe hashes run GUID and ordinal ONLY. Workflow/version/
  fingerprint/declared types/bytes are a separate exact collision binding.
  DAG-run caller keys cannot pre-claim deterministic children or workflow keys.
- Four reserved DAG outbox kinds. Child start/join/cancel pumps run on EVERY
  durable host, even without a dispatcher or DAG role; ready claims require
  the DAG role and the exact null-or-local boot recipient partition. Internal
  IMessageDispatcher is the PR-015 dispatch port, not an external transport;
  it excludes continue, workflow-event and all DAG kinds. Wrong-lane release or
  poison does not acknowledge work or free uncertain admission.
- Exact version-1 discriminator/property allowlists and checkpoint framing;
  five complete first-create tables, shared-outbox columns, provider-native
  keys/types/constraints/indexes, no ALTER compatibility upgrade. DG-P01–13
  names real PostgreSQL/SQL Server certification, not mock-only evidence.
- One-way ready-sink/callback direction: Durable.Hosting owns the internal port,
  Dag.Hosting supplies mapping. No tenth friend, no reverse edge and no codec
  widening. Shipped-Hosting behavior and reflection/UnsafeAccessor/expression
  bans are explicit source/certification exits, not claimed implemented here.

Public application authoring and fixed-codec semantics are unchanged. Internal
StartAuthorized projects to public Running; IntegrityFault is not a fake terminal
result. Task 8.6 must freeze that projection. The full schema/key constraints and
run-input shape binding in the contract are proposed additions, not current
provider behavior.

## Exactly four owners; normative statements, not scenario tails

| Capability | Requirement |
|---|---|
| durable-persistence-and-outbox | Workflow store persists first-class durable records |
| durable-persistence-and-outbox | Outbox delivery is adapter-driven and at-least-once |
| durable-persistence-and-outbox | First-release provider ownership and schemas are exact |
| durable-runtime | Durable DAG progression is runtime owned |

Each revised delta preserves canonical statement and all canonical scenarios
verbatim, inserts proposed SHALL text before the FIRST scenario, and appends
one explicitly proposed successor scenario. The process-only proposal text is
replaced, not silently treated as canonical. openspec show --json --deltas-only
places every new obligation in requirement.text, not the last scenario. No fifth
owner is introduced; any uncovered fifth owner requires its own reviewed process
extension before contract/source work.

The four hashes are coherently re-pinned in guard source and the registry.
Measured provenance: 184 rows / 48,464 bytes /
`bb7a3394ffcd4a325c81c5d2ff0e3636996a30e2edbede23278efc8cd7446470`; 173 Synchronized, 4 Superseded,
4 PendingModification, 3 bootstrap-only; semanticApprovalEligible false.
Active capability directories: 22 / 1808 bytes /
`2869d4ad4541be53adeac5819091605eff85c39b5140abec4438c668ac99b6e2`. Canonical inventory,
preambles and requirement bytes remain unchanged.

## Document reconciliation and permanent history

The contract's disposition names docs 03/04/05/06/08/10/12/16/17 and all binding,
guide/root restatements. DR-037/PR-015, CP-022/025 and §17.2.6 are explicitly
covered. Doc 11 is excluded with its NF-002/package-boundary reason; doc 17's
immutable C# companion has no application signature change and is excluded.
Each amended doc has ONE source-pinned proposed/pending section; old normative
passages and dated decisions are not rewritten. Decision 7 is retained and a
dated proposed decision appended. Reshape changes only OPEN 8.4/8.5 wording plus
the dated proposal note; the 8.0 map updates those ownership rows.

All 22 Task 7.3 rows and artifact digest, the map digest, and exact maximal
current-match projections are refreshed. Six whole planning files and 16
document proposal blocks have durable guard-source hashes, not just a temporary
freeze. Only task 1.2's checkbox is normalized out of the tasks hash; its text,
uniqueness, every other task and status stays protected.

New current provenance is the contract-openspec-provenance artifact. The 0.1
process artifact is UNEDITED in the permanent superseded catalog, now nine
entries. Both earlier child contract artifacts, all old packets/verdicts and
all canonical files remain byte-exact. No Fact/Theory is added; the declaration
crosswalk correctly does not move.

One narrow prerequisite fix is disclosed: RequireApprovalEvidence's task-token
boundary now accepts punctuation such as 'task 0.1.' while still rejecting
'task 0.11' or 'task 0.1.1'. Three in-process checks prove those boundaries.
The immutable process verdict was NOT rewritten to satisfy the old regex.

## Exact records and reproduction recipes

- Raw porcelain/manifest: 35 rows, 2471 bytes,
  SHA-256 `fa3604972b2dca65a55bc753841323af9171a6438a0011be26b64ca330658a02`. Exact Git-emitted tracked THEN
  untracked order; UTF-8/LF and one terminal LF. Compare raw and NUL porcelain,
  diff-plus-untracked set, and a copied-index staged set (no cache phantoms).
- Semantic record: 31 rows, 4179 bytes,
  SHA-256 `26b22536e51c4974d21432c028ebf4d81b77a8961ba34883ccd5a0af57fbe2da`. From manifest paths omit
  docs/review/** and the history/review fixtures; read RAW bytes, render
  path<TAB>byte-count<TAB>sha256, ORDINAL-sort FULL rows, LF-join + terminal LF.
  This is not raw-manifest order and does not normalize semantic file bytes.
- Active record: every manifest path except review-manifest-provenance.json
  (self-reference). Render XY<TAB>path<TAB>raw-byte-count<TAB>raw-sha256,
  ordinal-sort FULL rows, UTF-8/LF + terminal LF. Request, manifest and immutable
  history ARE included. Its exact final bytes/hash are in activeFreeze.
- The exact simulated tree is supplied in the final handoff AFTER this
  self-inclusive request is frozen. Embedding its own tree would be a hash
  self-reference. A copied index stages exactly the manifest paths, compares
  every blob to raw bytes, checks the committed diff and writes the tree.

## Validation and isolated controls

Final Debug and Release non-incremental -warnaserror builds: zero warnings/errors.
Core 350, Ephemeral 79, Durable 99, Acceptance 37, Hosting 24, Certification 96,
PostgreSQL 101, SQL Server 72, Integration 11 all pass. Infrastructure 243/243;
focused OpenSpec guards 15/15 in Debug; ExpectedRed exactly 14 failures, no passes
(257 guards accounted for). Strict OpenSpec 21/21. The final handoff also reports
the frozen/committed and cleared-freeze 1.2 activation rehearsals; do not infer
them merely from this dirty-state validation. Standalone/API proofs both 0/0.

Eleven negative controls on the owned, byte-verified disposable worktree are RED:

| Control | Intended first detector |
|---|---|
| C1 append proposal text | whole proposal pin |
| C2 weaken withdrawal in design | whole design pin |
| C3 check 1.3 early | tasks whole decision pin |
| C4 CapacityDeferred becomes AdmissionStopped | whole contract pin |
| C5 append API factory claim | exact API catalog pin |
| C6 rename standalone fence signature | standalone signature pin |
| C7 doc 08 proposed status becomes approved | document section pin |
| C8 doc 17 proposed status becomes approved | document section pin |
| C9 put workflow identity in child hash | reviewed proposed block pin |
| C10 edit the old 0.1 provenance | permanent superseded-artifact pin |
| C11 claim semantic eligibility | independent provenance semantic check |

P1 checking 1.2 alone is GREEN; baseline and final restored controls are GREEN.
Mutations did not rely on the active freeze, which was null during these tests.
The full cleared-freeze activation rehearsal is supplied with the final handoff.
No probe reset/committed/edited main. The runner paused during restoration; exact
copy comparisons identified the remaining mutation, it was independently tested,
then restored. One pre-final Debug run used the older malformed task regex and
failed; final rebuilding and 15/15 rerun confirm the correction. No result is
represented as a never-failed first attempt.

## Reviewer instructions / checkpoint order

Independently judge the COMPLETE seam: API compatibility, lifecycle feasibility,
atomic fence/version/claim partitions, exact schema/certification, all four
normative owners, document disposition, immutable history and activation.
Do not treat green tests as evidence of an implemented bridge: this is planning.
Please run destructive/mutation tests only in a verified disposable copy.

Write a NEW immutable verdict with stem
developer-facing-interface-section-08-task-8-4-dag-run-contract-independent-review-verdict-YYYY-MM-DD.md,
one final **Verdict:** **APPROVE** or **REJECT** line. It is later evidence, not
part of this 35-path target. Do not edit any frozen packet/old record/main index.

Only after independent APPROVE and zero drift: checkpoint these 35 paths at
the handoff tree with this base parent. Its single-parent direct-child evidence
commit adds/catalogs the byte-exact verdict and clears activeFreeze while keeping
the history pointer. Activation checks 1.2 ONLY. Then prepare atomic 1.3/1.4;
no source, friend, canonical hand edit, reshape completion or archival now.
