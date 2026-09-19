# Harmonization Task 7.2 independent review verdict

**Date:** 2026-09-16
**Reviewer:** independent review
**Scope reviewed:** the sixteen-entry Task 7.2 immutable-history freeze on base
`261d578b11a4b2d09e033aa44c8123cbc4ec653e`, tree `cc4943fa88d38e23726d435e84b6bc6602222cbc`, named
by `harmonize-downstream-capability-specs-task-7-2-dirty-manifest-2026-09-16.txt`.
**Authorization scope:** none. This verdict rejects the target as frozen.

## Summary

Task 7.2's intent is sound, and most of the target verifies: the approval chain, freeze anchors,
review-manifest pins, the OO-1 closure, the telemetry race fix, declaration accounting, the ledger,
and every product and provider lane. It is rejected on two measured defects in the immutable-history
guard it introduces.

- **PP-1 (P1, blocking): the catalog pins bytes that exist only in the author's working copy.**
  25 of the 429 byte-pinned records hash files whose working-copy bytes contain carriage returns that
  the committed Git blobs do not. On any clean checkout the new guard fails. It failed in my fresh
  validation worktree and again in the simulated committed checkpoint, at **222 passed / 15 failed /
  237**, where the fifteenth failure is `Task72_…`. CI would fail the same way. The request's
  223/223 was measured against a checkout that `git status` reports as clean but that diverges from
  `HEAD` for those 25 files.
- **QQ-1 (P2, blocking): no record can be admitted without editing guard source.** Every new file
  under the historical roots — including this verdict, every future request and manifest, and the
  "new dated superseding record" the archive index now instructs authors to write — turns the
  must-be-green guard red until three guard-source constants are changed. I measured this by
  committing the target and adding a verdict. The evidence commit that registers an approval would
  therefore have to edit guard source outside review, and that undermines the anti-rehash property
  the design relies on.

One observation is recorded, not blocking:

- **RR-1 (P3):** the lease-fixture change is reasoned, but it removes the scenario's exposure to a
  deliberately select-once timeout arbitration whose spec wording deserves an explicit decision. The
  new polling helper also spins without a delay or timeout.

## Method

All work ran in two disposable `git worktree` copies created from `261d578b`, one for validation,
the simulated checkpoint, and an evidence-commit simulation, and one for the lease race experiments.
Each received the sixteen frozen entries byte-for-byte with porcelain confirmed byte-identical to the
frozen manifest. The reviewed worktree was never modified, `HEAD` never moved, nothing was staged, and
the index was not refreshed. Both worktrees were removed and pruned, and the throwaway commit is
contained in no ref.

Working-copy divergence was confirmed read-only with `git hash-object --no-filters` against
`git rev-parse HEAD:<path>`. PowerShell 7 is absent on this host, so the review-manifest `-Check` was
reproduced by the Python emulation used since round 54.

Because the two blocking defects are measured directly by validation and by the evidence simulation,
I did not run the target's full catalog mutation suite; its outcomes cannot change this verdict.

## 1. Freeze anchors reproduced

- **Raw commit-real porcelain:** **1,211 bytes**, SHA-256
  `8c7cb3f404b7a567e09cba5ff293cb0fc757e4fffb629a5d52265372c93bc6c0`, byte-identical to the frozen
  manifest. Sixteen entries (thirteen modified, three untracked), none staged, index tree `cc4943fa`
  equal to the `HEAD` tree.
- **Scoped content record:** **15 rows, 2,190 bytes**, SHA-256
  `74e9985de3760ac143fe85e6c3c92ba0e73430d98f452b5aa2d88afbee266b34`.
- **Scope:** zero changes under `src/**` or canonical `openspec/specs/**`.

## 2. The approved Task 7.1 hardening chain carries zero drift

- **Checkpoint:** `f618ede0` has parent `e7f8e26c` and tree `c612ed39d67556ee65670baf54095d9821d62e7e`,
  identical to the tree of my own round-59 simulated checkpoint.
- **Approval evidence:** `a7d9c523` adds the round-59 verdict, whose committed blob hashes to
  `b8c64aba6ede0ea43c7bbd3c5a2c27caaac29173de61dcfe7a39039248bd717b`. Entry 7.1 now registers all four
  Task 7.1 verdicts.
- **Activation:** `261d578b` changes exactly two values.
- **Projection:** all fourteen archived freezes reproduce manifest, content record, and tree from
  their checkpoint blobs. All ten entries reproduce their historical rows with maximal pins — Task
  7.1 correctly falls from sixteen to eleven for the five still-pinned rows this target changes — and the emulated `-Check` is
  order-exact.

## 3. PP-1 (P1, blocking) — the catalog is not reproducible from Git

From the author's working copy, the catalog reproduces exactly: 431 discovered files equal 429 records
plus the two mutable paths, every record matches, and the ordinal record is 66,788 bytes /
`208590d2…`. That is not the state anyone else gets.

