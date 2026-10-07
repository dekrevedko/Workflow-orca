# Independent review: atomic DAG run canonical/registry transition 1.3/1.4

Date: 2026-10-05. Change: admit-durable-dag-run-progression.
Base/parent: d59f8307977dc71d1b77444b9711a0468c65f93d. Base tree: 3d834a59b0b20d6bcd02f065c198ee8ae545fde7.
Exact 36-path uncommitted target, empty live index. Manifest:
[raw Git-order freeze](developer-facing-interface-section-08-task-8-4-dag-run-atomic-canonical-transition-dirty-manifest-2026-10-05.txt).

## Bounded authority and prior approval

Review ONE canonical/registry transition, not a reopening of the approved RC-3
contract. Checkpoint e6802a348b0180d5eaad3be99b32e3204c5c25a1 / tree
d367ced646461fb78c8c85dea5bf1fb142e640f7, evidence
578593efd3709a5e9a14582d572eb4275324de3e and 1.2-only activation d59f8307977dc71d1b77444b9711a0468c65f93d
are the landed chain. The [RC-3 APPROVE](developer-facing-interface-section-08-task-8-4-dag-run-contract-rc-3-remediation-independent-review-verdict-2026-10-05.md)
is byte-exact: 17,803 B / b7c9d54dcfdbcc8fd3ec2ef99c43fb313e9b400a038b6da7f13b969b2feead4c.
This request asks for a SECOND, transition-specific verdict citing that approval
and this packet/addendum. No source, friend, API-baseline or disposition changes.

The RC-3 contract f8a1a7f6d69dc337854dd7e6dc9bbe59a2e7c3a43329635cf1b57c9f8280d665,
catalog 766ad5d84c90a0666a8a631018fdd686f9a59b00d6c8d50f3378b1b4fa6a308f,
signatures 8301094ba0a14634f9c8ba08ac2642eb2b8e5128981da453ecd0623762926189
and all older approved/rejected artifacts and dated records remain unchanged.
The [new addendum](../../openspec/changes/admit-durable-dag-run-progression/artifacts/task-1-3-1-4-rc-3-transition-addendum-2026-10-05.md)
is separately registry/source-pinned alongside the original packet and verdict.

## Mechanical sync proof

Apply the full approved deltas to canonical at d59f830: exactly three blocks in
durable-persistence-and-outbox and one in durable-runtime. All preambles/Purpose,
heading order, unrelated blocks and all other canonical files remain byte-exact.
Modified headings match canonical literally. Verify:

    git diff e6802a3 -- openspec/changes/admit-durable-dag-run-progression/specs

Exactly ONE word differs from the approved delta: in the runtime block's
"A newly Ready lower ordinal MAY invalidate one higher speculative receipt",
MAY -> SHALL. RC-3 section 4.2/DG-P22 and its P3-1 explicitly allowed this at sync.
The original approved hash stays in Proposed history; the new one is ApprovedPending.

| Capability | Exact heading | Predecessor hash | RC-3-approved delta hash | New canonical/delta hash |
|---|---|---|---|---|
| durable-persistence-and-outbox | Workflow store persists first-class durable records | 75cf33d00e6129e0ae702d04a37e0438358158adb76101bf9896dcc8144b6020 | 7d0fd8f4345f412fc3b76b82e5bbbe76dc420b2d43fcff99bc927f12fb7e9660 | 7d0fd8f4345f412fc3b76b82e5bbbe76dc420b2d43fcff99bc927f12fb7e9660 |
| durable-persistence-and-outbox | Outbox delivery is adapter-driven and at-least-once | 777a930986599b2931d71b6b3665e15fadf5869d4df748b954bf6f252670551d | 2aa07f46a0a53766db5aaf3adc743506e615f542d2bf6e9c033fbaa0db34f926 | 2aa07f46a0a53766db5aaf3adc743506e615f542d2bf6e9c033fbaa0db34f926 |
| durable-persistence-and-outbox | First-release provider ownership and schemas are exact | 1b539ab44adefc0cd020131c389ff420b6c31b2973cd123bd7e59ed436e852cb | 30ffcff973c7aac408396c002180b75d995b60493163b8f62ba0455df73260db | 30ffcff973c7aac408396c002180b75d995b60493163b8f62ba0455df73260db |
| durable-runtime | Durable DAG progression is runtime owned | 11741edaf2f26fd43846948d5395a8fe5be92ce61c016e3dc232d11dc810cd40 | fe738cf91b0a4048a9594deac8650a86c70d0bd9b35d2394d9be8b217e61dac8 | 81fe1db4624403098328534bb4903b4bf8df5c99d382783ce567077414a83904 |

## Registry, crosswalk and state

Schema 11 retains all four Proposed history rows and all unrelated registry rows.
It adds exactly four ApprovedPending rows plus FOUR exact reshape predecessor
supersessions. No generic drift/duplicate-owner waiver. Real RC-3 checkpoint/tree,
single-parent evidence that ADDS the unique terminal APPROVE and addendum hashes
are pinned in guard source. One active or ISO-dated archived owner resolves;
zero/duplicates/undated archives fail closed. Actual archival and withdrawal are
separate reviewed registry/provenance targets, not deletion shortcuts.

1.3 IS CHECKED inside this target. 1.2 is complete, bound to real approval; 1.4 stays
open for independent review/checkpoint, with ONLY its later checkbox activation
exempt. Source 2.1–3.2 and reshape 8.4/8.5 stay open.

