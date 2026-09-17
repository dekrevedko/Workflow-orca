# Harmonization Task 7.1 JJ-1 remediation independent review verdict

**Date:** 2026-09-15
**Reviewer:** independent review
**Scope reviewed:** the fifteen-entry remediated Task 7.1 freeze on base
`99657834684deefa2d22cb6526d714c4566b7ae0`, tree `dc221c3a95ba3543b18b464112e405485c416869`, named
by `harmonize-downstream-capability-specs-task-7-1-dirty-manifest-2026-09-15.txt`.
**Authorization scope:** none. This verdict rejects the remediated target as frozen.
**Relationship to the earlier verdict:** the 2026-09-15 `REJECT` verdict
(13,297 bytes, `b15e501f…`) is preserved byte-exact in this target and is unaffected by this one.

## Summary

The companion record settles JJ-1 completely, and the answer is that **both digests are correct for
different sort orders**. All 86 rendered rows are byte-identical to my independent recomputation.
The population, normalization, lengths, and per-file hashes all agree. The rows differ only in
**order**:

- the checked-in TSV is ordered **case-insensitively** and hashes to `74fc7697…`;
- the same rows in **ordinal** order — which is what the artifact's recipe says to use — hash to
  `56b6d27e…`, the value my first verdict reported.

So my earlier finding stands in substance, and my proposed digest was right for the documented
recipe. The remediation request's claim that "a fresh reproduction using the artifact's stated
ordinal-path recipe yields the original `74fc7697…`" is contradicted by the companion it ships.

Two blocking defects remain, both one-line fixes inside the same hash-pinned artifact:

- **JJ-1 (P2, unresolved):** the stated recipe still says ordinal while the record is
  case-insensitive, so following the artifact still produces a different digest.
- **KK-1 (P2, new):** the artifact's cross-reference to the companion is corrupted. It contains a
  literal tab and a truncated filename, and the artifact never names the real path.

Everything else in the target re-verifies, including the original Task 7.1 obligations, HH-1, II-1,
the freeze anchors, and full validation.

## Method

All probing ran in two disposable `git worktree` copies created from `99657834`, one for validation
and the simulated checkpoint and one for mutation probes. Each received the fifteen frozen entries
byte-for-byte with porcelain confirmed byte-identical to the frozen manifest. The reviewed worktree
was never modified, `HEAD` never moved, and nothing was staged or committed in it. Both worktrees
were removed and pruned, and the one throwaway commit is contained in no ref.

The record comparison was recomputed from the artifact's published recipe against live files, then
compared row by row with the checked-in companion.

## 1. Freeze anchors reproduced

- **Raw commit-real porcelain:** **1,282 bytes**, SHA-256
  `c40b2eb41e1e78752f91e649f2a2ce1db82900b873c9a5f4c586b02b19a458f0`, byte-identical to the frozen
  manifest. Fifteen entries (nine modified, six untracked), none staged, index tree `dc221c3a`
  equal to the `HEAD` tree.
- **Scoped content record:** **14 rows, 2,190 bytes**, SHA-256
  `8add06235e4e3c628e0dad81150a1c6bb618745340c3c692d6f9eb65d2d4ddfc`.
- **Capability inventory:** 16 directories, 1,321 bytes,
  `5e8a9725979d702ff2f639fef587828958c92533139602ab13f1401d6df7ebd5`, recomputed from the
  filesystem.
- **Preserved evidence:** the immutable `REJECT` verdict is present at 13,297 bytes / `b15e501f…`,
  and the original request is unchanged at 4,124 bytes / `5d6076f7…`.
- **Chain:** unchanged from the previous round and still zero-drift — `7ce56191` carries tree
  `930828cc`, identical to my round-55 rehearsal; twelve archived freezes project exactly; all nine
  entries keep maximal pins; the emulated `-Check` is order-exact.

## 2. JJ-1 (P2, unresolved) — the digest disagreement is sort order, and the recipe is the wrong half

The companion made this fully diagnosable, exactly as the request intended. Working from the
published recipe against live files:

| Check | Result |
|---|---|
| Path set | identical, 86 paths |
| Rendered rows differing in content | **0 of 86** |
| TSV versus my case-insensitive rebuild | **byte-identical**, 10,655 bytes |
| Digest of the case-insensitive order | `74fc7697…` (the published value) |
| Digest of the ordinal order | `56b6d27e…` (my earlier value) |

The first divergence is the second row, which is what the request asked me to report:

- ordinal order: `README.md` · 1,172 · `bfd35c92…`
- companion order: `docs/active-implementation-index.md` · 5,383 · `c2142a8f…`

Ordinal comparison places uppercase `R` (U+0052) before lowercase `d` (U+0064), so `README.md` sorts
second. Case-insensitive comparison places it last. That single ordering choice is the entire
disagreement; no file content, length, or hash differs.

The artifact still states, on line 38: "Sort rows by ordinal path, join with LF, retain one final
LF, and hash the resulting UTF-8 bytes." That instruction does not produce the published digest.

Probe T6 makes the inconsistency executable: re-sorting the companion into the documented ordinal
order, with no other change, turns the focused Task 7.1 guard **red**, and the guard reports the
actual value as `56b6d27e…`. The repository therefore rejects the record its own artifact tells a
reader to construct.

For what it is worth, ordinal is also the house convention: the guard's own corpus enumeration ends
in `.Order(StringComparer.Ordinal)`, its provenance and findings records sort ordinal, and every
freeze content record in this series is ordinal-sorted.

**Remedy — either half, so long as text and bytes agree.**

