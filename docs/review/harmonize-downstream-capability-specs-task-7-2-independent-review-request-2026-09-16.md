# Harmonization Task 7.2 independent review request

**Date:** 2026-09-16
**Requested verdict:** `APPROVE` or `REJECT`
**Authorization requested:** create only the Task 7.2 immutable-history checkpoint

Review this target on immutable base `261d578b11a4b2d09e033aa44c8123cbc4ec653e`. Do not edit,
stage, or commit it. Return one dated immutable verdict. The raw-order manifest is
`harmonize-downstream-capability-specs-task-7-2-dirty-manifest-2026-09-16.txt`.

## Scope and decision

Task 7.2 makes historical-document immutability executable rather than conventional:

- `immutable-document-history.json` classifies every file under `docs/archive/` and
  `docs/review/` as byte-immutable except exactly two mutable surfaces: the active archive index
  and reusable review template.
- The guard source owns the complete catalog count and aggregate digest. Missing, modified,
  unclassified, duplicate, reordered, or coherently fixture-rehashed records fail.
- `docs/archive/README.md` directs corrections into a new dated superseding record plus an active
  index update rather than a rewrite of historical conclusions.
- The existing anti-archive-input guard remains intact: Task 7.2 uses its one approved immutable
  documentation-prefix declaration rather than treating historical prose as current guidance.
- The declaration crosswalk and Task 7.20 prose now account for the new guard at 337 sources /
  1,392 declarations.
- Validation closed the recurring 3.11c retry-fixture flake: both second-attempt checks now race
  `SecondStarted` against the real workflow instance terminal status rather than the inline
  `StartOrGetAsync` operation. The source guard pins both call sites, the snapshot boundary, and
  the post-snapshot signal recheck so a signal completing during observation cannot be misclassified;
  the first protected body also acknowledges its selected timeout only after physical release, making
  the timeout-vs-completion arbitration deterministic under scheduler load;
  the formerly failing retained-bindings scenario passed 25 focused repetitions.
- Parallel validation also exposed a durable telemetry-test race: `MeterListener` publication callbacks
  now capture the exact catalog in a `ConcurrentDictionary`, and the telemetry source guard forbids
  regression to the corruptible mutable `Dictionary` shape.

This target also closes Task 7.1 finding OO-1 by naming the exact harmonization `design.md` and
`tasks.md` paths whose pre-finalization TSV rows intentionally predate their final text.

## Mutation evidence

Against a green focused control, each unauthorized mutation failed:

1. edit a byte-pinned review verdict;
2. remove a byte-pinned record;
3. add an unclassified review file;
4. broaden the mutable-path list;
5. coherently edit a historical record and rehash the fixture catalog.
6. regress durable metric capture from `ConcurrentDictionary` to mutable `Dictionary`;
7. remove the first leased body's post-release cancellation acknowledgement.

A harmless edit outside the pinned rule in the active archive index remained green, proving the
two-state classification rather than universal write protection. Every probe restored its inputs
byte-for-byte.

## Validation

- Debug build: 0 warnings / 0 errors.
- Release non-incremental warnings-as-errors build: 0 warnings / 0 errors.
- Core / Ephemeral / Durable / Acceptance / Hosting / ProviderCertification:
  350 / 79 / 99 / 37 / 24 / 96.
- PostgreSQL / SQL Server / Integration: 101 / 72 / 11.
- Infrastructure guards: 223/223; four concurrent complete lanes also passed 4×223/223.
- Expected-red lane: exactly 14/14 intentional failures.
- Full guards: 223 passed / 14 expected red / 237 total.
- Strict OpenSpec validation: 18/18.
- Harmonization ledger: 26 complete / 8 open / 34 total.
- Review-manifest maximal-current-match refresh check: green.
- `git diff --check`: clean.

## Exact target

The target contains sixteen commit-real paths: the archive index, this request, its raw-order
manifest, the harmonization design and task ledger, the reshape Task 7.20 provenance sentence,
five guard sources, one behavior-scenario source, one durable-test source, the review-provenance
fixture, the declaration crosswalk, and the new
immutable-history fixture. No `src/**` or canonical `openspec/specs/**` path is changed.

The immutable-history catalog contains 429 byte-pinned historical records plus the exact two
mutable surfaces. The sole active-freeze content-record exclusion is the self-referential
`tests/OrcaCore.DeveloperSurface.Guards/Fixtures/review-manifest-provenance.json`.
