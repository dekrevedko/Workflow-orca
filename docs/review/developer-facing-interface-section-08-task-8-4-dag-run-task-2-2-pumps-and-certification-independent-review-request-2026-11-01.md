# Independent review: combined DAG Task 2.2 pumps and carried certification

Date: 2026-11-01. Authoritative copy: E:/d21, branch codex/dag-run-task-2-2.
Parent/base: 602844547539e0e5908d250c2a0d9560fc4b25dc. Target UNCOMMITTED; live index empty.
Do not stage the unrelated SQLite planning in the main X:/ checkout.

## Decision and scope

Please independently review this one coherent bounded Task 2.2 implementation:
the dedicated every-durable-engine-host child lane, capable-sink-only ready claim
seam, deterministic child start/reattach, atomic terminal join intent, fenced
join/cancellation recovery, visible poison rules, three carried provider test
gaps and the stale CLAUDE.md correction. The exact self-inclusive manifest has
42 commit-real paths, including 15 product-source paths. No public API,
friend, package edge, canonical spec, delta, provider schema or business codec changes.
The twelve public API baseline files are unchanged.

The real Dag.Hosting ready sink/scheduler, observer hub, continuous run rediscovery,
full typed bridge and public end-to-end DAG execution remain Task 2.3. No no-op
sink is installed: without a capable sink, the ready consumer makes NO claim call.
This is not completion of DG-P22–24 or reshape 8.4/8.5. Tasks 2.1–3.2 remain
unchecked. Request approval of these exact bytes, not task completion or source
after this slice. There is no activation checkbox for this bounded target.

Primary implementation/disposition record:
openspec/changes/admit-durable-dag-run-progression/artifacts/task-2-2-dedicated-pumps-and-certification-2026-11-01.md
(durably pinned in guard source). The new separate pending registry object and
exact source list do not constitute self-approval.

## Approved base and retained authority

Task 2.1 checkpoint f62b0aafd8beeb69aa28efaba0f25413c4970b3b has tree
0e5300632b9804f096ac45d119a602b5b9f5b9c7 on parent
0a8e375fbcf5005a2a2f491cf24dfbbd9d64c580. Its sole direct-child evidence
602844547539e0e5908d250c2a0d9560fc4b25dc adds the 2026-10-29 APPROVE byte-exact:
8,809 B / c971876e87d871f5fce5b48bc685a918f7d2e1dd8b6dd81869068c4bf9d5584b,
catalogs it and clears the active freeze. No activation followed.

The new approval binding checks the actual Git tree, single parent, verdict addition,
unique final APPROVE, raw bytes and ancestry. Repointing both fixture and source to
the real checkpoint instead of its evidence child is rejected by the actual parent
check. The old candidate remains unchanged history. Its no-pump source-set assertion
now checks that historical checkpoint, rather than forbidding this later source.
All old packets and dated decisions remain byte-exact. Current-match pins are refreshed
to their exact maximal sets, not used as a substitute for durable verdict hashing.

## Review the runtime behavior, not only the pins

- AddOrcaCoreDurableEngine installs a dedicated owned child/ready/observer-prune
  service independently of IMessageDispatcher and the DAG role. Definitionless
  callback-only ingress still has no progression service. Child selectors contain
  only dag-child-start-v1, dag-child-join-v1 and dag-child-cancel-v1.
- Every child/continuation consumer releases a wrong-lane record BEFORE payload
  parsing, callback, poison or success. The external selector intersects arbitrary
  caller selections with reserved exclusions. Its existing workflow-event adapter
  has an explicit internal workflow-event lane, not a widened external dispatcher.
- A version-1 closed child signal and trusted run coordinate identify a durable
  operation. Deterministic child identity hashes run GUID plus ordinal only; exact
  workflow/version/fingerprint, declared contracts, committed input presence,
  format/bytes/hash and lineage are checked separately. Missing installed definition
  retries. Binding conflict is visible IntegrityFault, never successful reattachment.
- Engine.Durable alone uses its existing friend and fixed codec. Starting a missing
  child appends its normal continuation, not user execution inside a claim/turn.
  Cancellation materializes an authorized missing child only after a committed run
  barrier, then sends ordinary cancellation. Admission remains until terminal join.
- Child terminal facts, projections/checkpoint and join intent share one workflow
  transaction. Joining the separate run re-reads authoritative child facts, takes a
  bounded local-arrival/provider FIFO fence, then writes ChildJoined and any Terminal
  fact atomically. There is no cross-stream atomicity claim. Duplicate/lost-response
  delivery verifies the persisted outcome; it cannot free admission twice.
