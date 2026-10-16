# Harmonization Task 6.3 independent review verdict

**Date:** 2026-09-01
**Reviewer:** independent review session (cold verification, no authoring involvement)
**Target:** the dirty worktree named by
`docs/review/harmonize-downstream-capability-specs-task-6-3-dirty-manifest-2026-08-31.txt`
**Base:** `2dbf2b94168e4338c1fa8af742573545287f2f9d`
**Requested authorization:** create only the Task 6.3 checkpoint commit

## Scope

This verdict covers the nine Task 6.3 claims, the freeze anchors, the prior checkpoint chain, the
schema 10 provenance registry, and the declared validation packet. It does not complete Tasks
6.4-8.3, archive the harmonization change, or authorize reshape Task 8.0.

## Method

Every claim was recomputed rather than accepted. The reviewed worktree was never modified, staged,
or committed. All builds, suites, container lanes, mutations, and the simulated checkpoint ran in a
disposable `git worktree` created at the declared base with the sixteen target files copied in; that
worktree was removed and pruned before this verdict was written. No probe commit is reachable from
any ref.

## 1. Freeze anchors

Reproduced three times - live target before validation, independent clean checkout, live target
after validation. All three agree.

- raw porcelain (`--untracked-files=all`): **1,168 bytes**,
  `5f6cb97df7ed146e54614234429336ce04e7a8478b7b128033eac2017091ec9e` - matches the declared value.
- `...task-6-3-dirty-manifest-2026-08-31.txt` is **byte-identical** to live porcelain (`cmp` against
  `git status` output: no difference). There are no phantom rows.
- scoped content record over the fifteen non-fixture paths: **2,146 bytes**,
  `dcb93375c045ccf0f0b5525e218936b3098d6243cf7f532c108a0538dc389a28` - matches the declared value.
- entries: **16** - 13 modified, 3 untracked, 0 staged.

The unscoped sixteen-row record is 2,300 bytes /
`9837f4f809a2a90fac0651e5f8144200102b1b9a8ef6f8e1af1bcd7d6784039d`; it is claimed nowhere and is
recorded only so the scoping is unambiguous.

## 2. Prior checkpoint chain

- `7abf95e3` (parent `532940a3`) contains **exactly the fifteen paths** of the Task 6.2 target I
  approved on 2026-08-31, verified by set difference.
- `bf6eb959` carries my Task 6.2 verdict **byte-exact** at
  `486b73cebf7a5048f7a655227f73d60405b6191356e458d13044b41b1b5b10fd`.
- `2dbf2b94` is the one-path mechanical activation and its tree is the declared base tree
  `b6d66882`.

**All four findings I raised on 2026-08-31 are closed, and I verified each fix directly rather than
from its description:**

- **T-1** - `RequireIndependentReviewAuthority` now rejects an owner-approval verdict on
  `ApprovalAwaitingEvidenceCommit` and `Approved`, its mirror rejects an independent verdict on the
  two `Owner*` states, and `ValidateReviewAuthoritySemantics` proves both throws in-process.
  `ArchivedReviewFreeze` gained `Authority` plus `AuthorityEvidencePath`, each bound to a registered
  immutable verdict. Probe P16 confirms it fires: relabelling the owner-authorized
  `6.1-post-review-hardening` freeze as `IndependentReview` produces
  `Independent review state 6.1-post-review-hardening cannot use owner-approval evidence ...`.
- **T-2** - the CR-014a block terminator is now `"\n##"`, matching its sibling guard.
- **T-3** - the complete CR-014a block is SHA-256 pinned. Probe P12 confirms an appended
  contradiction now fails. See U-1 for the half of this fix that was not applied.
- **T-4** - Task 6.3's ledger entry, and now `AC-029` itself, name all three contributing normative
  sources.

Schema 10 records were recomputed from first principles:

