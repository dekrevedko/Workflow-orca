# Harmonization Task 6.2 independent review verdict

**Date:** 2026-08-31
**Reviewer:** independent review session (cold verification, no authoring involvement)
**Target:** the dirty worktree named by
`docs/review/harmonize-downstream-capability-specs-task-6-2-dirty-manifest-2026-08-31.txt`
**Base:** `532940a3865b8eec85c315dca9da09b874cb4173`
**Requested authorization:** create only the Task 6.2 checkpoint commit

## Scope

This verdict covers the seven Task 6.2 claims, the freeze anchors, the prior checkpoint chain, the
schema 9 provenance registry, and the declared validation packet. It does not complete Tasks
6.3-8.3, archive the harmonization change, or authorize reshape Task 8.0.

## Method

Every claim was recomputed rather than accepted. The reviewed worktree was never modified, staged,
or committed. All builds, suites, mutations, and the simulated checkpoint ran in a disposable
`git worktree` created at the declared base with the fifteen target files copied in; that worktree
was removed and pruned before this verdict was written. No probe commit is reachable from any ref.

## 1. Freeze anchors

Reproduced three times - live target before validation, independent clean checkout, live target
after validation. All three agree.

- raw porcelain (`--untracked-files=all`): **1,157 bytes**,
  `33c554e370cbff021d3f06b0a48cd4279eb99e16967e4521f63d779536c3057a` - matches the declared value.
- `...task-6-2-dirty-manifest-2026-08-31.txt` is **byte-identical** to live porcelain (`cmp`
  against `git status` output: no difference). There are no phantom rows.
- scoped content record over the fourteen non-fixture paths: **2,064 bytes**,
  `56b99de45b1570839a66c6bcfe8bc0da0e0fbc38ed09fecf5ae1731bc64ca261` - matches the declared value.
- entries: **15** - 11 modified, 4 untracked, 0 staged.

The unscoped fifteen-row record is 2,218 bytes /
`707e8d7b3ea62a9e9d050c8b8a8bf1e756b4911c303dedcaa5e409235e1d60a6`; it is claimed nowhere and is
recorded only so the scoping is unambiguous.

## 2. Prior checkpoint chain

- `9f4fa0b5` (parent `603dffc0`) contains **exactly the sixteen paths** of the Task 6.1 remediation
  target I approved on 2026-08-23, verified by set difference.
- `87da8c2b` (parent `9f4fa0b5`, tree `a91c7f4a`) contains fourteen paths and carries my
  remediation verdict **byte-exact** at
  `fa09be9097deb3f32da1fd6079ef7da9acb1a1ca1ad7927cb17bf378639e5d3e`. Thirteen of its paths are
  post-review hardening that was owner-authorized, not independently reviewed; the request says so
  plainly.
- `532940a3` (parent `87da8c2b`) is the two-path evidence transition and its tree is the declared
  base tree `07e3845a`.

Both archived freezes and all four registry entries were recomputed from first principles:

| record | manifest | path-sorted | parent/tree | committed paths | scoped or blob record | pins |
|--------|----------|-------------|-------------|-----------------|-----------------------|------|
| archived `6.1-remediation` | 16 / 1,361 B exact | exact | exact | exact | 2,336 B / `f2baefb0` exact | n/a |
| archived `6.1-post-review-hardening` | 14 / 1,262 B exact | exact | exact | exact | 2,098 B / `878ef0e2` exact | n/a |
| entry 5.1 | exact | exact | - | - | `741cfd6b` exact | 9, exact + ordered |
| entry 5.2 | exact | exact | - | - | n/a | 4, exact + ordered |
| entry 5.3 | exact | exact | - | - | `dac6b842` exact | 3, exact + ordered |
| entry 6.1 | exact | exact | - | - | `e6863f9c` exact | 4, exact + ordered |

Task 6.1's pins fell from eight to four because Task 6.2 edits four of the previously pinned files;
the reduction is exactly forced by the maximality rule and every remaining pin verifies.

Two findings I raised on 2026-08-23 are closed in this base and I verified the fixes directly:
**S-1** - both leased retry fixtures now call
`AwaitSignalBeforeWorkflowCompletionAsync(gate.SecondStarted.Task, running, ...)`, and the guard
now requires that exact form while forbidding both the bare unbounded await and the old
post-completion snapshot. **S-4** - CI steps are now parsed into structured steps and pinned as
exact normalized commands; the Task 6.2 guard reuses that machinery rather than a substring match.