Crosswalk primary owners 06/10/16 are amended; reverse audit reconciles 03/04/05/
08/12/17 and the binding/root guide blocks as approved, source pending. Doc 11 is
explicitly excluded: NF obligations and package/nonfunctional limits are unchanged.
The public-authoring companion is excluded: no public application signature change.
Current nine friends/codec edges stay unchanged. No exactly-once mapper invocation.
Existing dated decisions are retained; a new dated status note is appended.

## Four RC-3 observations

- P3-1: mandatory discard above, with old/new block hashes separately bound.
- P3-3: the addendum permits checkpoint-read skipping ONLY at unchanged version
  AND matching local evaluation/result for lowest Ready OR F=0. Version alone is
  insufficient; unchanged-version orphan wakes reload. Lost/completed local work,
  changed limits/version and invalidation reconsider. DG-P24 explicitly tests it.
  Doc16's Startup-only line is corrected in the addendum and active guide block;
  O(run heads) discovery plus non-skippable checkpoint reads is disclosed.
- P3-2: active/retired lane telemetry, cancelled stuck mappers, idle worker reuse
  and lifecycle accounting are named 2.3 source-review obligations. NO new gauge
  API/catalog/signature member is introduced; 365 keys stay 7/193/165.
- P3-4: Task7.3 artifact is edited for status rows, so the duplicate scheduling
  section is removed here, with all22 rows and whole-artifact pin refreshed.

## Measured provenance and accounting

184 rows / 48504 B / fd6ad0449e706d5ecf3f9dab552a9ad5fc3ef1be95df13304afba91e1d9ca8c8; 173 synchronized,
8 exact superseded, 3 bootstrap-only; unresolved operations: NONE (empty table).
semanticApprovalEligible=true means synchronized-corpus eligibility only, not this
transition's independent approval or source authority. Guard recomputes these values.
Canonical inventory 14/547/7165dac4e1a57022a7890b421f522bf4152f6d5ddc159a41539ce2ef0d18ec9f;
preambles 14/1,233/595528c6a7ba56dd5648e7fc12ac6bc2af9e9bf6fa3fbf853b86fffdd7cecd3c;
active directories22/1,808/2869d4ad4541be53adeac5819091605eff85c39b5140abec4438c668ac99b6e2.
Permanent provenance catalog13 retains the RC-3 artifact unchanged.
One new Fact exercises lifecycle resolution/withdrawal/archival; expected
Infrastructure is244, not243. Crosswalk is340 physical sources/1,406 declarations,
active718, with the retirement ledger unchanged. Maintained-after provenance names
this guard slice; no previous acceptance evidence is reclassified.

## Validation and independent controls

The final handoff reports completed runs, not assumptions: non-incremental Debug/
Release -warnaserror; exact12-package feed; Core350/Ephemeral79/Durable99/Acceptance37/
Hosting24/Certification96/PostgreSQL101/SQLServer72/Integration11; Infrastructure244,
exact14 ExpectedRed (258 total); focused corpus16; strict OpenSpec21; diff --check.
Verify all three states: clean checkpoint, evidence-state freeze cleared retaining
history pointer, and 1.4-only activation. They must stay244 without a guard edit.

Each lifecycle rule has its own negative control: active+archive duplicate;
registry rows+owner withdrawn; undated archive; missing owner. A valid dated-only
owner is positive. Also test stale evidence/activation-as-evidence, a fifth
supersession, reopened1.3, early2.1, old MAY text, unrelated canonical drift,
edited addendum/approved contract and proposed-status flips. Run targeted controls
without the active freeze so temporary content anchoring cannot mask the rule.
These are registry/document guards, NOT implemented DG-P22–24 scheduler behavior.

## Exact freeze recipes and four anchors

1. Base HEAD/tree above, unchanged; no staging/commits in main.
2. Raw manifest: exact git status --porcelain=v1 --untracked-files=all --no-renames,
   LF/finalLF; 2427 B / 57d94a5f5e52ad8bb76871b1e2286d9bbd8d48467fa3b5286099894a9251b32c.
3. Semantic record: omit docs/review/** and ONLY review-manifest-provenance.json
   and immutable-document-history.json; path<TAB>RAW length<TAB>lowercase RAW SHA,
   ordinal-sort FULL rows, LF-join/finalLF, UTF8-noBOM. 32 rows /
   4166 B / 06730a2208aa0ebe3efe493cb24085f7069d5ec668fc09171f50410cb101bd88.
4. Active record: omit ONLY review-manifest-provenance.json; XY<TAB>path<TAB>RAW
   length<TAB>RAW SHA, same sort/encoding; its bytes/hash are in activeFreeze and
   repeated in the final handoff. That record includes this request. Therefore its
   own hash and the all-file staged tree cannot be embedded here without circular
   hashing; the final handoff states both exact values after freezing. Verify a
   copied-index rehearsal and all raw-file/blob bytes before review/checkpoint.

## Verdict and subsequent sequence

Write one immutable dated APPROVE/REJECT verdict naming this exact target in a
disposable review copy or as an extra untracked record AFTER finishing the freeze
comparison. Never edit this request, manifest or earlier history to fit findings.
Approval authorizes only the named manifest paths: checkpoint, sole direct-child
evidence adding/cataloging that verdict and clearing freeze (history pointer kept),
then 1.4 checkbox only. Source slices need their own reviews AFTER that chain.