Comparing every record against three sources — the fixture, a fresh checkout, and the committed blob —
finds **25 records** where the fixture matches only the author's working copy:

| Example record | Fixture / working copy | Fresh checkout / committed blob | CR bytes, working copy → blob |
|---|---|---|---|
| `docs/archive/architecture/design-proposal-minimal-core.md` | 48,415 | 48,414 | 1 → 0 |
| `docs/archive/architecture/engine-runtime-diagrams.md` | 9,709 | 9,390 | 319 → 0 |
| `docs/review/findings/R3-ephemeral-engine.md` | 15,627 | 15,457 | 170 → 0 |
| `docs/review/developer-facing-interface-section-07-exit-review-dirty-manifest-2026-07-30.txt` | 49,472 | 48,794 | 678 → 0 |
| `docs/review/test-scenarios/README.md` | 3,088 | 3,039 | 49 → 0 |

The remaining twenty cover four more archive files, nine more review dirty manifests, and the other
seven `docs/review/findings/R*.md` records. Every one of the 25 has CR bytes in the working copy and
none in its blob. For the sampled files, `git hash-object --no-filters` on the working copy yields a
different object from `HEAD:<path>` (for example `b66c9c1a…` against `3d20b690…`), while `git status`
still reports them unmodified. The author's checkout silently diverges from `HEAD`.

The effect is measured, not inferred:

| Run | Result |
|---|---|
| Fresh validation worktree, full guard lane | 222 passed, **15 failed**, 237 total |
| Simulated committed checkpoint (16 paths, tree `db679d156f75f58449c14d8c5d045774400a8b1c`), full guard lane | 222 passed, **15 failed**, 237 total |
| Unexpected failure in both | `Task72_…`: `design-proposal-minimal-core.md` "must retain its recorded byte length" (48,415 expected, 48,414 found) |

CI checks out on `ubuntu-latest` and will see the blob bytes, so the must-be-green Infrastructure lane
fails on the checkpoint this request asks to authorize.

The hashing is also checkout-dependent by construction. The guard hashes raw bytes; no
`.gitattributes` rule covers either historical root; the repository sets `core.autocrlf=false` while
this host's system configuration sets `true`. A default Git for Windows clone that does not inherit
the repository override may therefore check these text files out with CRLF and fail on far more than
25 records.

**Remedy.** Generate the catalog from committed blobs rather than working-copy files. Then make the
comparison checkout-independent: either declare both historical roots `-text` in `.gitattributes` so
checkout never rewrites their bytes, or hash LF-normalized content as the Task 7.1 record does.
Validate the guard in a fresh clone before freezing. Separately, the author's working copy should be
reconciled with `HEAD` for these 25 files. Every local measurement taken there is suspect until it is.

## 4. QQ-1 (P2, blocking) — admitting any new historical record requires an unreviewed guard-source edit

The guard requires the classified set to equal every file discovered under `docs/archive/` and
`docs/review/`, and it pins the catalog's count, byte length, and digest as guard-source constants.
Nothing exempts records the review-manifest provenance fixture already byte-pins, and no text
describes how a record is admitted.

I measured the consequence. After committing the sixteen frozen paths in the disposable worktree, I
added a correctly named verdict file,
`harmonize-downstream-capability-specs-task-7-2-independent-review-verdict-2099-01-01.md`. `Task72_…`
went red on "every archived or review document must be explicitly immutable or one exact mutable
surface".

That breaks the checkpoint protocol at its most routine step:

- **Registering an approval requires editing guard source.** Each approval's evidence commit adds a
  verdict under `docs/review/`, so it must also change `ImmutableDocumentHistoryCatalogCount`,
  `…CatalogBytes`, and `…CatalogSha256` plus the fixture. Evidence commits have been mechanical
  registration outside review. A guard-source edit made there could equally rehash a *rewritten*
  historical record, which is exactly what those constants exist to prevent.
- **The documented correction procedure trips the guard.** Rule 5 of the archive index tells authors
  to add a new dated superseding record. Following it turns the must-be-green lane red, with no
  instruction on what else to change.

**Remedy.** Separate the immutable baseline from routine admission. For example: classify review
evidence that the review-manifest provenance fixture already byte-pins (requests, manifests, and
verdicts of entries, archived, rejected, and active freezes) from that registration. Keep the
guard-source digest over the catalog as it stands at Task 7.2. Require any later catalog addition to
be append-only against that pinned baseline, and document the admission step beside rule 5.

## 5. What verifies

- **OO-1 is closed.** The Task 7.1 clarification now names
  `openspec/changes/harmonize-downstream-capability-specs/design.md` and `…/tasks.md` by full path,
  and the pinned guard constant matches.
- **Telemetry race fix.** `MeterListener` may publish instruments concurrently, so capturing into a
  `ConcurrentDictionary` is the correct correction. The source guard forbids regressing to
  `Dictionary`, and the durable lane is 99/99.