## 3. The seven Task 6.2 claims

**1. One new stable numbered requirement.** Confirmed. `### CR-014a Workflow failures retain
authored and runtime occurrence provenance` appears exactly once, between `CR-014` and `CR-015`,
and the guard pins single ownership of the ID. The letter-suffix convention has precedent in
`CR-013a` and `DR-011a`.

**2. It mirrors the approved canonical requirement.** Confirmed against the canonical and reshape
`quality-and-verification` requirement `Authoring lifecycle, fingerprint coverage, and failure
provenance are executable`, whose two blocks are **byte-identical** (1,852 bytes, `34816cd2`). I
also checked CR-014a clause-by-clause against the implementation rather than against the prose:

- closed union - `FailureOccurrence` has a `private protected` base constructor and `internal`
  variant constructors, and `FailureProvenance` can only build them through reflection over
  non-public constructors. Runtime-only is structurally true, not merely asserted.
- `Item(index)` throws `ArgumentOutOfRangeException` below zero, matching "non-negative item index".
- one-failure propagation - `ScopeReducer.AggregateFailures` returns `orderedFailures[0]` unchanged
  for a single cause, so code, message, location, and occurrence are preserved by construction.
- multi-cause aggregation - it creates one `SFE-JOIN-FAILED` at the supplied join location and
  occurrence while retaining every cause.
- codec - `WorkflowFailureJsonConverters` rejects any version other than 1, accepts exactly
  `root`/`branch`/`item` with an exact per-variant property allowlist (`HasOnly`), and rejects a
  negative index. "Rejected rather than coerced" is literal.

See finding T-4 for a precision point about which approved source each clause actually mirrors.

**3. `AC-022` relocation.** Confirmed and, more importantly, **proven load-bearing**. AC-022 is
*Structural fingerprint and opaque-code versioning are honest*; the two tests it moved to are
`Fingerprint_IsDeterministicAndChangesWithStructureAndOutcome` and
`Fingerprint_IgnoresCapturedOpaqueSelectorConfiguration`, which is exactly what that criterion
describes. Its previous home, `FailureProvenanceTests`, has nothing to do with fingerprints, so the
old tag was false. Mutation N20 removes AC-022 from both fingerprint tests and the pre-existing,
independent `RepositoryGuardTests.AcceptanceCriterionCatalog_HasTraitCoverageOrExplicitWaiver`
fails naming `AC-022`. The catalog cannot be silently orphaned.

**4. Active ephemeral and durable runtime regressions.** Confirmed. Both execute a real failing
authored parallel branch and assert the exact authored location
`workflow:$/n:00000001/parallel:00000000/n:00000000`, `FailureOccurrence.Branch` with branch ID
`failing`, and the authored outcome order. The durable test is genuinely durable: it drives
`DurableWorkflowRuntime` over a real provider store, loads the persisted checkpoint, and reads
state through `DurableExecutionEnvelopeV2.Deserialize`. Neither file appears in its project's
`Compile Remove` list - which matters here, because the durable project excludes twenty-seven other
`Driver\*`/`Execution\*` files, exactly the trap the author's own self-review caught.

**5. Non-vacuous must-green guard.** Confirmed. `Task62_...` is a `[Fact]` on the
`Disposition=Infrastructure` class and every clause is independently mutation-proven - see section
5. It is materially stronger than the Task 6.1 guard: it pins canonical-to-delta byte equality and
five failure-provenance clauses inside the canonical block, so a **coherent** revert across both
trees is caught (N5 RED). That is precisely the gap I recorded as S-2 last round, closed here for
this requirement.

**6. Crosswalk delta.** Confirmed: 336 to 337 physical sources, 1,384 to 1,386 declarations, 188 to
189 active files, 696 to 698 active declarations; the 866-row retired ledger is untouched. The
deltas are exactly the one new durable test file (+1 file, +1 declaration) and the new guard
(+1 declaration). The row totals are machine-derived from live discovery, not hand-asserted, and
N15 confirms a count edit is RED.

**7. Scope boundaries.** Confirmed by inspection of all fifteen paths: no `src/**`, no
`openspec/specs/**`, no public API baseline, no package manifest. Task 6.2 is `- [x]`, 6.3 remains
`- [ ]`, and the harmonization ledger is **20 complete / 14 open / 34 total**.