| record | manifest | path-sorted | parent/tree | committed paths | scoped or blob record | authority | pins |
|--------|----------|-------------|-------------|-----------------|-----------------------|-----------|------|
| archived `6.1-remediation` | exact | exact | exact | exact | exact | IndependentReview, bound | n/a |
| archived `6.1-post-review-hardening` | exact | exact | exact | exact | exact | OwnerAuthorization, bound | n/a |
| archived `6.2` | exact | exact | exact | exact | exact | IndependentReview, bound | n/a |
| entry 5.1 `Approved` | exact | exact | - | - | exact | - | 9, exact + ordered |
| entry 5.2 `Approved` | exact | exact | - | - | n/a | - | 4, exact + ordered |
| entry 5.3 `OwnerAuthorized` | exact | exact | - | - | exact | - | 3, exact + ordered |
| entry 6.1 `Approved` | exact | exact | - | - | exact | - | 3, exact + ordered |
| entry 6.2 `Approved` | exact | exact | - | - | exact | - | 4, exact + ordered |

Pin reductions for 6.1 (4 to 3) and 6.2 (11 to 4) are exactly forced by the maximality rule against
the files Task 6.3 edits; every remaining pin verifies byte-for-byte.

## 3. The nine Task 6.3 claims

**1. Exclusive bidirectional links.** Confirmed. `CR-009a` gains `Acceptance criterion: `AC-028`.`
and `CR-014a` gains `AC-029`; each criterion appears exactly once in the catalog; and the guard
asserts mutual exclusion in both directions plus `AC-022` absence. P1, P2, P6, and P7 are all RED.

I also verified that the re-pinned CR-014a block hash is honest rather than a laundered edit: the
diff of the complete block against `2dbf2b94` is **exactly** the two added lines, nothing else, so
`8fd23512...` to `27f38ec1...` reflects only the acceptance-criterion backlink.

**2. AC-028 covers the lifecycle.** Confirmed against `CR-009a` clause by clause: `Open` to
`JoinPending`, one join winner returning a successor `Open` epoch, root terminal freezing one
immutable snapshot, rejection of stale / superseded / escaped-callback / duplicate-join /
post-terminal / losing-race handles **before graph mutation**, and repeated-build stability. P10
(dropping "before graph mutation") is RED.

**3. AC-029 covers failure provenance.** Confirmed against `CR-014a` and the implementation I
verified last round: creation-time authored/runtime provenance, unchanged one-failure propagation,
one owning multi-cause join failure, authored-branch and item-index ordering, non-negative indexes,
the closed `orcacore-json-v1` graph, and rejection of unknown versions/discriminators, missing
variant data, and malformed payloads.

**4. AC-029 cites its normative companions.** Confirmed, and I resolved each citation by hand:
`quality-and-verification` line 212 and `structured-fiber-execution` line 118 both carry the exact
headings the anchors slugify to, and `docs/specs/17` exists. This is the right place for those
sources - it converts my T-4 note from task prose into the criterion itself. P5 is RED. See U-2 for
what the guard does **not** check about these links.

**5. AC-022 not reused.** Confirmed. It remains only on the two fingerprint tests; the guard asserts
its absence from both the `CR-014a` block and the `AC-029` criterion, and P8 (re-adding it to
`FailureProvenanceTests`) is RED on both the Task 6.2 and Task 6.3 guards.

**6. Traits and executability.** Confirmed, and **proven load-bearing independently of the new
guard**: probe P19 removes `[Trait("AC", "AC-028")]` from `AuthoringLifecycleTests` and the
pre-existing `RepositoryGuardTests.AcceptanceCriterionCatalog_HasTraitCoverageOrExplicitWaiver`
fails naming `AC-028`. The catalog cannot be silently orphaned. P3 and P9 (runtime traits) are RED.

**7. Non-vacuous must-green guard.** Confirmed. `Task63_...` is a `[Fact]` on the
`Disposition=Infrastructure` class, and all five of the author's declared negative controls
reproduce exactly (P1-P5), along with seven further mutations I devised.

**8. Crosswalk delta.** Confirmed: 1,386 to 1,387 declarations and 698 to 699 active declarations,
with `physicalFiles` unchanged at 337 because no source file was added - the delta is exactly the
one new guard fact. `Task720CompletionNote_MatchesTheExecutableCrosswalkAccounting` re-pins the
reshape ledger prose to the fixture, and P15 confirms a count edit is RED. The 866-row retired
ledger is untouched.

**9. Scope boundaries.** Confirmed by inspection of all sixteen paths: no `src/**`, no
`openspec/specs/**`, no public API baseline, no package manifest. Task 6.3 is `- [x]`, 6.4 remains
`- [ ]`, and the ledger is **21 complete / 13 open / 34 total**.

