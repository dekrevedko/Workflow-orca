# Independent review: combined DAG Task 2.1 protocol/providers and Released erratum

Date: 2026-10-29. Authoritative review copy: E:/d21.
Parent/base: 0a8e375fbcf5005a2a2f491cf24dfbbd9d64c580. The target is UNCOMMITTED, index empty.
Do not review or stage the unrelated SQLite planning in the main checkout.

## Requested decision and exact scope

Review one coherent data-plane implementation of the remaining Task 2.1 work:
closed protocol/codec, all eleven provider ports, atomic run persistence/replay,
FIFO fencing/discovery/subscriptions, complete first-create PostgreSQL/SQL Server
schemas, all three providers and permanent certification, with the carried
round-100 fixes and the dated Released erratum. The self-inclusive manifest names
95 commit-real paths. No new runtime pump, Hosting bridge, mapper scheduler,
role registration, child-start delivery or public DAG execution is implemented.
Tasks 2.1–3.2 and reshape 8.4/8.5 stay open; no activation is proposed for this slice.

Primary records under openspec/changes/admit-durable-dag-run-progression/artifacts/:
- task-2-1-protocol-provider-errata-2026-10-29.md
- task-2-1-protocol-provider-api-candidate-2026-10-29.md
- task-2-1-protocol-provider-signatures-2026-10-29.cs
- task-2-1-protocol-provider-implementation-2026-10-29.md

Those four records and the proposed registry object are durably source-pinned.
They are candidate decisions, not an author-created approval. Old RC-3, rejected
packets, fingerprint refinement, canonical transition, verdicts and deltas remain
byte-exact. No canonical spec, numbered spec, friend grant, public application
fingerprint factory, business codec permission or package edge changes.

## Reconciled base and approval consumption

The DAG branch was fast-forwarded from 82811cd to master 0a8e375fbcf5005a2a2f491cf24dfbbd9d64c580, including
already-merged preview.3 publication/release-gate work, without making a DAG
checkpoint. The main checkout's uncommitted DAG work and SQLite planning were
preserved; the latter is excluded from this independent review copy. The recovery
stash is retained. This target neither publishes packages nor alters that release.

The fingerprint amendment is consumed through checkpoint a6f02fa5be9c31db232e3582d2f77d35acf86639,
tree dcf4337daffef0630f917f57d563011e43d330ec, direct-child evidence
3c2cd9606db6e4380b148c5f8dd3503e367f255f, and the byte-exact 2026-10-15 verdict
f8c0082e190d5656211caf5e76c318f5044b370524caad053287097aecfda3c2.
The real Git parent, tree, verdict addition/bytes and ancestry are checked, not
only a fixture echo. Task 1.4 is locked complete, the stale CLAUDE transition
sentence is fixed, and its Task 7.3 row/digest are refreshed.

Older independent-change guards project only this separately checked DAG
extension before comparing every unrelated registry value. The codec guard keeps
live byte pins on its actual codec/Core/ephemeral boundary, verifies historical
qualification sources against their Git commit, and retains the release's
historical-harness treatment. Later DAG/provider code inherits no native support
qualification. This is not permission to edit the codec or release evidence.

## Exact public surface and representation

Relative to the original RC-3 comparison baselines the cumulative totals are
7 OrcaCore / 193 Runtime.Protocol / 166 Provider.Abstractions = 366 keys. The seven
OrcaCore keys already shipped, and OrcaCore is unchanged in this target. Against
this base, additions are 193 and 166 respectively, with zero removals.

The sole new contract key beyond the independently approved fingerprint catalog
is DagRunLeaseOutcomeV1.Released = 6. The codec type and two methods were ALREADY
included in the approved 193 keys: there is no codec-count erratum. The signature
file differs from its fingerprint predecessor only by Released. Its standalone
proof compiles as OrcaCore.Runtime.Protocol with only OrcaCore.dll, inside the
repository SDK pin, with no friend, provider/runtime reference or reflection.
Compiler extraction is checked against the two refreshed exact API baselines;
all other baselines are unchanged.

