# Harmonization task 5.2 refreeze independent review verdict

**Date:** 2026-08-22

**Reviewer:** independent review of the frozen target; no authorship of the reviewed commits

**Review request:** `docs/review/harmonize-downstream-capability-specs-task-5-2-refreeze-independent-review-request-2026-08-22.md`

**Exact review target:** `c996e3a08f55697e1814cf84a7581c9f05473142`

**Target tree:** `d2d5bcc496bf618dbae7a960aef20754b0c0c8cb`

## Scope

This verdict approves Task 5.2 on the exact immutable tree above. It does not start Task 5.3, does
not close the harmonization gate, does not archive either change, and does not authorize reshape
Task 8.0. Every earlier request and verdict is preserved byte-for-byte; the prior Task 5.2 `REJECT`
records remain immutable history.

## Method

Verification ran against the committed objects and against two disposable `git worktree` checkouts —
one at `c996e3a08f55697e1814cf84a7581c9f05473142` and one at
`5140208c7b82332ada8b7a39848888ddd58eb89d`. Mutation testing ran only inside the disposable
checkouts. Two probe commits were fabricated with `commit-tree` and `update-ref` and deleted
afterwards; no worktree was ever checked out to them. Both worktrees were removed and pruned. The
reviewed working copy was never modified: `HEAD` stayed at
`c996e3a08f55697e1814cf84a7581c9f05473142` with zero staged paths and a single untracked file (the
review request) throughout.

## 1. Immutable chain — MATCH

The chain is linear and each object resolves as claimed:

| Commit | Tree | Parent | Scope |
| --- | --- | --- | --- |
| `ff11ead781f8fef343fafc6e6bc8307d746e4a05` | `3baddd97…` | `179421029f…` | 17 paths |
| `d0e7c4821199b8b1ee13d5f6fd22f79133abc576` | `ef8f948b…` | `ff11ead7…` | 25 paths |
| `5140208c7b82332ada8b7a39848888ddd58eb89d` | `25965144…` | `d0e7c482…` | 8 paths |
| `c996e3a08f55697e1814cf84a7581c9f05473142` | `d2d5bcc4…` | `5140208c…` | 3 paths |

`d0e7c482` is recorded as pre-approval Task 5.2 content (`preApprovalContentCommits`), with
`checkpointCommit` still null — it is not relabelled as an approved checkpoint. `c996e3a` changes
exactly two fixture fields (`reviewState` and `approvalEvidenceCommit`), one ledger checkbox with its
completion text, and the remediation artifact. It activates only; it introduces no new evidence.

No immutable record was edited anywhere in the chain: the only `docs/review/` changes after
`d0e7c482` are two additions. Zero `src/**` paths are touched across the whole chain.

## 2. Task 5.1 approval chain — MATCH

The external Task 5.1 `APPROVE` verdict is committed in `5140208c` byte-exact at **15,778 bytes /
`16989506dc3e4db2da372bb92270960c891819f365615b862267b43e0b200839`**, LF-only, and is unchanged at the
target. It is registered in the provenance fixture under that exact hash alongside both retained
Task 5.1 rejections and both Task 5.2 rejections, each of which re-verifies byte-exact.

## 3. Task 5.2 synchronization — MATCH

Comparing `ff11ead` with the target, exactly three canonical capabilities changed, and exactly eight
requirement blocks:

- `management-and-querying` (1): `Management API is scope-oriented and fluent`.
- `quality-and-verification` (5): `Every code and test removal has complete burden-of-proof evidence`,
  `Known workflow-engine failure patterns stay covered`,
  `Selected-mode capability separation is compile verified`,
  `Typed workflow contract is compile and behavior verified`,
  `Exact facade, event, hosting, and path-token contracts are guarded`.
- `repository-foundation` (2): `Documented application packages are sufficient`,
  `Package topology and ownership are exact`.

The other eleven canonical capabilities are untouched, and all three affected `## Purpose` preambles
are byte-unchanged.

Every one of the eight is delta-backed by `reshape-developer-facing-interfaces` with that delta's own
disposition. Five of them present as in-place rewrites rather than insertions, which looks at first
like an `ADDED`-versus-`MODIFIED` mismatch; I traced each heading and all five were first created
canonically by `50254d0` on 2026-07-31 from this same delta's `ADDED` blocks, so Task 5.2 is
refreshing content the delta already owns rather than re-adding an unrelated pre-existing
requirement. There is no heading-disposition defect here.