- Failed children block reachable Pending/Ready descendants in linear graph time,
  preserve independent work and derive the terminal run state. A malformed signal
  commits owning IntegrityFault BEFORE poison without freeing uncertain admission.
  Failed fault commits release/retry; an untrusted/corrupt owner is quarantined,
  never acknowledged as successful business work.
- Ready claims are exact-kind and null-or-local boot-host recipient only. An installed
  capable sink receives a closed, authoritatively checked notice before successful
  acknowledgement. Sink/storage exceptions release for retry instead of data poison.
  Real rediscovery must arrive with the production sink in Task 2.3, not be inferred
  from the test-owned sink used to exercise this shipped consumer.
- Hosted child/ready/prune loops are owned and joined; maintenance runs independently
  in bounded positive batches every ten seconds. Existing retry/failure boundaries
  and pump-observer telemetry are retained. No mapper executes in this target.
- The engine independently writes approved checkpoint/operation framing; providers
  independently reduce and validate it. Exactly six source-generated framing calls
  are registered in the existing codec guard. No provider-private reducer is linked
  into Engine or Hosting, no dynamic type-loading or new serialization permission.

## Carried findings and permanent certification

All three cases run against InMemory and real PostgreSQL/SQL Server containers:

| Missing contract rule | New failing-case evidence |
| --- | --- |
| Dead FIFO head expires after thirty seconds | Younger live waiter obtains the next grant; removing expiry fails |
| A turn may append only its purpose's record kinds | Foreign-purpose append rejects DAG_PROTOCOL_INVALID with no tail/checkpoint/outbox effect |
| Last node and run finish in the same append | Last join alone rejects with no effects; join plus Terminal succeeds in the same turn |

Correct code is green for all nine provider/case combinations. Each removed rule is
red on its own new certification case for ALL three providers (nine targeted red
executions, three distinct mutants). Existing tests are not weakened.

Seventeen Hosting cases exercise the shipped pump/engine/DI boundaries: wrong lane,
commit-before-ack, fault-before-poison, retry when fault persistence fails, no sink/no
claim, foreign boot, wake-before-ack, sink failure, deterministic crash recovery and
duplicate delivery, cancelled missing child, failure blocking, version rejection,
cancellation barrier, foreign stream collision and missing definition. Six durable
continuation wrong-lane cases additionally reject before decoding. The ready test
sink is disclosed and does not receive credit as the real Dag.Hosting scheduler.

CLAUDE.md now records the checkpointed Task 2.1 data plane, this pending pump slice
and the separate Task 2.3 remainder. Task 7.3 row 1/digest, planning/status pins,
declaration accounting and only the two changed package-source rows are refreshed.
No old dated status is silently rewritten.

## Scoped mutation proof and limitations

Mutations ran only in independently owned E:/p22 with root/base assertions. Product
and evidence source files were restored byte-exact before rebuilding positive controls.

| Mutation | Intended permanent detector |
| --- | --- |
| Remove dead-waiter expiry | New shared provider certification (all three providers) |
| Remove turn-purpose check | New shared provider certification (all three providers) |
| Remove atomic terminal closure check | New shared provider certification (all three providers) |
| Child lane handles foreign kinds | Wrong-lane no-callback/release assertion |
| Acknowledge before async child handler commit | Pending-handler acknowledgement assertion |
| Poison without committed owning fault | Fault-before-poison assertion |
| Change deterministic child identity recipe | Actual shipped child stream assertion |
| Omit atomic terminal join intent | Child recovery/terminal notice assertion |
| Omit run terminal closure in coordinator | Terminal run assertion |
| Drop cancellation barrier | Zero child effect before committed cancellation assertion |
| Leak completed provider turn | Fresh lease after failed-child join assertion |
| Claim ready work with no capable sink | No claim call assertion |
| Ready ack before accepted wake | Pending sink acknowledgement assertion |
| Ignore ready boot recipient | Wrong-boot release assertion |
| Accept unknown signal version | Both unknown-version theory rows |
| Coherently rebind Task 2.1 evidence to its checkpoint | Actual Git direct-child parent assertion |

All sixteen VALID distinct mutants are killed, zero survivors/timeouts/uncovered in
this explicitly scoped set. An initial early-ack edit was a compile error (CS0162),
discarded and replaced by a compilable premature-ack edit that the test kills. It is
not counted as a killed mutant. This is not a whole-repository mutation score.
Restored Hosting controls are 17/17; the two binding/source controls are 2/2, including
the latter in a CRLF checkout. Only the new baseline comparison normalizes checkout
line endings; shipped baseline content is unchanged. No whole-CRLF-lane claim is made.