- **Lease helper boundary.** `SecondStarted` is set synchronously by the second attempt's body before
  it returns, so a persisted terminal status cannot precede it. The inline `StartOrGetAsync` operation
  was the wrong boundary, and the new terminal-status comparison is correct.
- **Accounting and ledger.** One new `[Fact]` moves declarations to 1,392 / 704, consistently in the
  crosswalk, the recovery guard, and the reshape 7.20 sentence. The harmonization ledger is 26 complete
  / 8 open / 34 total.
- **Other lanes.** Everything outside `Task72_…` is green in my runs, as the validation table shows.

## 6. Observation RR-1 (P3) — the leased first body now acknowledges cancellation

The shared blocking step's first attempt now calls `ThrowIfCancellationRequested()` after its release
gate. The pinned design text explains why: to prevent "a late successful return from winning
scheduler-dependent arbitration".

That arbitration is real and deliberate. `DurablePolicyWinnerSelector.TimeoutWonAsync` selects once
with `Task.WhenAny`, and `DurableDeadlineWinnerRaceTests` pins in IL that the selection is never
re-observed, noting there is "no deterministic seam". The timeout task uses
`RunContinuationsAsynchronously`, so its selection continuation is queued. An execution task that
completes before that continuation runs is selected as the winner. The driver's success path does
not consult the `timedOut` flag set by the timer callback. A separate test already covers a
token-ignoring attempt that returns only after its retry commits.

Two points follow:

- **Coverage narrowed.** These scenarios no longer exercise a token-ignoring body that returns
  success just after its deadline, which the durable-runtime requirement discusses explicitly.
- **Spec wording.** `docs/specs/04-requirements-core-runtime.md` says "Reaching the deadline before a
  winning result wins the timeout race". Whether "reaching" means the timer firing or the selector
  observing it deserves an explicit decision in the owning change rather than an implicit one in a
  test fixture.

I could not reproduce a divergent outcome. With the token-ignoring body restored and the new helper
kept, all five fixture scenarios plus `no-overlapping-leased-retry` passed 20 of 20 runs at four-way
concurrency. An attempt to force a one- or two-thread pool starved the test host before any test ran
and produced no data.

Separately, `AwaitSignalBeforeWorkflowTerminalAsync` polls snapshots in a tight `Task.Yield` loop with
no delay and no overall timeout. That adds CPU pressure under the concurrent lanes it was written to
stabilize, and it hangs rather than failing if neither condition ever occurs.

## 7. Validation reproduced from a clean checkout

| Gate | Result |
|---|---|
| Debug build, non-incremental, warnings as errors | 0 warnings, 0 errors |
| Release build, non-incremental, warnings as errors | 0 warnings, 0 errors |
| Exact package feed | 12 packages |
| Core / Ephemeral / Durable / Acceptance / Hosting / Certification | 350 / 79 / 99 / 37 / 24 / 96 |
| PostgreSQL / SQL Server / Integration | 101 / 72 / 11 |
| Full guard lane | **222 passed, 15 failed, 237 total** |
| Classification of the 15 failures | 14 `ExecutableBehaviorExpectedRedGuards.Scenario_…` plus **`Task72_…` (PP-1)** |
| OpenSpec `validate --all --strict` | 18 / 18 |
| Harmonization ledger | 26 complete, 8 open, 34 total |
| Review-manifest current matches | order-exact for all ten entries (emulated) |
| `git diff --check` / changes under `src/**` | clean / zero |
| Evidence-commit simulation (committed target plus one verdict) | `Task72_…` red on classification (QQ-1) |

## 8. Sequencing for the remediated target

Writing this verdict adds a seventeenth entry, and under this target's own guard it is also an
unclassified historical record. The remediated freeze must carry this file, classify it under
whichever admission design resolves QQ-1, and register it as the first Task 7.2 verdict.

## 9. Reviewer hygiene

`HEAD` remained at `261d578b11a4b2d09e033aa44c8123cbc4ec653e` throughout, nothing was staged, the index
was not refreshed, and I created no commit in the reviewed repository. It still showed exactly the
sixteen frozen entries when this verdict was written. Both disposable worktrees were removed and
pruned.

## Determination

Making historical immutability executable is the right goal, and the supporting changes in this
target are sound. But the guard as frozen is not reproducible from Git: it encodes 25 working-copy
byte sets that no clean checkout has, and it fails the must-be-green lane on the checkpoint it would
authorize. It also cannot admit a single new review record without an unreviewed guard-source edit,
which collides with the checkpoint protocol and with its own correction rule.

Regenerate the catalog from committed blobs with checkout-independent hashing, give routine review
evidence an admission path that does not require editing guard source, validate in a fresh clone,
refreeze, and re-request.

**Verdict:** **REJECT**