I also confirmed that the round-43 `Q-2` correction survives: both canonical `workflow-authoring`
and its reshape delta still enumerate the callback-local `scope` handle in the requirement body and
its scenario.

## 4. Validation reproduced from an independent clean checkout

Disposable worktree at `2dbf2b94` with the sixteen target files copied in; both anchors reproduced
there before any build.

| gate | result |
|------|--------|
| Debug `dotnet build OrcaCore.slnx --no-incremental -m:1 -warnaserror` | 0 warnings, 0 errors |
| Release, same flags | 0 warnings, 0 errors |
| exact package feed | 12 packages |
| `AC=AC-028|AC=AC-029` (Core) | 28/28 |
| `AC=AC-029` (Ephemeral / Durable) | 1/1, 1/1 |
| `OrcaCore.Core.Tests` | 350/350 |
| `OrcaCore.Engine.Ephemeral.Tests` | 79/79 |
| `OrcaCore.Engine.Durable.Tests` | 99/99 |
| `OrcaCore.Acceptance.Tests` | 37/37 |
| `OrcaCore.Hosting.Tests` | 24/24 |
| `OrcaCore.ProviderCertification` | 96/96 |
| `OrcaCore.Providers.PostgreSql.Tests` | 101/101 |
| `OrcaCore.Providers.SqlServer.Tests` | 72/72 |
| `OrcaCore.Integration.Tests` | 11/11 |
| `Disposition=Infrastructure` | 218/218 |
| `Disposition=ExpectedRed` | exactly 14 failures, 0 passed |
| unfiltered guards | 232 total = 218 green + 14 expected red |
| `openspec validate --all --strict` | 18 passed, 0 failed |
| `git diff --check` | exit 0 |

Every declared number reproduces exactly. The container lanes are genuinely container-backed: the
SQL Server lane reports a 5-second execution time, which looked implausible, so I watched Docker
while it ran and observed `testcontainers/ryuk` and `mcr.microsoft.com/mssql/server:2022-latest`
start - the reported duration excludes fixture startup, and no test is silently skipped
(0 skipped across all three container lanes).

Committability: a simulated checkpoint produced **exactly 16 real paths**, a clean worktree, and
218/218 afterwards, exercising the schema 10 committed-blob branch.

## 5. Mutation and probe results

Control green (11 tests across the three affected guard classes). RED:

