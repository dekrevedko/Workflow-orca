# Task 6.4 post-review hardening

Date: 2026-09-02

## Approved checkpoint chain

The independently approved remediation is preserved at checkpoint `5e38de27a99a1a5ba5bd55a3ac2beb49f1b043e0`.
Commit `df3d866c925f4b7ca525d21ad28634de957a7480` records the immutable approval verdict, and
`adc6a8f542a6e5362096df8a7213e06f55f11ad2` mechanically activates that evidence. This hardening
starts only after that chain and does not rewrite its request, manifest, artifacts, or verdicts.

## W-1: maintained-inventory provenance

Reshape task 7.20 now names harmonization task 6.4 as the task that produced the current
337-source / 1,388-declaration inventory. The accounting guard requires the current task suffix,
so restoring the stale task-6.3 attribution fails even when the numeric totals remain correct.

## W-2: complete semantic-appendix synchronization

The guard derives the complete published canonical appendix from the immutable reshape source
artifact through the reviewed publication-only transformations: canonical link routing, removal of
the temporary lease-promotion note, and the task-6.4 negative admission wording. It compares the
entire result byte-for-byte with the canonical appendix. Drift in `## Laws`, any other section, or
the excluded-claims list therefore fails after the dirty freeze is archived.

## W-3: immutable documentation roots

The active-document scan now excludes both `docs/archive/` and `docs/review/` through one named
immutable-prefix allowlist. An occurrence placed in either immutable evidence tree remains green;
the same occurrence in active documentation remains red.
The pre-existing guard against consuming archived documentation recognizes only this exact
exclusion declaration; it continues to reject every attempt to use archived content as current guidance.

## Mutation evidence

Against a green focused control:

1. restoring the stale task-6.3 inventory owner failed the accounting guard;
2. changing an independent law in the canonical appendix failed the full-source projection;
3. adding a second archive-path reference to the active-corpus guard failed the historical-input guard; and
4. adding historical evidence beneath `docs/archive/` remained green by design.

Every mutation was byte-restored. Product source, canonical OpenSpec requirements, public API
baselines, package manifests, and the declaration crosswalk are unchanged.