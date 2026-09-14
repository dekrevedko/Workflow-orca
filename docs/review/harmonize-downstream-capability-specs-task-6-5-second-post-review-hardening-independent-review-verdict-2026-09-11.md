# Harmonization Task 6.5 second post-review-hardening independent review verdict

**Date:** 2026-09-11
**Reviewer:** independent review
**Scope reviewed:** the six-entry Task 6.5 second post-review-hardening freeze on base
`d501083a1879c241ed784565ad828997f620bb00`, named by
`harmonize-downstream-capability-specs-task-6-5-second-post-review-hardening-dirty-manifest-2026-09-04.txt`.
**Authorization scope:** creation of the Task 6.5 second post-review-hardening checkpoint only. This
verdict does not authorize Task 6.6, change archival, or reshape Task 8.0.

## Summary

The target closes review finding Z-1 by binding three documentary decisions to named guard-source
constants inside the Task 6.5 corpus guard: the Y-1 post-review-hardening ledger decision, the new
second-review ledger marker, and the `design.md` guard-source ownership paragraph. All six claims
and all four negative controls reproduced independently, and the closure is durable — the focused
guard reddens without any reference to the active freeze.

## Method

All probing ran in a disposable `git worktree` created from `d501083a` with the six frozen entries
copied in byte-for-byte; its porcelain was confirmed byte-identical to the reviewed worktree's
before any probe. The reviewed worktree was never modified, `HEAD` never moved, and nothing was
staged or committed in it. The worktree was removed and pruned afterwards and its one throwaway
commit is unreachable from every ref.

## 1. Freeze anchors reproduced

- Raw commit-real porcelain: **589 bytes**, SHA-256
  `8066b603f4fb54427dd10a8a08dc6521ec7fe494910920f015e0b7b7b843a62c`.
- The published dirty manifest is **byte-identical** to that live porcelain, not merely equal in
  hash.
- Scoped content record — ordinal-sorted `status\tpath\tbytes\tsha256` rows, LF-joined with one
  final LF, UTF-8 without BOM, excluding only the self-referential provenance fixture:
  **5 rows, 858 bytes**, SHA-256
  `504b6a2e721ea6e51c77cd48322bab74b6b4ac3504a18b598facd5e17cb2b7f1`.

Six entries: three modified, three untracked, zero staged. Both anchors match the declaration.

## 2. The approved hardening chain carries zero drift

Checkpoint `fba2c9a3476511025625e58dad46839b81851f0d` contains **exactly the seven real paths** of
the approved target, and its tree is `41041d92f49c16eb10e447c0107a7894336d2b69` — **precisely the
tree my own simulated checkpoint produced during the previous review**. The author's commit and my
rehearsal are the same Git object.

- Recovered dirty manifest: **636 bytes / `a597be2e…`**, byte-identical to the approved anchor.
- Re-projected scoped content record from that commit's blobs: **6 rows / 976 bytes / `68e7f3dd…`**,
  byte-identical to the approved anchor.
- The post-review-hardening verdict landed byte-exact at **16,269 bytes / `3cde1ea0…`** in evidence
  commit `59cab939d48c6e93742896d39462d09cb7d56ac5`.
- Activation `d501083a1879c241ed784565ad828997f620bb00` touches only the provenance fixture and is
  the declared base of this freeze.

The registration prediction made in the previous verdict held: because the discovery glob
`task-6-5*verdict-*.md` matches both files, the 6.5 entry now registers **both** verdicts
(`6616e9ba…` and `3cde1ea0…`), with `approvalEvidenceCommit` advanced to `59cab939`.

## 3. Provenance fixture integrity — claim 6

Schema 10; **eight** archived freezes; eight entries. Every archived freeze was recomputed from its
own checkpoint commit rather than accepted, including the newly added one:

| Archived freeze | checkpoint | manifest | projected record | tree |
| --- | --- | --- | --- | --- |
| 6.1-remediation | `9f4fa0b5` | exact | 15 rows / 2,336 B | exact |
| 6.1-post-review-hardening | `87da8c2b` | exact | 13 rows / 2,098 B | exact |
| 6.2 | `7abf95e3` | exact | 14 rows / 2,064 B | exact |
| 6.3 | `609e9c45` | exact | 15 rows / 2,146 B | exact |
| 6.4-remediation | `5e38de27` | exact | 13 rows / 2,064 B | exact |
| 6.4-post-review-hardening | `737f2fd4` | exact | 8 rows / 1,249 B | exact |
| 6.5 | `ffc87b28` | exact | 10 rows / 1,521 B | exact |
| 6.5-post-review-hardening | `fba2c9a3` | exact | 6 rows / 976 B | exact |

