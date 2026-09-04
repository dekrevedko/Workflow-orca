# Harmonization Task 6.5 post-review-hardening independent review request

**Date:** 2026-09-03
**Requested verdict:** `APPROVE` or `REJECT`
**Authorization requested:** create only the Task 6.5 post-review-hardening checkpoint

The approved Task 6.5 chain is:

- checkpoint `ffc87b282f89e5d99ffa508c237d6968e03bddd4`;
- approval-evidence commit `172002f82f45a252fe2869b854695edcf3d2f9f1`; and
- mechanical activation `4f3b71a85372a041c4b6ee90a0096856922f41c9`.

The immutable Task 6.5 verdict is 10,979 bytes with SHA-256
`6616e9ba4bd764ff8b2c20da06ce509748d1a310907352b281e75f340d737928`.
This request reviews only finding Y-1 from that verdict. It does not authorize Task 6.6, change
archival, or reshape Task 8.0.

The exact target is named by
`harmonize-downstream-capability-specs-task-6-5-post-review-hardening-dirty-manifest-2026-09-03.txt`.
Recompute the commit-real raw manifest and scoped content record from base
`4f3b71a85372a041c4b6ee90a0096856922f41c9`.

## Claims to verify

1. `OpenSpecCorpusGuards` owns the reviewed companion SHA-256
   `41f6472c2774363d2ab922c608922e787ec241333e1d1c0b76b0c6d529ab8ec3` as a named source
   constant rather than trusting only mutable fixture data.
2. The Task 6.5 guard first requires `v1-public-contract.json` to reproduce that source-owned pin,
   then requires `docs/specs/17-public-authoring-contract.cs` to match it byte-for-byte.
3. A differently named companion declaration plus a coherent fixture SHA update is red. The exact
   mutation that was green in the Task 6.5 review is therefore closed after archival as well as
   during the active freeze.
4. The companion itself, its public-contract fixture, all twelve public API baselines, and product
   source remain byte-unchanged. The prior exhaustive baseline continues to protect compiled public
   visibility independently.
5. The approved Task 6.5 freeze, verdict, evidence commit, and activation remain registered exactly;
   the current-byte pin refresh removes only historical paths legitimately changed by this hardening.

## Negative controls

- append a differently named public companion declaration and coherently update the fixture digest:
  Task 6.5 guard red on the source-owned constant;
- alter the fixture digest alone: Task 6.5 guard red;
- alter the source-owned constant alone: Task 6.5 guard red; and
- alter any immutable Task 6.5 request, manifest, verdict, or checkpoint record: provenance guard red.

## Validation to reproduce

- Debug and Release warnings-as-errors builds: 0 warnings / 0 errors;
- focused Task 6.5 and review-provenance guards: green;
- Infrastructure 220/220 and exactly 14 separately classified intentional expected reds;
- OpenSpec strict 18/18, task ledger 23 complete / 11 open / 34 total, and `git diff --check` clean;
- zero changes under `src/**`, canonical `openspec/specs/**`, the companion, or public API baselines.

Do not edit, stage, or commit the target. Return one dated immutable `APPROVE` or `REJECT` verdict.
