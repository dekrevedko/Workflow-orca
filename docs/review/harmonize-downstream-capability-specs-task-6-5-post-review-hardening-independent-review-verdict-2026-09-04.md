# Harmonization Task 6.5 post-review-hardening independent review verdict

**Date:** 2026-09-04
**Reviewer:** independent review
**Scope reviewed:** the seven-entry Task 6.5 post-review-hardening freeze on base
`4f3b71a85372a041c4b6ee90a0096856922f41c9`, tree `7a41327d41a15083bf602d5b7d0af951a57e637f`,
named by
`harmonize-downstream-capability-specs-task-6-5-post-review-hardening-dirty-manifest-2026-09-03.txt`.
**Authorization scope:** creation of the Task 6.5 post-review-hardening checkpoint only. This
verdict does not authorize Task 6.6, change archival, or reshape Task 8.0.

## Summary

The target closes review finding Y-1 from the immutable Task 6.5 verdict
(10,979 bytes, SHA-256 `6616e9ba4bd764ff8b2c20da06ce509748d1a310907352b281e75f340d737928`) by
promoting the reviewed companion SHA-256 to a named guard-source constant and requiring the mutable
public-contract fixture to reproduce it before the companion bytes are checked. Every declared
claim, negative control, and validation figure reproduced independently. The exact mutation that
was green in the Task 6.5 review is now red on the Task 6.5 guard itself.

## Method

All probing ran in a disposable `git worktree` created from `4f3b71a8` with the seven frozen
entries copied in byte-for-byte; the reviewed worktree was never modified, `HEAD` never moved, and
nothing was staged or committed in it. The worktree porcelain was confirmed byte-identical to the
reviewed worktree's before any probe. The worktree was removed and pruned afterwards and its one
throwaway commit is unreachable from every ref.

## 1. Freeze anchors reproduced

- Raw commit-real porcelain (`git status --porcelain=v1 --untracked-files=all`): **636 bytes**,
  SHA-256 `a597be2ef1825755ebe292dd97a2f5ce3777bd9d616b0249e7d8eb9a3dd99450`.
- Published dirty manifest is **byte-identical** to that live porcelain, not merely equal in hash.
- Scoped content record — ordinal-sorted `status\tpath\tbytes\tsha256` rows, LF-joined with one
  final LF, UTF-8 without BOM, excluding only the self-referential provenance fixture:
  **6 rows, 976 bytes**, SHA-256
  `68e7f3dde750d6c9f8376358dd054f5f459dedde04e7a2b75e2e4d6d28ec9620`.

Both match the declared anchors exactly. Seven entries: four modified, three untracked, zero staged.

## 2. The approved Task 6.5 chain carries zero drift

- Checkpoint `ffc87b282f89e5d99ffa508c237d6968e03bddd4` (tree `6257286f`) contains **exactly the
  eleven real paths** of the approved target — three added, eight modified, no phantoms.
- Its recovered dirty manifest is **897 bytes / `fb8cf0ee…`**, byte-identical to the anchor
  approved in the Task 6.5 review.
- Re-projecting the scoped content record from that commit's blobs yields **10 rows / 1,521 bytes /
  `d73fc37f…`** — again byte-identical to the approved anchor.
- The Task 6.5 verdict landed byte-exact at **10,979 bytes / `6616e9ba…`** in evidence commit
  `172002f82f45a252fe2869b854695edcf3d2f9f1`, and is registered on the 6.5 entry with verdict
  `APPROVE` and that same digest.
- Activation `4f3b71a85372a041c4b6ee90a0096856922f41c9` touches only the provenance fixture and is
  the declared base of this freeze.

## 3. Provenance fixture integrity

Schema 10; seven archived freezes; eight entries. Every archived freeze was recomputed from its own
checkpoint commit rather than accepted:

