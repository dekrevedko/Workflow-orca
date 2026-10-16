# Reshape Task 8.0 WWW-1/XXX-1 remediation — independent re-review request

Date: 2026-09-26. Base: `4ef6253e31aaad3698afcbf7fd2d3eaa0dda0bb8`.
This supersedes two uncommitted Task 8.0 freezes, both independently REJECTED.
Their manifests, requests, and verdicts remain immutable and cataloged. This
packet is a planning gate only: no Section 8 product source was changed, and
8.1 must not start until this exact target is independently approved and
checkpointed.

Review the exact target in
`developer-facing-interface-section-08-task-8-0-www-xxx-remediation-dirty-manifest-2026-09-26.txt`.
The manifest must be byte-identical to raw
`git status --porcelain=v1 --untracked-files=all --no-renames` in Git order.
Manifest: 1,798 bytes, SHA-256
`1f13174694835a9baace6f78a8f75a3a2e6d0ce4cc437d8290499ea28a85039d`.
For the scoped content record, exclude only this request and
`tests/OrcaCore.DeveloperSurface.Guards/Fixtures/immutable-document-history.json`;
for every other manifest line emit the two status bytes, TAB, path, TAB, raw
byte length, TAB, lowercase SHA-256; sort records ordinally, LF-join with one
final LF, and hash UTF-8. Record: 17 rows, 2,793 bytes, SHA-256
`a233956fd034cad17b975d6c55eca3cc655dfd3d3def5ff4cc6b90c57c2d88ac`.
The universal review-history guard checks the excluded
request's normalized bytes separately. No target commit exists yet.

## Findings to verify

1. **WWW-1:** The Task 8.0 artifact now routes document 16 DR-041/DR-AC-008
   to 8.4–8.6 and compiled exit 8.10, AC-528's parked-child DAG ceiling to
   8.7/8.10, and AC-321 create-or-observe identity to 8.9/8.10. Their waivers
   are pending-owner statements, not claims of executable completion. AC-317
   is outside the DAG slice; its stale 8.4/9.2 attribution is replaced by an
   explicit open 9.1 diagnostic/sample/evidence obligation. Every waiver that
   names an 8.x task is enumerated and guarded. The 8.5 row cites DU-031/033
   and DR-037/041, naming the one outbox and its internal claimant.
2. **XXX-1:** The 8.10 closure guard recognizes the ledger parser's equivalent
   `[x]`/`[X]`, indentation and spacing forms and reports **all** missing IDs.
   Trait extraction ignores line/block comments and string-literal decoys,
   with an in-process negative control. The unrelated JS-AC-007 trait is
   removed from the provider pool-capacity test; its watcher journey is
   explicitly pending 8.9/8.10 and has no current executable credit. The old
   physical-file catalog still sees an excluded legacy trait, so no
   overlapping waiver is added to that transitional catalog.
   The new baseline is 34 exit criteria: 0 active trait-bearing sources,
   24 compile-excluded-only, 10 with no trait source. A waiver or old excluded
   source never counts as compiled evidence.
3. **P3 carry-forward:** JS-AC-018 includes 8.9 and no longer misattributes
   itself to 8.7; JS-003 and JS-AC-005 include 8.6 registration conflict;
   the gate's failure reports every missing criterion. The previous document
   14, DU-033, zero-pending OpenSpec, package-skeleton, and nine expected-red
   decisions are otherwise unchanged.
4. **Immutable history:** The second REJECT verdict is byte-exact in
   `docs/review/` and registered in `appendOnlyRecords`. Neither prior review
   request, manifest, or verdict has been edited. The active-freeze pointer
   names this packet's manifest.

## Requested validation and mutation probes

Reproduce Release/Debug `-warnaserror` builds, product/provider test lanes,
Infrastructure separately from the 14 intentional ExpectedRed scenarios,
strict OpenSpec validation, `git diff --check`, both anchors, and a simulated
checkpoint path set/tree. With mutable current-match pins coherently refreshed
in a disposable copy, test `[X]`, double-space and indented 8.10 closure;
comment-only and string-only traits; a restored JS-AC-007 pool trait; removal
of each newly routed criterion or waiver; and the full missing-ID diagnostic.
Do not mutate or commit the reviewed target while probing. A new dated
independent `APPROVE` is required before checkpointing or any 8.1 source work.