| id | mutation | result |
|----|----------|--------|
| P1 | `CR-009a` backlink to `AC-999` | RED (author's control 1) |
| P2 | `AC-029` binding to `[CR-999]` | RED (author's control 2) |
| P3 | remove the ephemeral runtime AC trait | RED (author's control 3) |
| P4 | narrow the Core CI filter to `AC=AC-028` | RED (author's control 4) |
| P5 | remove the `structured-fiber-execution` citation | RED (author's control 5) |
| P6 | duplicate `AC-029` elsewhere in the catalog | RED |
| P7 | cross-link `AC-029` into the `CR-009a` block | RED |
| P8 | re-add `[Trait("AC", "AC-022")]` to failure provenance | RED (Task 6.2 + 6.3) |
| P9 | remove the durable runtime AC trait | RED |
| P10 | drop "before graph mutation" from `AC-028` | RED |
| P11 | revert the Task 6.3 ledger checkbox to `[ ]` | RED (Task 6.2 pin) |
| P12 | append a contradiction to the `CR-014a` block | RED (block hash - T-3 fix) |
| P14 | forge the scoped content anchor by one hex digit | RED |
| P15 | tamper the crosswalk active-declaration count | RED |
| P16 | relabel an owner-authorized archived freeze as independent | RED (T-1 fix, exact message) |
| P18 | swap the order of the two class traits | RED |
| P19 | orphan `AC-028` by removing its class trait | RED (independent Core catalog guard) |

GREEN, and these are findings U-1 and U-2:

| id | probe | result |
|----|-------|--------|
| P13 | append a contradiction to the **`CR-009a`** block | **Task62 and Task63 both GREEN** |
| P17 | point `AC-029`'s `structured-fiber-execution` link at a nonexistent heading | **Task63 GREEN** |

## 6. Findings

### U-1 (P3) - the T-3 block-hash fix was applied to `CR-014a` only

`Task62_...` pins the complete `CR-014a` block to an exact SHA-256, so an appended contradiction
fails. `CR-009a` has no equivalent pin. P13 appends "A frozen session MAY be reopened for further
mutation." - which directly contradicts the frozen-terminal clause two paragraphs above - and both
`Task62_...` and `Task63_...` stay green. `Task63_...` does read the `CR-009a` block, but only to
assert that it contains `AC-028` and does not contain `AC-029`, so a contradictory addition passes
both checks.

This is the same defect class I recorded as T-3 last round, now half-closed. The two requirements
are peers introduced by consecutive tasks and reviewed under the same standard; one is immutable
reviewed text and the other is not. Adding a `CR-009a` block hash alongside the existing one closes
it symmetrically.

### U-2 (P3) - normative-companion citations are label text, not verified links

Claim 4 is satisfied in substance - I resolved all three companion targets by hand and each exists.
But the guard checks only that the strings "`quality-and-verification` executable evidence",
"`structured-fiber-execution` join ordering", and "public-contract companion" appear. P17 repoints
the `structured-fiber-execution` anchor at `#requirement-this-heading-does-not-exist` and
`Task63_...` stays green. Since the whole point of the citation is that a future reader can follow
it to the governing text, a link that silently stops resolving defeats it. Both spec files are
already read by this guard class, so asserting that each anchor slug matches a real heading is a
few lines.

### U-3 (P3) - the reshape ledger's maintenance list stopped at Task 6.2

`7.20` now reads "337 sources / 1,387 declarations", and
`Task720CompletionNote_MatchesTheExecutableCrosswalkAccounting` pins those numbers to the fixture -
so the count is correct and guarded. But the sentence that carries it still says the inventory was
"maintained after ... harmonization task 6.1, and harmonization task 6.2", while the number it now
quotes is the post-Task-6.3 number. Every prior task appended itself to that list. The pin covers
the digits, not the provenance sentence around them, so this drifted silently.

### U-4 (P3, brittleness) - the trait pin is an exact ordered multi-line string

`Task63_...` asserts the literal
`[Trait("Requirement", "CR-009a")]\n[Trait("AC", "AC-028")]\npublic sealed class ...`. P18 swaps the
two attribute lines - semantically identical to xUnit - and the guard is RED. That is stricter than
the runtime-evidence check in the same guard, which uses `ContainAll` over the captured attribute
block and is order-independent. Not a correctness defect; it will simply fail a future harmless
edit for no reason. The `ContainAll` form used a few lines below is the better shape.

## 7. Operational note on landing this verdict

Writing this file into the reviewed worktree makes the live projection **17** rows against a
**16**-row manifest, and `ValidateActiveReviewFreeze` asserts raw-order equality between the two, so
the Infrastructure lane goes RED until they are reconciled. Commit the approved sixteen-path
checkpoint first, then add this verdict; or refreeze to seventeen entries before validating.

## 8. Reviewer hygiene

- The reviewed worktree was never modified, staged, or committed by this reviewer. `HEAD` is
  unchanged at `2dbf2b94168e4338c1fa8af742573545287f2f9d`, nothing is staged, and both anchors
  reproduce after validation exactly as they did before it.
- All builds, suites, container lanes, mutations, and the simulated checkpoint ran in a disposable
  `git worktree`, now removed and pruned. `git log --all` contains no probe commit.
- This reviewer created no commit in the reviewed repository.

## Determination

All nine claims hold. The bidirectional mapping is exclusive in both directions and mutation-proven
seventeen ways, including all five of the author's declared controls; `AC-028` and `AC-029` are
executable through an independent pre-existing catalog guard, not only through the new one; the
re-pinned `CR-014a` block hash reflects exactly the two-line backlink and nothing else; the freeze
anchors, three archived freezes, and five registry entries all reproduce independently; the complete
validation packet - including the three container lanes, which I confirmed are genuinely
container-backed - reproduces from a clean checkout; and the checkpoint is provably committable at
exactly sixteen paths. Every finding from the previous round is closed and verified at the fix, not
at its description. U-1 through U-4 are durability and consistency gaps, not correctness defects.

**Verdict:** **APPROVE**