| Archived freeze | checkpoint | manifest | projected record | tree |
| --- | --- | --- | --- | --- |
| 6.1-remediation | `9f4fa0b5` | exact | 15 rows / 2,336 B | exact |
| 6.1-post-review-hardening | `87da8c2b` | exact | 13 rows / 2,098 B | exact |
| 6.2 | `7abf95e3` | exact | 14 rows / 2,064 B | exact |
| 6.3 | `609e9c45` | exact | 15 rows / 2,146 B | exact |
| 6.4-remediation | `5e38de27` | exact | 13 rows / 2,064 B | exact |
| 6.4-post-review-hardening | `737f2fd4` | exact | 8 rows / 1,249 B | exact |
| 6.5 | `ffc87b28` | exact | 10 rows / 1,521 B | exact |

All eight entries' historical dirty content records recompute byte-exact, and **all eight
`currentWorktreeMatchPaths` sets are exactly maximal** — every recorded row whose live bytes still
match is pinned, and nothing else is.

The pin refresh this target performs is honest: the 6.5 entry's set drops from ten to seven,
removing precisely `design.md`, `tasks.md`, and `OpenSpecCorpusGuards.cs` — the three of this
target's four modified files that are content-record members. The fourth, the provenance fixture
itself, is the excluded self-referential path and was never pinnable. Maximality is enforced in
**both** directions, which I verified rather than assumed (P8a, P8b below).

## 4. Y-1 is closed, and the proof is the round-50 probe itself

`OpenSpecCorpusGuards` now declares

```
private const string PublicAuthoringCompanionSha256 =
    "41f6472c2774363d2ab922c608922e787ec241333e1d1c0b76b0c6d529ab8ec3";
```

and `Task65_PublicAuthoringCompanionRemainsUnchangedAndLifecycleInternalsStayNonPublic` asserts, in
this order, that `v1-public-contract.json` reproduces that constant, then that the companion bytes
hash to it. Claim 2's ordering is real: the fixture assertion is what fires first.

Probe C1 replays the exact mutation that was **green** in the Task 6.5 review — append
`public sealed class AuthoringSessionScopeHandle { }` to the companion and coherently re-pin
`companionSha256` in the fixture. It is now **red**, and the failure is the new assertion verbatim:

> Expected `contract.CompanionSha256.ToLowerInvariant()` to be the same string because the mutable
> contract fixture must reproduce the source-owned reviewed companion pin

That name is deliberately outside the six-name forbidden list, so the redness comes from the
source-owned pin and nothing else. The pre-existing mutable-vs-mutable companion check
(`CompanionBaseline_HasExactReviewedNamespaceArityAndSignatures`) stays green under C1 — which is
exactly why Y-1 existed and exactly what the constant now covers.

## 5. The remaining claims

- **Claim 4, byte-invariance.** The companion is unchanged at **53,745 bytes /
  `41f6472c2774363d2ab922c608922e787ec241333e1d1c0b76b0c6d529ab8ec3`**, matching the new constant.
  All twelve approved API baselines are unmodified, `OrcaCore.Core.api.txt` is still the genuinely
  empty 48-byte baseline, and the porcelain contains no `src/**`, `openspec/specs/**`, companion,
  or baseline path.
- **Claim 4, independence.** Verified, not assumed. Probe P12 adds a public type with a name on no
  forbidden list to `OrcaCore.Core`; the pre-existing exhaustive baseline guard reddens on its own.
  The compiled-surface protection is therefore both independent of the companion pin and
  name-independent. Probe P1 shows the reverse direction: a forbidden-named companion declaration
  stays red even under a fully coherent three-way re-pin, because `NotContainAny` is name-based and
  digest-independent.
- **Claim 5, registration.** The Task 6.5 freeze, verdict, evidence commit, and activation all
  remain registered exactly, as recomputed in sections 2 and 3.

## 6. Validation reproduced from a clean checkout

Reproduced in the disposable worktree, not read from the record:

- `dotnet build OrcaCore.slnx -c Debug -warnaserror` and `-c Release -warnaserror`: **0 warnings,
  0 errors** each.
- `pack-exact-package-feed.ps1 -NoBuild`: **12 packages**.
- Core 350 · Ephemeral 79 · Durable 99 · Acceptance 37 · Hosting 24 · ProviderCertification 96 —
  all green, zero skipped.