Independently re-derived from the target tree:

- Provenance record: **176 rows / 46,211 bytes /
  `e1420f367a491e62e896f041f288b3235eeaf7647d721069cb71dda509b4021b`** — 173 `Synchronized`,
  3 `NewCapabilityOutsideCanonical`, **zero pending operations, zero duplicate owners**.
- Canonical directory inventory: 14 / 547 bytes / `7165dac4e1a57022a7890b421f522bf4152f6d5ddc159a41539ce2ef0d18ec9f`.
- Active-delta inventory: 16 / 1,321 bytes / `5e8a9725979d702ff2f639fef587828958c92533139602ab13f1401d6df7ebd5`.
- Canonical preamble record: all 14 / 1,233 bytes / `595528c6a7ba56dd5648e7fc12ac6bc2af9e9bf6fa3fbf853b86fffdd7cecd3c`.
- Task 5.1's synchronized-removal catalog remains permanent at 11 entries.

Both historical content records still recompute exactly from their rows — Task 5.1 at 2,428 bytes /
`741cfd6b…` and Task 5.2 at 1,793 bytes / `0068973b…` — and every row binds one-to-one to its frozen
manifest in the disclosed order.

## 4. N-1 through N-4 — dispositions and mutations

**N-1 — CLOSED.** The guard now pins the complete four-line `.gitattributes` in order rather than
probing for three lines, and the exemption is disclosed in `design.md`, the task ledger, and the
remediation artifact. I checked the disclosure's factual claim as well: all six exempted lines are
exactly two-space Markdown hard breaks in two frozen review requests, so the stated rationale is
accurate and the exemption is genuinely confined to immutable records.

**N-2 — CLOSED, with a residual.** The subject-line grep is gone. Discovery now walks
`git log --all` over the three owned canonical spec paths, keeps commits that descend from the base,
changed all three paths, and carry a checked `5.2` ledger entry, then requires the declared
`preApprovalContentCommits` to equal that set. `d0e7c482` is recorded, and the `Approved` branch
requires every pre-approval content commit to precede the approval-evidence commit.

I note explicitly that this reformulates the invariant rather than restoring a prohibition: the rule
is now "pre-approval Task 5.2 content must be declared and must precede the approval evidence,"
not "no such content may exist." That is the honest reading of what actually happened, and I regard
it as the right resolution.

**N-3 — CLOSED, with direct evidence.** The new `ApprovalAwaitingEvidenceCommit` state registers the
external verdict while leaving the evidence SHA null and keeping Task 5.2 blocked and task 5.2a open.
I checked out `5140208c` and ran the lane there: **214/214 green**. The transition no longer requires
a red intermediate commit, which was the substance of the finding. `c996e3a` then flips to `Approved`
with the already-existing SHA and completes 5.2a, taking the ledger from 16/18 to 17/17.

**N-4 — CLOSED.** The pin set is now recomputed as the maximal set of historical rows still
byte-verifiable in the working tree and compared with `Equal`, so it can be neither shrunk nor
padded. I recomputed both sets independently: Task 5.1 has exactly 11 still-matching rows and Task
5.2 exactly 4, with zero unpinned-but-still-matching rows in either, and all 15 pins verify against
current bytes.

| ID | Mutation | Result |
| --- | --- | --- |
| P-1 | Replace the review rule with `* -whitespace` | **RED** — exact allowlist mismatch |
| P-2 | Remove `d0e7c482` from `preApprovalContentCommits` | **RED** — rediscovered from content |
| P-3 | Set `Approved` with a null evidence SHA | **RED** |
| P-4a | Empty the Task 5.1 pin set (11 entries) | **RED** |
| P-4b | Empty the Task 5.2 pin set (4 entries) | **RED** |
| P-5 | Fabricated commit changing all three canonical specs | **RED** — discovered and undeclared |
| P-6 | Fabricated commit changing one canonical spec | **GREEN** — residual, recorded as O-1 |

Control before and after every mutation: 3/3 green. P-5 matters because it proves the discovery is
live rather than an echo of the fixture: the guard reported the fabricated SHA by name.

