# Harmonization Task 7.1 round-57 remediation independent review verdict

**Date:** 2026-09-16
**Reviewer:** independent review
**Scope reviewed:** the nineteen-entry final Task 7.1 freeze on base
`99657834684deefa2d22cb6526d714c4566b7ae0`, tree `dc221c3a95ba3543b18b464112e405485c416869`, named
by `harmonize-downstream-capability-specs-task-7-1-round-57-remediation-dirty-manifest-2026-09-15.txt`.
**Authorization scope:** creation of the Task 7.1 documentation/guard checkpoint only. This verdict
does not authorize Task 7.2, reshape task 9.6 membership changes, change archival, or Task 8.0.

## Summary

Every round-57 finding is closed:

- **JJ-1:** the companion record is now ordinal-sorted, 86 rows, 10,655 bytes, and hashes to
  `56b6d27ece05d0ff536d9ca5114ba3e1856f0f3288f4bf5226bab341e36963f2`. The artifact's recipe and the
  record's bytes finally agree, and the guard enforces ordinal order and path uniqueness separately
  from the digest.
- **KK-1:** the scan artifact names the real companion path exactly once, backticked, with no tab or
  truncated spelling, and the guard requires that exact path.
- **LL-1:** each rejected freeze keeps its own byte-exact manifest, request, and `REJECT` verdict in a
  new `rejectedFreezes` registry, and manifest discovery accounts for all three Task 7.1 manifests.

The original Task 7.1 obligations still hold, every declared negative control is red on a durable
check, validation reproduces in full, and the simulated checkpoint matches the freeze's named tree.

Two observations are recorded, neither blocking:

- **MM-1 (P2):** 2 of the companion's 86 rows predate this remediation's final ledger and design
  text, so recomputing the record from the checkpoint tree gives a different digest. The substantive
  zero-finding property stays durably enforced by the live scan; the gap is undisclosed snapshot
  timing, and one of the two rows can never match while the ledger quotes the digest.
- **NN-1 (P3):** a cosmetic formatting defect in guard source.

## Method

All probing ran in two disposable `git worktree` copies created from `99657834`, one for validation
and the simulated checkpoint and one for mutation probes. Each received the nineteen frozen entries
byte-for-byte with porcelain confirmed byte-identical to the frozen manifest. The reviewed worktree
was never modified, `HEAD` never moved, and nothing was staged or committed in it. Both worktrees
were removed and pruned, and the one throwaway commit is contained in no ref.

Each documentation and evidence probe first re-pinned the active freeze's content-record anchor to
the mutated bytes. The fixture holding that anchor is excluded from the content record, so this
silences only the lapsing active-freeze comparison and lets the durable check under test report.

The companion was verified row by row against live files using the published recipe. PowerShell 7
is absent on this host, so the review-manifest `-Check` was reproduced by the Python emulation used
since round 54.

## 1. Freeze anchors reproduced

- **Raw commit-real porcelain:** **1,748 bytes**, SHA-256
  `8ed304b279965f76acdac26e332ac19d1f00a371de3e2388f171bd8bb1dc50ea`, byte-identical to the frozen
  manifest. Nineteen entries (nine modified, ten untracked), none staged, index tree `dc221c3a`
  equal to the `HEAD` tree.
- **Scoped content record:** **18 rows, 2,936 bytes**, SHA-256
  `dcec43f391f67df97566f727d27719d72ef1e1b0db8fee2ce534261bd08585a0`, with the review-provenance
  fixture as its only exclusion. `activeFreeze` names the same values.
- **Chain:** unchanged and zero-drift. `7ce56191` carries tree `930828cc`, identical to my round-55
  rehearsal; twelve archived freezes project exactly; all nine entries keep maximal pins; the
  emulated `-Check` is order-exact.

## 2. JJ-1 closed — ordinal record, recipe and bytes agree

| Check | Result |
|---|---|
| Rows / bytes / CR / terminal LF | 86 / 10,655 / none / present |
| Path order | ordinal, and every row is unique |
| First two rows | `CLAUDE.md`, `README.md` |
| SHA-256 of the file | `56b6d27e…`, equal to the artifact, ledger, and guard constant |
| Artifact line 38 | "Sort rows by ordinal path" — now true of the bytes |

The guard now asserts path uniqueness and `Order(StringComparer.Ordinal)` equality **before** the
digest, so an ordering regression reports as an ordering failure rather than an unexplained hash.
Probe O1 re-sorts the unchanged rows case-insensitively and is red on the ordinal-order assertion.
O2b duplicates a path between two adjacent rows of equal length, so byte and row counts are
unchanged, and is red on the uniqueness assertion. O3 flips one byte and is red on the digest.