- PostgreSQL **101**, SQL Server **72**, Integration **11** — all green, zero skipped, against real
  Testcontainers storage.
- Guard lane: **220 passed / 14 failed / 234 total**, and I enumerated every failure: all fourteen
  are `ExecutableBehaviorExpectedRedGuards.Scenario_…`, the documented intentional set. Nothing
  else is red. (`--filter-trait "Disposition=Infrastructure"` still does not apply with this
  VSTest/xunit-v3 combination, so the full lane was run and the failures classified by name.)
- `openspec validate --all --strict`: **18 passed, 0 failed**.
- Task ledger: **23 complete / 11 open / 34 total**. `git diff --check`: clean.
- `CanonicalSynchronizationGate_EnumeratesCapabilitiesDeltasAndRequirementOwners`: green.
  Provenance checkpoint (`Fixtures/openspec-provenance-checkpoint.json`, 15,263 bytes /
  `d04b25d5…`): **176 rows / 46,211 bytes / `64b8b6df…`**, `pendingCanonicalOperations` **empty**,
  `semanticApprovalEligible` **true**. With no unresolved change-to-canonical operations, this may
  be reported as semantic approval rather than structural validation only.

A note on one figure that could mislead a later reader: the SQL Server lane reports about five
seconds because xunit excludes collection-fixture startup from the reported duration. The real
containers do start; `Skipped: 0` and the passing count are the evidence, not the clock.

## 7. Simulated checkpoint

Staging the target and committing it in the disposable worktree produced a commit of **exactly
seven real paths** — three added, four modified, no phantom entries — and a clean worktree. The
guard lane in that committed state is still **220 passed / exactly 14 expected red**, because the
active-freeze guard's clean branch verifies `HEAD^` equals the declared base, the committed paths
equal the manifest paths, and the committed blob content record equals the scoped anchor. The
checkpoint is safe to create as frozen.

## 8. Scope and hunk census

Four modified files, three new files, **30 changed lines** across eight `-U0` hunks (2 · 1 · 3 · 3
by file for design, tasks, fixture, guard). I read every hunk. Additions and deletions:

- `design.md` +3/−1 — the single deleted line is `types and signatures.`, re-wrapped into the same
  sentence. No content is lost; I checked this specifically because a silent deletion inside a
  plausible-looking hunk was the blocking finding of the Task 6.4 review.
- `tasks.md` +3/−0 — purely additive `**Post-review hardening:**` continuation of the 6.5 block.
- `review-manifest-provenance.json` +12/−4 — `activeFreeze` populated, three pin paths removed.
- `OpenSpecCorpusGuards.cs` +6/−1 — the constant, the fixture assertion, and the swap of the
  companion comparison's expected value.

Probe P10 corroborates the census independently: reverting exactly the lines I read restores
`design.md`, `tasks.md`, **and** `OpenSpecCorpusGuards.cs` to bytes identical to their Task 6.5
committed state — which the maximal-pin invariant then reports as three newly-matching paths. There
are no changes in those files beyond the ones enumerated here.

## 9. Mutation and probe results

| Probe | Mutation | Result |
| --- | --- | --- |
| C0 | untouched target | 220/220, exactly 14 expected red |
| C1 | differently-named public companion declaration + coherent fixture re-pin (**green in round 50**) | **RED** on the source-owned constant |
| C2 | fixture digest altered alone | RED (Task 6.5 guard *and* the pre-existing companion baseline) |
| C3 | source-owned constant altered alone | RED |
| C3b | constant and fixture altered coherently, companion untouched | RED |
| C4a | immutable Task 6.5 verdict tampered | RED — *durable* byte-hash: "immutable review evidence must remain byte-exact" |
| C4b | verdict tampered **and** its digest coherently re-pinned | RED, but only on the active-freeze manifest comparison |
| C4c | immutable Task 6.5 review request tampered | RED |
| P1 | forbidden-named public declaration + fully coherent three-way re-pin | RED on the name list — digest-independent |
| P2 | companion + fixture + **guard constant** all re-pinned coherently | RED only on the active-freeze anchor |
| P5 | pinned-but-untargeted file perturbed | RED on maximal-pin enforcement (durable) |
| P6 | content-record file perturbed without refreshing the anchor | RED on the scoped content record |
| P8a | a legitimately removed pin re-added | RED — pins must not exceed the matching set |
| P8b | a still-matching pin dropped | RED — pins must not fall short of it |
| P10 | byte-exact revert of the whole Y-1 hardening | RED on maximal-pin enforcement (durable) |
| P10b | same revert, deliberately not byte-exact | RED only on the active-freeze anchor |
| P11 | one extra `[Fact]` added to the corpus guard | RED on the declaration crosswalk |
| P12 | new public type with a non-forbidden name leaked from `OrcaCore.Core` | RED on the exhaustive twelve-assembly baseline |