## 5. Validation — REPRODUCED

From a clean checkout of `c996e3a08f55697e1814cf84a7581c9f05473142`:

- Release build, `--no-incremental -m:1 -warnaserror`: **0 warnings / 0 errors**, 29 outputs.
- Debug build, `--no-incremental -m:1 -warnaserror`: **0 warnings / 0 errors**, 29 outputs.
- `Disposition=Infrastructure`: **214 passed / 0 failed / 0 skipped**.
- `Disposition=ExpectedRed`: **exactly 14 failures**, all `ExecutableBehaviorExpectedRedGuards`.
- Complete guard assembly: **228 total — 214 passed, 14 intentional red**.
- Focused provenance and attribute guards: **3/3**.
- `OrcaCore.ProviderCertification`: **96 passed / 0 failed**.
- `openspec validate --all --strict`: **18 passed / 0 failed**.
- Harmonization ledger: **17 complete / 17 open / 34 total**.
- `git diff --check ff11ead..c996e3a`: exit 0.

From a clean checkout of `5140208c7b82332ada8b7a39848888ddd58eb89d`: Release build 0/0 and
`Disposition=Infrastructure` **214/214**.

## 6. New findings — all minor

### O-1 (P3) — content discovery is conjunctive and ref-sensitive

`DiscoverTask52ContentMaterializationCommits` only recognises a commit that changes **all three**
owned canonical paths in a single commit and carries a checked `5.2` ledger row at that commit. A
fabricated commit touching only `openspec/specs/quality-and-verification/spec.md` was not discovered
and the guard stayed green (P-6). Landing owned canonical content across several commits, or before
the ledger row is checked, therefore evades the registry. Consider discovering any post-base commit
that touches any owned canonical path and requiring an explicit disposition for each, accepting the
extra bookkeeping that downstream tasks will generate.

Separately, `git log --all` includes every ref plus other worktrees' `HEAD`s, so a stale branch or a
second worktree parked on such a commit will turn this guard red for reasons unrelated to the change
under review. Worth stating in the design so the failure is diagnosable.

### O-2 (P3) — `blockingEvidencePath` is unvalidated and now misnamed in the approved state

The field is read only inside the `Rejected` branch. At the target it names the `APPROVE` verdict,
where nothing checks it, so it can drift to any path without detection. Either validate it per state
or rename it to something state-neutral such as `primaryEvidencePath`.

### O-3 (P3) — maximal pins are computed against the live working tree

Because the maximal set is recomputed from working-tree bytes, an ordinary downstream edit to any
pinned file — several of the 11 Task 5.1 pins are canonical specs that Task 5.3 may legitimately
touch — shrinks the maximal set and turns the must-be-green lane red until the fixture is refreshed.
That sensitivity is the point of the check, but it will fire during routine work rather than only on
tampering, so the refresh step belongs in the freeze procedure rather than being discovered as a
surprise failure.

### Note on registering this verdict

`ValidateReviewVerdictEvidence` globs Task 5.2 verdicts the same way it globs Task 5.1's, so adding
this file makes it discovered and unregistered until the fixture records it. The
`ApprovalAwaitingEvidenceCommit` pattern introduced for N-3 applies unchanged here: register this
verdict with its byte hash in that state first, commit it, then activate `Approved` with the
resulting SHA in the following commit.

## Determination

- The commit chain, trees, and per-commit scopes all reproduce exactly, and the activation commit
  does nothing beyond activation.
- Task 5.2's canonical synchronization is exactly the eight declared operations across the declared
  three capabilities, every one delta-backed, with all fourteen preambles and the other eleven
  capabilities untouched; the provenance corpus is 176 rows with zero pending operations and zero
  duplicate owners.
- All four findings from the previous review are closed, each proven by a mutation that turns the
  guard red for the stated reason, and N-3 additionally proven by a green lane at the intermediate
  commit — the specific outcome the finding demanded.
- Every validation number in the request reproduces on clean checkouts.
- The three new findings are all P3 hardening or naming matters. None affects the correctness of the
  Task 5.2 synchronization or the integrity of the approval chain, and none warrants withholding
  approval of this target.

**Verdict:** **APPROVE**