## 4. Validation reproduced from an independent clean checkout

Disposable worktree at `532940a3` with the fifteen target files copied in; both anchors reproduced
there before any build.

| gate | result |
|------|--------|
| Debug `dotnet build OrcaCore.slnx --no-incremental -m:1 -warnaserror` | 0 warnings, 0 errors |
| Release, same flags | 0 warnings, 0 errors |
| exact package feed | 12 packages |
| `Requirement=CR-014a` - Core / Ephemeral / Durable | 8/8, 1/1, 1/1 |
| `OrcaCore.Core.Tests` | 350/350 |
| `OrcaCore.Engine.Ephemeral.Tests` | 79/79 |
| `OrcaCore.Engine.Durable.Tests` | 99/99 |
| `OrcaCore.Acceptance.Tests` | 37/37 |
| `OrcaCore.Hosting.Tests` | 24/24 |
| `OrcaCore.ProviderCertification` | 96/96 |
| `Disposition=Infrastructure` | 217/217 |
| `Disposition=ExpectedRed` | exactly 14 failures, 0 passed |
| unfiltered guards | 231 total = 217 green + 14 expected red |
| `openspec validate --all --strict` | 18 passed, 0 failed |
| `git diff --check` | exit 0 |

Every declared number reproduces exactly.

Committability: a simulated checkpoint commit produced **exactly 15 real paths**, a clean worktree,
and 217/217 afterwards, exercising the schema 9 committed-blob branch of the scoped content record.

## 5. Mutation and probe results

Control green. RED:

| id | mutation | result |
|----|----------|--------|
| N1 | rename the CR-014a heading | RED |
| N2 | change "ordered by item index" to "completion time" | RED |
| N3 | move a required clause verbatim into `CR-015` | RED |
| N4 | desynchronize canonical from its reshape delta | RED |
| N5 | coherently drop a failure-provenance clause from **both** trees | RED |
| N6 | restore `[Trait("AC", "AC-022")]` on the provenance suite | RED |
| N7 | break AC-022 on one fingerprint test | RED |
| N8 | `Skip` a named Core evidence member | RED |
| N9 | `<Compile Remove>` the durable regression | RED |
| N10 | `<Compile Remove>` the ephemeral evidence file | RED |
| N11 | point one CI requirement lane at a different project | RED |
| N12 | alter the asserted authored location by one digit | RED |
| N13 | drift one non-excluded target file | RED |
| N14 | forge the scoped content anchor by one hex digit | RED |
| N15 | tamper the crosswalk physical-file count | RED |
| N18 | drop the requirement trait from the durable evidence | RED |
| N19 | drop the requirement trait from the ephemeral evidence | RED |
| N20 | orphan AC-022 by removing it from both fingerprint tests | RED (independent Core guard) |

GREEN, and these are findings T-1, T-2, T-3:

| id | probe | result |
|----|-------|--------|
| N17 | record an owner-authorized checkpoint as independently `Approved` | **GREEN - 217/217** |
| N3b | relocate a required clause under an inserted `##` section heading | **Task62 GREEN** |
| N16 | append a sentence to CR-014a contradicting its own codec rule | **Task62 GREEN** |

## 6. Findings

### T-1 (P2, inherited) - the registry cannot tell independent approval from owner authorization at the `Approved` state

Round 44's remediation introduced `OwnerAuthorizationAwaitingEvidenceCommit` / `OwnerAuthorized`
specifically so owner authority is never presented as independent review, and it enforced that with
a `-owner-approval-verdict-` filename token. That token is required on the two `Owner*` states but
never **forbidden** on the independent ones.

N17 demonstrates the consequence: pointing Task 6.1's `stateEvidencePath` at the owner-approval
verdict while leaving `reviewState: "Approved"` keeps the entire 217-test Infrastructure lane green.
The distinction is one-directional and therefore not a real invariant.

This is not hypothetical in the live registry. Task 6.1 is recorded as `Approved`; its
`verdictEvidence` mixes two independent APPROVEs with one owner-approval verdict; and its
`approvalEvidenceCommit` is `87da8c2b` - a checkpoint whose other thirteen paths were
owner-authorized, never independently reviewed. Separately, `ArchivedReviewFreeze` carries no
review-state or verdict field at all, so the independently approved `9f4fa0b5` and the
owner-authorized `87da8c2b` are recorded in an identical shape.

