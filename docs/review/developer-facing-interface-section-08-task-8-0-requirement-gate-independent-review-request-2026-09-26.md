# Reshape Task 8.0 — independent requirement-gate review request

Date: 2026-09-26

Base: `4ef6253e31aaad3698afcbf7fd2d3eaa0dda0bb8`, the activated
harmonization Task 8.3 closeout. The Section 7A/7B checkpoints and the
harmonization planning approval, canonical synchronization, final-target
approval, checkpoint, evidence, and activation are prior prerequisites,
not part of this target.

Please review the exact uncommitted target in the companion
`developer-facing-interface-section-08-task-8-0-requirement-gate-dirty-manifest-2026-09-26.txt`.
Use Git-emitted raw porcelain order and verify every listed file's bytes.
This is a reshape review under its existing dated request/manifest/verdict
convention. The harmonization registry's task-name pattern is not reused
as an assertion of reshape approval.

The 11-line raw manifest is 953 UTF-8 bytes with SHA-256
`7e97b158a0c6de58a380b0ebad2bc7eb7b4e41988191c53e91903c4c653531f4`.
For an independently recomputable non-self-referential content record,
exclude only this request and
`tests/OrcaCore.DeveloperSurface.Guards/Fixtures/immutable-document-history.json`;
for each other manifest line emit `<two status bytes><TAB><path><TAB><raw byte length><TAB><lowercase SHA-256>`,
sort those records ordinally, LF-join with one final LF, and hash UTF-8.
That record has 9 rows, 1,386 bytes, and SHA-256
`6e612bd6005bad5f0e0efec384b78f588bae9e3d04f9d861d797f6b134d69ce1`.
The two excluded files remain in the raw manifest; the universal
append-only history catalog separately pins this request's normalized
bytes and verifies every review-root path.

## Decision being reviewed

Task 8.0 maps all tasks 8.1–8.10 to exact canonical OpenSpec requirements,
the selected-mode document 17, and AC-606–AC-618. The dated mapping artifact
records the 176-row / 46,211-byte / `ea8719e7…` canonical provenance
checkpoint with zero pending sync operations and semantic-approval eligibility.
It identifies all nine Task 3.10 DAG scenario rows, leaves every expected-red
disposition unchanged, and assigns each a later Section 8 turn-green task.
It explicitly treats `OrcaCore.Dag` and `OrcaCore.Dag.Hosting` as the existing
approved six-file package skeletons: task 8.1 reconciles them rather than
creating duplicate project/package IDs.

No `src/**`, canonical `openspec/specs/**`, package manifest, or product
behavior changes in this target. Section 8 source work remains blocked until
this exact 8.0 target receives independent `APPROVE` and a checkpoint.
Archiving harmonization is a separate action, not a prerequisite.

The implementation README and phased plan now distinguish the completed
harmonization checkpoint from Task 8.0's still-pending independent gate.
Because the phased plan is one of the 22 Task 7.3 hash-pinned sources, row 9
of its dated artifact and the guard-source artifact digest are refreshed in
this same target. The separate Task 8.0 artifact and its ledger gating
sentence are also pinned by guard source. The reshape accounting pin moves
from 129/30 to 130/29, and the review-provenance current-file pins are
mechanically refreshed.

## Review focus

1. Compare each of the ten mapping rows against both normative trees. In
   particular, verify runtime-owned DAG progression, one unified outbox for
   internal child starts (AC-613), root/parent lineage (AC-614), detached
   typed outputs, notification-driven terminal wait, and parked-child
   `MaxConcurrentNodes` lifetime. A disagreement requires a separately
   approved amendment before source work; do not silently correct it here.
2. Verify the complete nine-row Task 3.10 expected-red handoff, with no
   scenario reclassified as passing merely because the mapping is complete.
3. Verify the 8.1 reconcile-in-place decision against the two existing
   package roots and the exact twelve-package manifest.
4. Verify the phased-plan row 9 LF-normalized hash, refreshed Task 7.3
   artifact hash, Task 8.0 map hash, and the refreshed maximal-current pins.
5. Reproduce Release `-warnaserror`, Infrastructure and intentional
   ExpectedRed dispositions separately, strict OpenSpec validation,
   `git diff --check`, the raw manifest, and the scoped content record.

The current local validation before this packet: serial Release guard build
0 warnings/0 errors, focused accounting/provenance/documentation guards 3/3,
and OpenSpec strict 18/18. The full Infrastructure lane passed 226/226 after
the accounting and pin refresh; please rerun it on the final frozen packet.
The 14 expected-red executable-behavior scenarios are separate and remain
intentional.

Return a dated immutable `APPROVE` or `REJECT` verdict for exactly this
target. A verdict is not part of the frozen manifest. Do not checkpoint
this target or start task 8.1 source work before independent approval.