## 3. KK-1 closed — exact companion path

The 3,184-byte artifact hashes to the pinned `55bda56e…`, contains zero tab characters, zero
truncated `ask-7-1` spellings, and exactly one occurrence of the backticked real path. The guard now
asserts that exact backticked path independently of the artifact hash. Probe O4 corrupts the path
**and** re-pins the guard's artifact-hash constant to match, so the hash check passes; it is still
red on "the dated scan must name the exact companion record it publishes".

## 4. LL-1 closed — rejected freezes are separately preserved and validated

| Rejected freeze | Request | Manifest | Verdict |
|---|---|---|---|
| Initial | 4,124 B `5d6076f7…` | 926 B `76b82093…` | 13,297 B `b15e501f…` |
| JJ-1 | 4,496 B `4a61f17b…` | 1,282 B `c40b2eb4…` | 12,514 B `19a64192…` |

All six files match byte-exact. The original manifest path again holds exactly the twelve-entry
porcelain I reviewed in round 56, and the new `jj-1-remediation` manifest equals exactly the
fifteen-entry porcelain I reviewed in round 57. Schema 11 records each rejected freeze's
request-named manifest path separately from its preserved manifest path, which truthfully resolves
the JJ-1 request's reference to the then-reused name. Manifest discovery now requires every
`task-*-dirty-manifest-*.txt` to belong to an entry, a rejected freeze, an archived freeze, or the
active freeze.

Every rejected-evidence control is red on the rejected-freeze validation itself, with the active
freeze re-anchored so it cannot mask the result:

- appending one byte to any of the six files is red, each on its own byte-length or LF-framing check;
- relabeling the JJ-1 verdict to `APPROVE` **and** coherently re-pinning its length and hash in the
  fixture is still red on "a rejected freeze cannot be relabeled as approved evidence" (J7);
- removing the JJ-1 rejected freeze from the fixture is red on manifest discovery (J8); and
- pointing its `requestManifestPath` at a name its request never mentions is red on "the rejected
  review request must name the manifest that actually froze its target" (J9).

When entry 7.1 is created, its verdict-discovery glob will match both `REJECT` verdicts and the
approval, and `verdictEvidence` accepts either value, so the evidence commit must register all three.

## 5. Original Task 7.1 obligations retained

- **Zero positive call forms:** my independent scan of the 86-source corpus found zero, and the live
  guard scan is red on a positive `WhenFirst()` call (R1).
- **Guide anchors:** all three guides still carry the exact `#134-future-capability-registry`
  anchor, each resolving to the real §13.4 heading.
- **HH-1 and II-1:** the guard logic is unchanged from the round-56 target, where both were
  measured closed.
- **Durable decisions:** the ledger and design remediation decisions are pinned; weakening either
  (L1, L2) is red.
- **Ledger and accounting:** 25 complete / 9 open / 34 total; declaration accounting 1,391 / 703.
- **Scope:** no `src/**` or canonical `openspec/specs/**` path is in the target.

## 6. Observation MM-1 (P2, non-blocking) — two companion rows predate the final ledger and design text

Reconstructing each row from the frozen files, as the request asks, reproduces **84 of 86**. The two
that do not are this change's own planning files:

| Path | Companion row | Frozen file | Relationship |
|---|---|---|---|
| `…/harmonize-downstream-capability-specs/design.md` | 25,348 · `090e7364…` | 25,723 · `5c149c65…` | companion equals the round-56/57 target bytes |
| `…/harmonize-downstream-capability-specs/tasks.md` | 31,799 · `15c284f9…` | 32,198 · `193280d5…` | companion equals the round-56/57 target bytes |

This remediation added its review-remediation paragraphs to both files after the companion was
generated. Recomputing the documented record from the checkpoint tree therefore yields
`a929a7568e20dcbf26e7093e68576e09e00c37dcda9a3a7f43f5135661d31434`, not `56b6d27e…`.

The two rows differ in kind:

- **`tasks.md` cannot converge.** The ledger quotes the companion's digest, so any record that
  matches the ledger changes the digest the ledger quotes.
- **`design.md` could have matched.** It does not quote the digest; it simply changed after the
  record was generated.

This does not block, for three reasons. No text in the target claims the companion equals the
checkpoint tree. Every row is honest: the two rows are exactly the bytes of the reviewed round-56
and round-57 targets. And the property the record evidences — zero positive call forms — is enforced
on the committed files by the live guard scan, which is green on the final ledger and design.