All eight entries' historical dirty content records recompute byte-exact, and **all eight
`currentWorktreeMatchPaths` sets are exactly maximal**. No pin set needed adjustment this round,
which is correct: the two content-record files this target edits were already unpinned by every
entry after the previous hardening changed them.

## 4. Z-1 is closed durably

`OpenSpecCorpusGuards` now declares `HarmonizationDesignPath` plus three semantic constants, and
`Task65_PublicAuthoringCompanionRemainsUnchangedAndLifecycleInternalsStayNonPublic` requires all
three. I extracted each constant from guard source and checked it against the live documents rather
than inferring fidelity from a green lane:

| Constant | chars | occurrences in target |
| --- | --- | --- |
| `Task65PostReviewHardeningDecision` | 282 | exactly 1 in the 6.5 ledger block |
| `Task65SecondReviewHardeningDecision` | 194 | exactly 1 in the 6.5 ledger block |
| `Task65DesignHardeningDecision` | 191 | exactly 1 in `design.md` |

These are whole multi-line decisions, not tokens, so the `Contain` assertions cannot be satisfied by
an incidental phrase. Both path constants resolve correctly.

The closure is durable because `Task65_…` does not consult the active freeze at all. Every
documentary control below reddens that focused guard on its own, which is exactly the property Z-1
asked for and exactly what the previous round's P10b lacked.

## 5. Validation reproduced from a clean checkout

- `dotnet build OrcaCore.slnx` with `-warnaserror` in Debug and Release: **0 warnings, 0 errors**
  each.
- `pack-exact-package-feed.ps1 -NoBuild`: **12 packages**.
- Core 350 · Ephemeral 79 · Durable 99 · Acceptance 37 · Hosting 24 · ProviderCertification 96 —
  green, zero skipped.
- PostgreSQL **101**, SQL Server **72**, Integration **11** — green, zero skipped, against real
  Testcontainers storage. (The SQL Server lane again reports about five seconds because xunit
  excludes collection-fixture startup; `Skipped: 0` and the passing count are the evidence.)
- Guard lane: **220 passed / 14 failed / 234 total**, and I enumerated every failure — all fourteen
  are the documented `ExecutableBehaviorExpectedRedGuards.Scenario_…` set and nothing else.
  (`--filter-trait "Disposition=Infrastructure"` still does not apply with this VSTest/xunit-v3
  combination, so the full lane was run and classified by name.)
- Focused Task 6.5, review-provenance, and canonical-synchronization guards: **3/3 green**.
- `openspec validate --all --strict`: **18 passed, 0 failed**. Ledger **23 complete / 11 open / 34
  total**. `git diff --check`: clean.
- Provenance checkpoint (`Fixtures/openspec-provenance-checkpoint.json`, 15,263 bytes /
  `d04b25d5…`, unchanged): **176 rows / 46,211 bytes / `64b8b6df…`**, `pendingCanonicalOperations`
  empty, `semanticApprovalEligible` true. With no unresolved change-to-canonical operations this may
  be reported as semantic approval rather than structural validation only.

## 6. Simulated checkpoint

Staging and committing the target in the disposable worktree produced a commit of **exactly six real
paths** — three added, three modified, no phantoms — tree `6f6c40afd10fc0db0c76d868c1d97fed481667cf`,
and a clean worktree. The guard lane in that committed state is still **220 passed / exactly 14
expected red**. The checkpoint is safe to create as frozen.

## 7. Scope and hunk census — claim 5

Three modified files, three new files, **36 changed lines** across four `-U0` hunks (1 · 1 · 2 by
file for tasks, fixture, guard). I read every hunk. The only deletion in the entire target is the
fixture's `"activeFreeze": null` line, replaced by the freeze object; everything else is additive.

- `tasks.md` +2/−0 — the `**Second-review hardening:**` continuation of the 6.5 block.
- `review-manifest-provenance.json` +12/−1 — `activeFreeze` populated. No pin or entry edits.
- `OpenSpecCorpusGuards.cs` +21/−0 — four constants and eight assertion lines.

The porcelain contains no `src/**`, no canonical `openspec/specs/**`, no companion, no public API
baseline, and no declaration-accounting fixture. The companion is still 53,745 bytes /
`41f6472c…`, `OrcaCore.Core.api.txt` is still the genuinely empty 48-byte baseline, and
`section-07-r-declaration-crosswalk.json` is untouched at 701 active / 1,389 physical declarations.

That last point deserves a word, because leaving the crosswalk untouched while adding four
declarations to a scanned guard file could look like an omission. It is not: probe P7 adds one
`[Fact]` to the same file and the crosswalk guard reddens immediately, so the accounting is live,
and a `const` field is simply not a counted declaration in its model. The fixture is correctly
unchanged.

## 8. Mutation and probe results