Everything here is disclosed in prose - the request, `tasks.md`, and the dated owner verdict all say
what happened - so this is a structural gap, not a concealment. It arrived with schema 9 in
`87da8c2b`, before this target, and Task 6.2 neither introduced nor worsened it. It should not block
this checkpoint. Two additions close it: forbid the `-owner-approval-verdict-` token in
`stateEvidencePath` for `ApprovalAwaitingEvidenceCommit` and `Approved`, and give each archived
freeze an explicit authority field validated the same way.

### T-2 (P3) - the CR-014a block terminator is looser than its sibling guard's

`Task62_...` ends the requirement block at the next `"\n### "`. The Task 6.1 guard ends its block at
the next `"\n##"`, which stops at a section heading as well. N3b shows the difference is real:
moving the codec-rejection clause out of CR-014a and placing it under a newly inserted
`## 4.1b` heading before `### CR-015` leaves `Task62_...` **green**, because the block simply runs
across the section boundary to the next `###`. Today CR-014a is immediately followed by `### CR-015`
so the boundary is exact; the weakness is latent until a section is inserted or CR-014a becomes the
last requirement in its section. One character - `"\n##"` - makes it match the sibling.

### T-3 (P3) - clause pins are containment-only, so contradictions can be added

N16 appends "Malformed payloads MAY instead be coerced to a root occurrence." to CR-014a, directly
contradicting the sentence above it, and `Task62_...` stays green: the guard proves the nine
required clauses are present, never that nothing contradicts them. Only the freeze content anchor
noticed, and a legitimate refreeze clears that. This is the standard limit of substring pinning
rather than a defect in this target, and it applies equally to the Task 6.1 guard.

### T-4 (P3) - "mirrors ... without adding semantics" names one source but draws on several

CR-014a is richer than the `quality-and-verification` requirement the artifact names. That
requirement says "multiple causes retain ordered individual provenance"; CR-014a supplies the
ordering keys ("fixed branches ordered by authored branch order and dynamic items ordered by item
index"), the owning-failure rule, the non-negative item index, and the codec rejection behaviour.

I checked each addition and none is new semantics: the ordering keys are canonical
`structured-fiber-execution` ("causes ordered by authored branch or item index") and
`docs/specs/17` line 864 ("in authored branch/item-index order, never completion order"), and the
rest matches `ScopeReducer.AggregateFailures`, `FailureOccurrence.Item`, and
`WorkflowFailureJsonConverters` exactly. So the requirement is sound - but it mirrors a set of
approved sources, not the single one the artifact cites, and the claim would be more accurate if it
named them. Task 6.3 will map acceptance criteria to this requirement, which is the natural place to
record the full source list.

## 7. Operational note on landing this verdict

Writing this file into the reviewed worktree makes the live projection **16** rows against a
**15**-row manifest, and `ValidateActiveReviewFreeze` asserts raw-order equality between the two, so
the Infrastructure lane goes RED until they are reconciled. Commit the approved fifteen-path
checkpoint first, then add this verdict; or refreeze to sixteen entries before validating. The guard
is working as designed.

## 8. Reviewer hygiene

- The reviewed worktree was never modified, staged, or committed by this reviewer. `HEAD` is
  unchanged at `532940a3865b8eec85c315dca9da09b874cb4173`, nothing is staged, and both anchors
  reproduce after validation exactly as they did before it.
- All builds, suites, mutations, and the simulated checkpoint ran in a disposable `git worktree`,
  now removed and pruned. `git log --all` contains no probe commit.
- This reviewer created no commit in the reviewed repository.

## Determination

All seven claims hold and each is mutation-proven. CR-014a was verified clause-by-clause against the
runtime and codec implementation rather than against its own prose; the `AC-022` correction is
truthful and proven load-bearing by an independent pre-existing guard; both runtime regressions are
real, compile-included, and assert exact provenance; the durable one genuinely reads persisted
checkpoint state. The freeze anchors, both archived freezes, and all four registry entries reproduce
independently, the declared validation packet reproduces in full from a clean checkout, and the
checkpoint is provably committable at exactly fifteen paths. T-1 is a genuine structural gap but it
is inherited from the base, fully disclosed in prose, and untouched by this task. T-2, T-3, and T-4
are durability and precision gaps, not correctness defects.

**Verdict:** **APPROVE**
