# Harmonization Task 7.2 SS-1 / TT-1 remediation independent review request

**Date:** 2026-09-17
**Requested verdict:** `APPROVE` or `REJECT`
**Authorization requested:** create only the superseding Task 7.2 immutable-history checkpoint

Review this superseding target on immutable base
`261d578b11a4b2d09e033aa44c8123cbc4ec653e`. Do not edit, stage, or commit it.
Return one dated immutable verdict. The raw-order manifest is
`harmonize-downstream-capability-specs-task-7-2-ss-1-tt-1-remediation-dirty-manifest-2026-09-17.txt`.

## Prior rejected freezes

The 2026-09-16 Task 7.2 target and the 2026-09-17 Round 60 remediation target were both
rejected. Their requests, raw-order manifests, and `REJECT` verdicts remain byte-immutable and
are registered separately as `7.2-initial-rejected` and
`7.2-round-60-remediation-rejected`. This target supersedes both without rewriting either.

## Findings that must be closed

### SS-1 / TT-1 — one family-independent append-only rule

`immutable-document-history.json` must classify every post-baseline file under either
`docs/archive/` or `docs/review/` in one append-only set, independent of filename family and
independent of the harmonization review-state registry. While a record is uncommitted, its exact
path and normalized bytes must be pinned by the family-independent `activeFreezeManifestPath` and
this active reviewed freeze. After checkpoint, its
normalized bytes must equal its first Git addition. This must admit a future reshape-family review
record without a guard-source edit and must keep an approved request edit red after routine
review-manifest current-match refresh.

The harmonization `review-manifest-provenance.json` registry retains its additional authority,
verdict-state, checkpoint, and frozen-target semantics. It is no longer an admission exception for
immutable review bytes.

### UU-1 — line-ending-stable evidence

The leased-retry source guard normalizes line endings before its exact shape checks. The
append-only admission path parses the active manifest after CRLF-to-LF normalization while
retaining BOM rejection and the terminal-newline requirement. A CRLF checkout must not add either
of the two Infrastructure failures identified by the rejected review.

### VV-1 — exact finite deadline and baseline count

The source guard must pin
`TerminalObservationTimeout = TimeSpan.FromSeconds(15)`; an infinite timeout must fail. The
immutable-history fixture's `baselineCount` must equal the guard-owned count and the actual
baseline row count.

## Required negative controls

Against a green focused control in a disposable checkout, verify:

1. a new reshape-family `docs/review/` record is admitted by the active freeze without changing
   guard source;
2. after checkpoint, editing that record remains red even after all routine current-match pins are
   refreshed;
3. editing this request after simulated approval remains red for the same first-addition reason;
4. an uncommitted post-baseline file outside the active manifest is red;
5. converting the lease host and active manifest to CRLF remains green;
6. replacing the 15-second timeout with `Timeout.InfiniteTimeSpan` is red;
7. changing `baselineCount` alone is red; and
8. dropping either prior Task 7.2 rejection or changing either rejected verdict is red.

## Validation target

Reproduce Debug and Release warnings-as-errors builds, all product and provider suites, the
Infrastructure and expected-red lanes separately, strict OpenSpec validation, task accounting,
`git diff --check`, and a simulated checkpoint. No `src/**` or canonical
`openspec/specs/**` path may change.

## Exact scope and sequencing

The exact target is the companion raw-order manifest. It carries the previously reviewed Task 7.2
implementation, both immutable rejected packets, the universal post-baseline admission
remediation, the CRLF/deadline/count hardening, this request, and its manifest. Task 7.3 remains
blocked until this exact target receives independent approval and the approved checkpoint is
created.