## Validation and qualification

Observed before freezing: Debug and Release non-incremental -warnaserror builds have
0 warnings/errors. Twelve local packages pack. Core 372, Ephemeral 352, Durable 112,
Acceptance 37, Hosting 41, Certification 111, PostgreSQL 114, SQL Server 85 and
Integration 11 all pass, with zero skipped. Infrastructure is 313/313 (two new Facts),
with exactly fourteen ExpectedRed failures (327 total). Strict OpenSpec is 25/25.
Green package fixtures are 8/8; dag-hosting is the single intentional package red.
Compile infrastructure verifies 26 source and 26 package forbidden-member diagnostics.

Those full suites used their executable xUnit runners, not an instrumented coverage
run. No fresh coverage percentage, native-AOT qualification, public end-to-end DAG
execution or live production deployment is claimed. The frozen target, clean committed
copy and cleared-freeze evidence rehearsal are rechecked after this packet is written;
their actual results and the staged tree are supplied in the handoff, not placeholders.

Scoped ReSharper InspectCode 2026.2.3.1 completed with full solution-wide analysis,
--properties=Configuration=Release;Platform=Any CPU and --no-build after the Release
build. E:/dag22-inspection-final.sarif contains 0 errors, 77 warnings and 21 notes.
Scope covers changed product C# and runtime/provider test files in seven projects,
not guard-metadata C#. Findings include touched-file pre-existing closure warnings,
intentional best-effort telemetry catches, source-generated unused framing members,
pending-seam unused members and existing naming style. No functional defect identified;
no suppression or CleanupCode run. Retain this exact scope when comparing counts.

Declaration record: 374 physical files / 1,573 declarations; active 219 / 885,
compile-excluded 131 / 688, out-of-band 24 / 0. Canonical provenance remains
206 rows / 54,532 B / 63ee51d0001b33218e687a34e16058b5c972f8451de2c3454045facc5a96cd4e,
zero pending, all fourteen file contents pinned at
f61d8b49b25095b31989639fb43a8ac6cae6fedc996ddfe1bbf4ec7233b4fae2.

## Exact freeze recipes (no circular hashes)

Manifest: developer-facing-interface-section-08-task-8-4-dag-run-task-2-2-pumps-and-certification-dirty-manifest-2026-11-01.txt
3057 B / SHA-256 d17c81ba6d8cbced8106302b4bbdd003ceacc06678cbf0e5ee0d198b155fb8da.
Byte-equal to git status --porcelain=v1 --untracked-files=all --no-renames, including
itself, this request and both history/review fixtures. No phantom tracked entries:
the tracked set is also compared with git diff --name-only HEAD.

Semantic content record: 38 rows / 5177 B /
SHA-256 ffd72155ad3b760b79749b2d621fc2fba688cf2f875686c77d616efc63c571a4.
Recipe: exclude exactly this request, its manifest and the immutable-history and
review-provenance fixtures. For the remaining manifest paths sort by ordinal PATH;
render path\tRAW-byte-count\tSHA-256-of-RAW-bytes\n, UTF-8 without BOM, final LF.

Active content record excludes ONLY review-manifest-provenance.json. For each other
manifest row render XY\tpath\tRAW-byte-count\tSHA-256\n; sort WHOLE rendered rows
ordinally. Its count/bytes/hash are recorded in activeFreeze and in the handoff.
NUL porcelain and a status-less all-file record are independently reported there too.
Copied-index staging must yield exactly the manifest path set; every staged blob
must equal its raw file bytes and the staged diff must pass whitespace checks.
The staged tree cannot be embedded in its own request, so it is in the final handoff.

## Requested verdict and post-approval sequence

Use an immutable APPROVE or REJECT verdict under the task-2-2-pumps-and-certification
stem. Leave the exact target uncommitted pending that verdict; do not edit old packet
files or grant approval by refreshing metadata. Rehearsal commits exist only in an
independent disposable copy; no ref or commit is created in the authoritative repo.

If approved: recheck zero drift, checkpoint exactly the manifest paths on this base,
then in its sole direct child add/catalog the verdict and clear activeFreeze while
keeping the history-file pointer. No activation or source checkbox is authorized.
The next Task 2.3 target is separately reviewed. No publication, push or archival.