Fingerprints in storage DTOs are 64 uppercase ASCII hex characters, validated
against exact business bytes; operation/checkpoint/checksum/start-key digests
retain their lowercase rules. Fixed-order record/checkpoint framing, defensive
copies, exact property allowlists and strict coordinates fail closed. Node IDs
must also be untrimmed valid Unicode without NUL and representable in the
approved storage bound; rejection occurs before effects, not by provider coercion.

## Implementation points requiring substantive review

- One fact plus optional terminal closure per append; immutable graph/run input,
  exact checkpoint reduction, authored-order admission, deterministic child IDs,
  cancellation/failure blocking, independent progress and terminal/integrity rules.
- Compact intent-hash replay before stale fence/version checks. The erratum states
  the concrete canonical operation framing and the explicit idempotent release
  outcome; no new business-value serialization is introduced.
- Real database transactions and clocks after locks, exact FIFO request identity,
  five-second renewal and fifteen-second absolute turn bound, stale rejection,
  lost-response replay and cross-instance contention. No mapper runs in a turn.
- Six relational DAG tables and complete first-create DDL with binary comparison
  and hex-case checks. No ALTER/upgrade compatibility migration is introduced.
- SQL Server keeps its existing shared logical outbox in the transaction-locked
  workflow document; DAG tables and that document update share the same SQL
  transaction/lock. PostgreSQL adds three nullable fields to its existing outbox.
  This physical disposition is explicit in the erratum, not hidden as a new port.
- Provider-private linked reducer/framing sources are excluded from the contracts
  assembly. Exact project links, package source provenance, compile-exclusion
  ownership and declaration accounting disclose this choice; no public helper or
  extra assembly appears.
- DAG/observer/external pressure partitioning, bounded/paged recovery discovery,
  expired observer cleanup that retains claims/poison/business work, and seven
  approved BCL gauges with existing low-cardinality tags.
- Minimum fail-closed safety in the EXISTING external pump and hosted selector:
  exclude all four DAG kinds and release wrong-lane records even when explicitly
  selected. There is no new internal pump and no acknowledgement by a sink.

## Permanent tests, controls and limits of credit

DagRunCertification independently builds wire records/checkpoints/hashes instead
of calling provider-private builders. Its eight cases run against each provider:
creation/replay/discovery, fenced append/notices, FIFO/deadlines, failure closure,
observer maintenance, concurrent leases, cancellation/integrity and cross-kind
GUID collision. Both SQL providers use real containers. Core has eighteen codec
cases. Early workflow rejection asserts inbox/outbox absence, global event counts
and identity-before-cancellation; looking at an unrelated workflow tail alone is
not accepted. External pump tests cover all four kinds and default/explicit claims.

These contribute to data-plane portions of DG-P01–05/10–12/17–18/20, not full-row
runtime certification. Child crash interleavings, installed Hosting behavior,
mapper isolation/order, role-free delivery and DG-P06–09/13–16/19/21–24 remain for
later targets. Fourteen ExpectedRed scenarios retain their disposition.

Focused mutations ran only in E:/op21, with exact root/base checks and restoration:

| Mutation | Intended permanent detector |
| --- | --- |
| Reopen activated 1.4 | Whole planning hash |
| Reopen 1.4 and coherently re-pin the whole planning hash | Actual Complete task-state rule |
| Add an unlisted product source | Exact source path set |
| Edit the native-qualified codec | Retained live codec byte pin |
| Extend turn deadline to 60 seconds | Real provider contract test |
| Drop lower-ordinal admission check | Authored admission assertion |
| Drop checkpoint validation | Zero-effect invalid-checkpoint assertion |
| Drop external wrong-lane rejection | Four permanent pump cases |
| Remove early workflow identity validation | Identity-before-cancelled-I/O assertion |
| Repoint approval to a real activation, editing expected source coordinates too | Actual single-parent direct-child Git check |

