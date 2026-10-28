# Issue #13 codec-member corrections — self-review, 2026-10-28

**Verdict: local implementation fixes pass review; required native-host
requalification remains pending. Do not advance publication.**

This is an adversarial author self-review, not an external approval. The user
authorized the corrections with “Fix them”. The later no-push instruction remains
in force; a separate question requests permission for the temporary evidence
branch. No commit, push, PR, merge or publication is authorized by this report.

## Implementation review

Only three product files differ from approved checkpoint 8566986:

- FixedWorkflowValueCodec excludes write-only descriptors from Generated shape
  validation, matching the legacy serialized-member selection. It retains the
  public-property prepass and validates every value actually returned to the
  serializer, including fields, internal properties and collection elements.
- WorkflowJsonTypeContracts excludes write-only members from transitive contract
  lookup. The separately approved synthetic-initializer omission restriction is
  still classified before that filter and its existing negatives still pass.
- WorkflowJsonMetadataRegistry installs the fixed getter validation while building
  engine-owned generated metadata. Legacy options use the same validation through
  a reflection resolver created lazily inside the existing JIT-only fence. There
  is no new public API, option, serializer hook, root or suppression.

F1's failing Generated test now admits both write-only DTOs, while serialized
forbidden maps and application converters continue to reject. Exact public input
fingerprints and typed workflow output agree with legacy mode.

F2's old silent subtype loss and forbidden collection acceptance now reject.
The tests cover fields, internal getters, nested lists/maps, ordinary-property
controls, approved polymorphism, ignored members, detached valid values, rejected
inputs before Init, invalid dynamic Init state before later bodies, reusable start
keys and an unrelated accepted instance retaining its exact wait and state.

Getter semantics were reviewed explicitly. A first implementation added an extra
read through an included member's child; a new regression failed in both modes.
The final implementation validates runtime type/collection shape at the actual
serializer read without recursively reading object properties there. Existing
public getter reads remain two, included getters remain one, and a child's getter
under an included field remains one. A public getter returning a different,
unapproved subtype on its second read is rejected without a third read. Both
Metadata and Default generated contexts execute the cases.

The callbacks retain only metadata, types and getter delegates, not application
model instances or service providers. Scalar reads avoid allocating a validation
set. The legacy resolver is not initialized in Generated mode; local full-trim
and Native AOT execution produced no product-path warning. JSON bytes, ordering,
limits, fingerprints, DI ownership and host lifecycle rules are otherwise intact.

## Tests and tools

Red/green receipts distinguish behavior failures from builds: F1 compiled cleanly,
legacy passed and Generated failed before the correction; F2 compiled cleanly and
both modes failed before the correction. All final controls pass.

- OpenSpec all/strict: 25/25; full Release warnings-as-errors: 0/0.
- Six CI unit projects: 969/969; full Infrastructure: 309/309.
- PostgreSQL/SQL Server/integration: 188/188; requirement selectors: 60/60.
- Fifty consecutive unfiltered dotnet test ephemeral runs, two at a time:
  17,600/17,600, no skips or failures.
- CI ReportGenerator 5.5.11 engine line gate: 15,956/20,112 = 79.3357%, above 75%.
- Exact candidate-feed/source/assembly provenance: passed. API, dependency and
  canonical/delta baselines are unchanged.
- Additional 13-case corpus: corrected legacy/Generated input fingerprints,
  definition fingerprints and detached values match pre-AOT master.
- Built ReSharper 2026.2.3.1, Release / Any CPU / net10.0: source 52-file scope
  has zero errors, 585 warnings and 344 notes; changed guards have zero errors,
  58 warnings and 63 notes. A complete fixture/dependency solution has zero errors;
  its fixture subset has 34 warnings and 45 notes.

ReSharper's new product diagnostics are naming/qualification suggestions and a
CanBePrivate suggestion that applies to the Core-linked codec copy; the method
must remain internal for the authoritative Abstractions registry. Fixture warnings
include intentional unused write-only setter values, global fixture namespaces
and existing naming style. A standalone fixture attempt had unresolved reference
cascades, and an overly narrow temporary-solution filter found no files; neither
is counted as successful analysis. The corrected four-project context is retained.

## Scope, provenance and remaining gate

The P3 acceptance sentence now describes regression assertions rather than future
implementation. Mechanical updates add one active guard and one fixture to the
test inventory, preserve the 866 retired dispositions, refresh only the two
changed package source digests and the affected document hash/pin, and bind the
correction source/harness hashes. Registry schema 22 leaves current correction
source/native approvals null. The prior source/whole-target approvals are checked
against their immutable Git checkpoint, not misapplied to these changed bytes.

Protocol 3 requires all original journeys/subcases plus the new member controls:
82 JIT and 55 full-trim/AOT cases per host, 384 total. Protocol 2 is readable only
as explicit history and cannot qualify the corrections. Warning decisions now
enter the harness identity. Historical native records and prior verdicts remain
untouched.

Local Windows 11 public-package preflights pass JIT 82/82, full-trim 55/55 and
Native AOT 55/55, with disabled JSON reflection in the latter two and disabled
dynamic code in AOT. Their final host gate correctly rejects Windows 11. The four
trim and eight AOT warning records are the unchanged handwritten-context negative
body at shifted lines; exact Windows identities/counts match their reviewed
decisions. Ubuntu decisions are prospective until the real rerun verifies them.

**No new Windows Server 2025/Ubuntu 24.04 matrix is claimed.** The earlier run
40629471843 belongs to the approved baseline. Current qualification/publication
remains held until the temporary validation push is authorized, all six real cells
pass for these exact bytes, and the complete correction target receives review.
The issue branch and correction target remain uncommitted and unpushed.

Full logs, TRX, compressed raw coverage, SARIF/triage, failed-first attempts,
preflight records and cleanup/freeze receipts are retained under
`.artifacts/publishing-validation/issue-13-codec-fixes-2026-10-28/`.