All four declared negative controls reproduce. C4a is worth singling out: the review request's
fourth control is satisfied by a genuinely durable mechanism — `ValidateReviewVerdictEvidence`
re-hashes every registered verdict on every run, independent of any active freeze — not merely by
the freeze anchor.

## 10. Observation Z-1 (P3, non-blocking)

The hardening's own claim is not self-witnessing. `Task65_…` pins three exact strings from the 6.5
ledger block (`**Completed:**`, `deliberately byte-unchanged`, `exhaustive twelve-assembly public
API baseline`) so the decision's substance cannot be silently dropped. The new
`**Post-review hardening:**` sentence and the new `design.md` paragraph carry an equally
substantive claim — that the digest is owned by guard source — and neither is pinned by anything.

P10b is the demonstration: revert the constant, restore the mutable comparison, delete both doc
blocks, and leave the files not byte-identical to their historical rows, and the only red is the
active-freeze anchor, which lapses once this freeze is archived. The byte-exact variant P10 is
caught durably, but that is a happy consequence of exact reversion rather than a guarantee.

I want to be precise about severity, because this is one level removed from Y-1 rather than a
repeat of it. P2 shows the digest chain now terminates in reviewed guard source, and that is the
correct place for it to terminate — there is no further pin to add, and every pin scheme bottoms
out in source somebody has to read. Changing a named constant is a self-declaring weakening in a
reviewed diff; changing fixture data reads as routine metadata refresh. That asymmetry is exactly
what the fix bought, and it is real. What remains is narrower: the *documentation* of the fix has
no regression pin, so a later reverting change would not have to disclose itself in the ledger.

The proportionate closure is the one this change series already used for Task 6.4 finding W-1 —
add the hardening phrase to the strings `Task65_…` requires of the 6.5 ledger block. That costs one
assertion and makes the claim survive archival. I am recording it, not blocking on it.

## 11. Checkpoint sequencing

Writing this verdict into the reviewed worktree creates an **eighth** entry against the declared
seven, which would redden the active-freeze guard at 8-vs-7. Commit the approved **seven-path**
checkpoint first, then add this verdict and its registry entry in the following commit.

One registration detail: the provenance guard discovers verdicts for entry `6.5` with the glob
`harmonize-downstream-capability-specs-task-6-5*verdict-*.md`, which this file matches. The 6.5
entry must therefore register **both** the Task 6.5 verdict and this one, the same way the 6.4
entry came to hold three. Registering only one will fail the "must register every immutable verdict
without overwriting rejection history" assertion.

## 12. Reviewer hygiene

`HEAD` remained at `4f3b71a85372a041c4b6ee90a0096856922f41c9` throughout. Nothing was staged, and I
created no commit in the reviewed repository. The reviewed worktree still shows exactly the seven
frozen entries at the moment this verdict was written. The disposable worktree was removed and
pruned; its single throwaway commit is unreachable from every ref.

## Determination

Every claim in the review request reproduces independently. Y-1 is closed by a mechanism I verified
by replaying the precise mutation that defeated its predecessor. The chain behind it carries zero
drift, the pin refresh is exactly maximal in both directions, validation reproduces in full from a
clean checkout, and the simulated checkpoint is clean at exactly the declared seven paths. The one
observation recorded above is non-blocking and does not affect the correctness of this target.

**Verdict:** **APPROVE**
