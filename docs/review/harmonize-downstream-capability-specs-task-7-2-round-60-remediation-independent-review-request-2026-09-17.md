# Harmonization Task 7.2 Round 60 remediation independent review request

**Date:** 2026-09-17
**Requested verdict:** `APPROVE` or `REJECT`
**Authorization requested:** create only the remediated Task 7.2 immutable-history checkpoint

Review this superseding target on immutable base
`261d578b11a4b2d09e033aa44c8123cbc4ec653e`. Do not edit, stage, or commit it.
Return one dated immutable verdict. The raw-order manifest is
`harmonize-downstream-capability-specs-task-7-2-round-60-remediation-dirty-manifest-2026-09-17.txt`.

## Prior rejected freeze

The 2026-09-16 Task 7.2 target was rejected by Round 60. Its request, raw-order manifest, and
`REJECT` verdict remain byte-immutable and are registered as rejected freeze
`7.2-initial-rejected`. This target supersedes that freeze; it does not rewrite it.

## Findings that must be closed

### PP-1 — checkout-independent immutable baseline

The fixed historical baseline is rebuilt from the exact Git blobs at the immutable base commit,
not from checkout bytes. Its record normalizes CRLF and lone CR to LF before length and SHA-256.
Guard source owns the baseline commit, record format, normalization rule, count, aggregate byte
count, and aggregate digest. Every baseline worktree file is normalized the same way before it is
compared, so an `end_of_line = crlf` checkout remains green while a real content edit remains red.

### QQ-1 — later evidence must not require source edits

Guard-source constants pin only the fixed Task 7.2 baseline. Later immutable review evidence is
admitted through the existing byte-pinned `review-manifest-provenance.json` registry. Later archive
successors use the separate `appendOnlyRecords` set: before checkpoint the record must belong to
the active freeze; afterward its first Git addition permanently binds the normalized content.
The archive index explains both admission paths. Unregistered review evidence, an unowned
uncommitted archive successor, a changed baseline file, and a changed committed archive successor
remain failures.

### RR-1 — retain token-ignoring lease coverage without an unbounded poll

The leased retry body again ignores its cancellation token after physical release; the added
cancellation acknowledgement is removed. The real workflow-instance terminal boundary remains the
race peer for `SecondStarted`. Polling now uses a delayed ten-millisecond interval and a fifteen-
second `TimeProvider.System` deadline, producing a named failure instead of spinning or hanging.
The source guard requires that exact cancellation-ignoring and bounded-observation shape.

## Mutation evidence

Against green focused controls in a disposable checkout:

1. a CRLF-only transform of a baseline file stayed green;
2. appending real content to that same file turned the immutable-history guard red;
3. an unregistered review verdict turned it red;
4. registering that verdict through existing review provenance restored green without changing a
   guard-source constant;
5. an uncommitted archive successor recorded outside the active freeze turned it red;
6. adding that successor to the active freeze restored green;
7. the final byte-restored control was green; and
8. the complete Section 6 executable scenario lane passed four consecutive 32/32 runs with the
   deliberately token-ignoring lease body.

## Validation

- Clean-checkout focused immutable-history guard: 1/1.
- Debug build: 0 warnings / 0 errors.
- Release non-incremental warnings-as-errors build: 0 warnings / 0 errors.
- Core / Ephemeral / Durable / Acceptance / Hosting / ProviderCertification:
  350 / 79 / 99 / 37 / 24 / 96.
- PostgreSQL / SQL Server / Integration: 101 / 72 / 11.
- Infrastructure guards: 223/223.
- Expected-red lane: exactly 14/14 intentional failures.
- Full guards: 223 passed / 14 expected red / 237 total.
- Strict OpenSpec validation: 18/18.
- Harmonization ledger: 26 complete / 8 open / 34 total.
- `git diff --check`: clean.

## Exact target

The target contains nineteen commit-real paths: the thirteen tracked implementation and accounting
updates, the original rejected request/manifest/verdict, the immutable-history fixture, and this
superseding request/manifest. No `src/**` or canonical `openspec/specs/**` path is changed.

The sole active-freeze content-record exclusion is the self-referential
`tests/OrcaCore.DeveloperSurface.Guards/Fixtures/review-manifest-provenance.json`.