It is P2 rather than P3 because it is the same reproduction trap that consumed two review rounds.
The next person to recompute from the checkpoint will get `a929a756…` and no explanation, and those
two companion rows describe file versions that will never exist in Git history. The cheapest close
is a sentence in the active design or ledger at the next touch, stating that the companion
snapshots the corpus before the Task 7.1 remediation text and naming the two rows. The immutable
artifact needs no change. If a fully reproducible record is preferred instead, stop quoting its
digest in the ledger, which the guard constant already pins, and regenerate it last.

## 7. Observation NN-1 (P3) — two statements on one line in guard source

`OpenSpecCorpusGuards.cs:1652` ends one `design.Should().Contain(…)` call and begins the next on the
same physical line, and the following statement is preceded by a doubled blank line. It compiles
cleanly and `git diff --check` does not detect it. CI runs no format verification, and IDE0055 is
not enforced in build. Reflow it at the next touch of this file.

## 8. Mutation and probe results

| Probe | Mutation | Result on the owning check |
|---|---|---|
| O1 | Companion rows re-sorted case-insensitively, content unchanged | **red**, ordinal-order assertion |
| O2 | Duplicate path that changes the byte count | **red**, byte-count assertion |
| O2b | Duplicate path between equal-length adjacent rows, counts unchanged | **red**, uniqueness assertion |
| O3 | One companion byte flipped | **red**, digest |
| O4 | Companion path corrupted, artifact-hash constant coherently re-pinned (source) | **red**, exact-path assertion |
| J × 6 | One byte appended to each rejected request, manifest, and verdict | **red**, rejected-freeze validation |
| J7 | Rejected verdict relabeled `APPROVE` with coherent fixture re-pin | **red**, relabel check |
| J8 | JJ-1 rejected freeze removed from the fixture | **red**, manifest discovery |
| J9 | `requestManifestPath` pointed at a name the request never mentions | **red**, request-naming check |
| L1 / L2 | Ledger / design remediation decision weakened | **red**, Task 7.1 guard |
| R1 | Positive `WhenFirst()` call added to an active guide | **red**, live corpus scan |

The baseline and final runs were green on both targeted tests, and restoration was verified byte-exact
against the frozen target.

## 9. Validation reproduced from a clean checkout

| Gate | Result |
|---|---|
| Debug build, non-incremental, warnings as errors | 0 warnings, 0 errors |
| Release build, non-incremental, warnings as errors | 0 warnings, 0 errors |
| Exact package feed | 12 packages |
| Core / Ephemeral / Durable / Acceptance / Hosting / Certification | 350 / 79 / 99 / 37 / 24 / 96 |
| PostgreSQL / SQL Server / Integration | 101 / 72 / 11 |
| Full guard lane | 222 passed, 14 failed, 236 total |
| Classification of the 14 failures | all `ExecutableBehaviorExpectedRedGuards.Scenario_…`, zero others |
| OpenSpec `validate --all --strict` | 18 / 18 |
| Harmonization ledger | 25 complete, 9 open, 34 total |
| Review-manifest current matches | order-exact for all nine entries (emulated; PowerShell 7 absent) |
| `git diff --check` / changes under `src/**` or `openspec/specs/**` | clean / zero |
| Independent positive-call scan | 0 findings across 86 sources |
| Porcelain after validation | byte-identical to the manifest |

## 10. Simulated checkpoint and sequencing

Committing the nineteen frozen entries on `99657834` in a disposable worktree produced exactly
nineteen paths, ten added and nine modified. The resulting tree is
`bd519b0dd64e9dc6705b867faa0c63405351d04a`, which equals the tree named in the freeze. The worktree
was clean afterwards, and the full guard lane in that committed state is 222 passed, 14 failed, 236
total, with all 14 failures the documented expected-red scenarios.

Writing this verdict creates a **twentieth** entry against the declared nineteen, which reddens the
active-freeze guard. Commit the approved **nineteen-path** checkpoint first, then add this verdict
in the following evidence commit, registering all three Task 7.1 verdicts in the new entry.

## 11. Reviewer hygiene

`HEAD` remained at `99657834684deefa2d22cb6526d714c4566b7ae0` throughout, nothing was staged, and I
created no commit in the reviewed repository. It still showed exactly the nineteen frozen entries
when this verdict was written. Both disposable worktrees were removed and pruned.

## Determination

Three rounds converge on a clean result. The companion record's order, bytes, digest, and published
recipe agree. The artifact names its companion exactly. Both rejected freezes survive as separately
preserved, byte-validated evidence that cannot be tampered with or relabeled. Each of those
properties is enforced by a guard assertion proven red by a targeted mutation, while the original
Task 7.1 obligations and validation reproduce in full.

MM-1 is a real reproduction hazard worth one sentence of disclosure, but no statement in the target
is false and the substantive property is durably enforced. NN-1 is cosmetic.

**Verdict:** **APPROVE**