1. Re-sort the companion into ordinal order and publish `56b6d27e…` in the artifact and the guard
   constant. This matches the existing wording and the repository convention.
2. Or keep the companion and its `74fc7697…` digest and correct the artifact to state
   case-insensitive ordering explicitly, naming the comparer rather than saying "ordinal".

## 3. KK-1 (P2, new) — the companion cross-reference in the artifact is corrupted

The remediation's stated purpose is that the artifact names the companion so the digest can be
reproduced. It does not. Line 40 of the artifact reads, in raw bytes:

```text
The exact rendered record is preserved at
<TAB>ask-7-1-active-guide-positive-call-source-record-2026-09-15.tsv so the aggregate digest can
```

The line begins with a literal tab (0x09) followed by `ask-7-1-…`, so both the directory prefix and
the leading `t` of the filename are gone. It looks like a `\t` escape was interpreted while the path
was written. The artifact contains the real path
`openspec/changes/harmonize-downstream-capability-specs/artifacts/task-7-1-active-guide-positive-call-source-record-2026-09-15.tsv`
**zero** times, and it is the only tab in the file.

This blocks for the same reason JJ-1 does: the artifact is dated, immutable, and pinned by content
hash at `452bbc7b…`, so committing freezes a pointer that resolves to nothing, in the one sentence
the remediation exists to add. Fixing it also changes the artifact hash, so it pairs naturally with
whichever JJ-1 remedy is chosen.

## 4. Observation LL-1 (P3) — the remediated target reuses a manifest path cited by an immutable verdict

The remediated freeze reuses `…-task-7-1-dirty-manifest-2026-09-15.txt`. The immutable `REJECT`
verdict cites that exact path for the twelve-entry target at 926 bytes / `76b82093…`, and the file
now holds the fifteen-entry target at 1,282 bytes / `c40b2eb4…`. The request discloses this, and no
frozen record was edited, so it is not a defect. It does leave a dated verdict pointing at a path
whose content has changed, which is the kind of provenance ambiguity this series otherwise avoids.
The request file itself already uses a distinct name, so
`…-task-7-1-jj-1-remediation-dirty-manifest-2026-09-15.txt` would keep the naming consistent and
leave the rejected target's anchor resolvable.

## 5. Original Task 7.1 obligations re-verified

- **Zero positive call forms.** My independent scan of all 86 sources again found zero matches, and
  the corpus bytes are unchanged from the previously reviewed target.
- **The guard rescans the live corpus.** A positive `WhenFirst()` call in an active guide is red
  (R1).
- **Guides and anchors.** All three guides keep the exact `#134-future-capability-registry` anchor,
  and each link resolves to the real heading.
- **HH-1 remains closed.** A nested unregistered provenance artifact is red on the canonical gate
  (R3).
- **II-1 remains closed.** A later §13.4 subsection promising `WaitLong` is red on the Task 6.6
  guard (R2).
- **Ledger and accounting.** 25 complete / 9 open / 34 total, with declaration accounting at
  1,391 / 703 pinned in three places.
- **Companion immutability controls.** A one-byte mutation (T1), a removed row (T2), CRLF
  conversion (T3), a stripped final LF (T4), deletion (T5), and artifact drift (T7) are each red on
  the focused guard. The byte count, row count, CR absence, and terminal LF are all separately
  pinned.

The guard checks the companion against its own pinned digest only; it never recomputes the rows from
the live corpus. That is consistent with the request calling the companion historical evidence, and
the live zero-finding scan is the durable part.

## 6. Validation reproduced from a clean checkout

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
| `git diff --check` / changes under `src/**` | clean / zero |
| Independent positive-call scan | 0 findings across 86 sources |
| Porcelain after validation | byte-identical to the manifest |

## 7. Simulated checkpoint

Committing the fifteen frozen entries on `99657834` in a disposable worktree produced exactly fifteen
paths, six added and nine modified. The resulting tree is
`7146b0fb4fb55d62db798a8215be2b7de9fafbb1`, which equals the tree named in the freeze. The worktree
was clean afterwards, and the full guard lane in that committed state is 222 passed, 14 failed, 236
total, with all 14 failures the documented expected-red scenarios.

Recorded for completeness. It does not authorize a checkpoint.

## 8. Sequencing for the next remediation

Writing this verdict adds a sixteenth entry. Carry it into the next freeze so its manifest names this
file, or record it under your evidence-commit protocol first. Its name matches the Task 7.1 discovery
glob `harmonize-downstream-capability-specs-task-7-1*verdict-*.md`, so whichever entry carries Task
7.1 must register both Task 7.1 verdicts.

## 9. Reviewer hygiene

`HEAD` remained at `99657834684deefa2d22cb6526d714c4566b7ae0` throughout, nothing was staged, and I
created no commit in the reviewed repository. It still showed exactly the fifteen frozen entries when
this verdict was written. Both disposable worktrees were removed and pruned.

## Determination

The remediation did the most useful possible thing: shipping the rendered record turned an
unreproducible aggregate into a precisely located disagreement. That disagreement is now settled.
Every row is correct, and the two digests differ only because the record is ordered
case-insensitively while its published recipe says ordinal.

It is rejected because the frozen bytes still contain the defect JJ-1 named — an immutable,
hash-pinned artifact whose stated reproduction recipe does not produce its published digest — and
because the cross-reference added to resolve that dispute is corrupted and names no file. Neither is
an engineering fault in the guard, the scan, or the corpus, all of which verify. Both are one-line
corrections in the artifact.

Make the recipe and the record agree, repair the companion path, refreeze under a distinct manifest
name, and re-request. No other change is required by this verdict.

**Verdict:** **REJECT**