| Probe | Mutation | Result |
| --- | --- | --- |
| C0 | untouched target | 220/220, exactly 14 expected red; focused guard green |
| C1 | remove the second-review ledger marker | **RED** on the focused guard's ledger assertion; file restored byte-exact |
| C2 | remove the design ownership paragraph | **RED** on the focused guard's design assertion; file restored byte-exact |
| C3 | weaken one word inside the Y-1 hardening ledger decision | **RED** — the pin is the whole sentence, not a token |
| P2 | relocate the second-review marker out of the 6.5 block, under 6.6 | RED — the block boundary is load-bearing |
| P3 | move the design paragraph verbatim into another document | RED — the assertion is bound to `design.md` |
| P4 | re-wrap the design paragraph, same words | RED — an exact byte pin |
| P5 | perturb a pinned-but-untargeted file | RED on maximal-pin enforcement (durable) |
| P6 | perturb a content-record file without refreshing the anchor | RED on the scoped content record |
| P7 | add one `[Fact]` to the corpus guard | RED on the declaration crosswalk |
| P8 | companion edit plus coherent fixture re-pin (the Y-1 case) | RED — the previous round's fix is still load-bearing |
| P9 | delete both new assertions **and** both documentary claims coherently | RED only on the active-freeze anchor |
| P10 | delete the `design.md` Task 6.5 decision sentences, keep the pinned one | **GREEN** on the focused guard |
| P10b | invert those sentences to claim the lifecycle types are now public | **GREEN** on the focused guard |

Claim 4 is fully verified, in both directions and with byte-exact restoration confirmed by hash
after each probe. P9 is the terminus and is expected: no guard can defend the deletion of its own
assertions, and a change that removes them is a self-declaring weakening in a reviewed diff.

## 9. Observation AA-1 (P3, non-blocking)

The guard now reads `design.md` and pins the final sentence of the Task 6.5 paragraph. The five
sentences immediately preceding it — in the same paragraph, in the same file, stating that the
lifecycle types remain internal and that the twelve-assembly baseline independently rejects a
visibility leak — are pinned by nothing. Probe P10 deletes them and the focused guard stays green;
probe P10b goes further and **inverts** them to assert that those types "are now public
declarations in `OrcaCore.Core`", and the focused guard is still green. Only the active-freeze
anchor objects, and that lapses at archival.

I want to be careful about how much weight this carries, because it is narrower than Z-1 was:

- The decision's substance is durably pinned **elsewhere**. The ledger assertions require
  `deliberately byte-unchanged` and `exhaustive twelve-assembly public API baseline` in the 6.5
  block, so the record does not vanish from the normative trees.
- The **compiled** reality is protected independently and name-independently by the exhaustive
  baseline, which I re-confirmed is live. So P10b would produce a `design.md` that contradicts an
  enforced invariant — a documentation falsehood, not a product regression.

What makes it worth recording is that it is the same shape the last two rounds have been closing,
and the closure is nearly free: the guard already reads this file and already holds a constant for
part of this paragraph, so extending `Task65DesignHardeningDecision` to cover the decision sentences
costs one constant and no new assertion. I am recording it, not blocking on it.

A related maintenance note, not a defect: because the ledger constants embed the six-space
continuation indentation and the design constant embeds its exact line wrapping (P4), any future
editorial re-wrap of either passage will redden the guard until the constant is updated in the same
change. For a provenance pin that is the intended trade — it forces the edit through review — but it
should be a deliberate choice rather than a surprise.

## 10. Checkpoint sequencing

Writing this verdict into the reviewed worktree creates a **seventh** entry against the declared
six, which would redden the active-freeze guard at 7-vs-6. Commit the approved **six-path**
checkpoint first, then add this verdict and its registry entry in the following commit. The 6.5
entry will then carry **three** registered verdicts; registering fewer will fail the "must register
every immutable verdict without overwriting rejection history" assertion.

## 11. Reviewer hygiene

`HEAD` remained at `d501083a1879c241ed784565ad828997f620bb00` throughout. Nothing was staged, and I
created no commit in the reviewed repository, which still shows exactly the six frozen entries at
the moment this verdict was written. The disposable worktree was removed and pruned; its single
throwaway commit is unreachable from every ref.

## Determination

All six claims and all four declared negative controls reproduce independently. Z-1 is closed by a
mechanism that reddens without reference to the active freeze, the constants are byte-faithful
whole decisions rather than tokens, the chain behind the target carries zero drift down to tree
identity with my own rehearsal, validation reproduces in full from a clean checkout, and the
simulated checkpoint is clean at exactly the declared six paths. The single observation recorded
above is non-blocking and does not affect the correctness of this target.

**Verdict:** **APPROVE**