All ten are RED at those checks; restored sources are byte-verified, rebuilt and
positive controls rerun. No full-lane result is used as a substitute for naming
which assertion detected a mutation.

## Validation and reproducibility

Debug and Release -warnaserror builds: 0 warnings/errors; twelve exact local
packages. Core 372, Ephemeral 352, Durable 106, Acceptance 37, Hosting 24,
provider certification 108; PostgreSQL 111, SQL Server 82, Integration 11, no skips.
Infrastructure 311/311 (two added Facts), exactly fourteen ExpectedRed (325 total).
Strict OpenSpec: 25/25 in this copy, which excludes SQLite planning. Compile/package
fixture logs and frozen/committed/evidence-state rehearsals are retained outside
the target and their final results are reported in the handoff. No activation
exists; source and errata stay proposed until independent review.

ReSharper InspectCode 2026.2.3.1 ran on all changed compiled C# paths with
Configuration=Release;Platform=Any CPU, --no-build after a compatible full build,
and solution-wide analysis enabled. E:/d21-resharper.sarif has zero errors,
452 warnings and 220 notes, including pre-existing findings in whole touched
files. Retain this scope when comparing counts. Triage: linked private sources
explain namespace/unused-member findings; constructors intentionally throw in
negative tests; controlled clocks/awaited assertions explain closure findings;
early reader disposal before the next database command is intentional. The
remaining naming/style suggestions and pre-existing findings are not silently
suppressed or auto-cleaned. No new functional finding was identified.

Declaration record: 370 files / 1,545 declarations; active 215 / 857, excluded
131 / 688, out-of-band 24 / 0. Canonical provenance is unchanged: 206 rows /
54,532 B / 63ee51d0001b33218e687a34e16058b5c972f8451de2c3454045facc5a96cd4e,
zero pending; all fourteen canonical contents remain pinned at f61d8b49b25095b31989639fb43a8ac6cae6fedc996ddfe1bbf4ec7233b4fae2.

## Freeze conventions (no circular/self hashes)

Manifest: developer-facing-interface-section-08-task-8-4-dag-run-task-2-1-protocol-provider-dirty-manifest-2026-10-29.txt
7059 B / SHA-256 1934b1604357b5a5084ffffd701be38e131e2177bec5a0df696c80cd3aa29bf2.
It is byte-equal to git status --porcelain=v1 --untracked-files=all --no-renames,
including itself, this request and both history/review fixtures.

Semantic record: 91 rows / 12747 B /
SHA-256 40042e49effa5c70b117ce1acae5986dcebf4ec8411edd5bfbb26df1a8ad375c.
Recipe: every pre-packet candidate path except review-manifest-provenance.json;
ordinal PATH order, path\tRAW-byte-count\tSHA-256-of-RAW-bytes\n, UTF-8 without BOM.
The request, manifest and immutable-history fixture are metadata exclusions;
their bytes are still covered by the active record and staged tree.

The active record excludes ONLY review-manifest-provenance.json. Render each
manifest row as XY\tpath\tRAW-byte-count\tSHA-256\n and sort WHOLE rendered rows
ordinally, with LF and a final LF. Its count/bytes/SHA are in activeFreeze and
the handoff. NUL porcelain and the exact staged tree/all-blob checks are also in
the handoff: a tree containing this request cannot be embedded in its own bytes.
All content is LF. Git configuration and the empty live index are checked.

## Decision and post-approval sequence

Write one immutable APPROVE or REJECT verdict under the task-2-1-protocol-provider
stem, including scope and new findings. Do not edit this packet, the old history
or the reviewed files. An approval authorizes only the exact 95-path tree in
the handoff. It does not activate Task 2.1 or authorize pumps/bridge work.

After approval: recheck zero drift, checkpoint exactly the manifest paths from
this isolated target, then in its only direct child add/catalog the verdict and
clear activeFreeze while keeping activeFreezeManifestPath. No activation commit.
Do not git add -A in main, where unrelated planning remains. Do not publish,
archive or push on the strength of this review request.
