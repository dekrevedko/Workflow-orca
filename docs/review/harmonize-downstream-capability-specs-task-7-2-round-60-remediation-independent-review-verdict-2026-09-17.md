# Harmonization Task 7.2 Round 60 remediation independent review verdict

**Date:** 2026-09-17
**Reviewer:** independent review
**Scope reviewed:** the nineteen-entry superseding Task 7.2 freeze on base
`261d578b11a4b2d09e033aa44c8123cbc4ec653e`, tree `cc4943fa88d38e23726d435e84b6bc6602222cbc`, named by
`harmonize-downstream-capability-specs-task-7-2-round-60-remediation-dirty-manifest-2026-09-17.txt`,
which remediates Round 60 findings PP-1, QQ-1, and RR-1.
**Authorization scope:** the request asks to create the remediated Task 7.2 immutable-history
checkpoint. This verdict authorizes nothing.

## Summary

Every validation count and mutation result in the request reproduces. PP-1 and RR-1 are closed.
QQ-1 is closed only for harmonization review evidence and archive successors. Two gaps remain in the
new admission model, and both block:

- **SS-1 (P2):** review evidence outside the harmonization registry has no admission path. The
  binding phased plan requires every remaining reshape phase to write dated status, prompt, and
  review records under `docs/review/`. The first such record turns the Infrastructure lane red, and
  neither the registry nor `appendOnlyRecords` can admit it without a guard-source change.
- **TT-1 (P2):** an approved review request is admitted by path, not by bytes. After activation, the
  Task 7.2 request can be rewritten, and the routine current-match refresh restores green. The design
  and task ledger state the opposite.

What is closed, measured:

- **PP-1:** the 427-record baseline reproduces exactly from the committed blobs. All 25 records
  that diverged in the author's checkout now match after normalization, and the fresh-worktree and
  committed-checkpoint lanes are green. A real `core.autocrlf=true` clone passes all 427 baseline
  comparisons.
- **QQ-1, harmonization side:** replaying the evidence and activation commits on the simulated
  checkpoint needs no guard-source change and passes Infrastructure 223/223. Registered manifests
  and verdicts stay durably pinned after the routine refresh. Archive successors bind to the active
  freeze and then to their first Git addition.
- **RR-1:** the token-ignoring lease body is restored, polling is delayed and bounded, and the
  source guard rejects both regressions. The six lease scenarios passed 20/20 at 4× concurrency.

Two non-blocking observations are recorded: UU-1 (P3, CRLF sensitivity) and VV-1 (P3, two
unpinned values).

## Method

All probing ran in fresh disposable environments created from `261d578b`:

- one worktree for validation and the simulated checkpoint and approval chain;
- one worktree for mutation probes;
- one separate `core.autocrlf=true` clone for a real CRLF checkout.

Each worktree received the nineteen frozen entries byte-for-byte, and its porcelain was confirmed
byte-identical to the frozen manifest. The reviewed worktree was never modified, `HEAD` never moved,
and nothing was staged or committed in it. All three environments are removed, and every throwaway
commit is contained in no ref.

Each probe ran only its owning guard. The one probe that had to satisfy both guards (U10)
re-anchored the active content record. PowerShell 7 is absent on this host, so the review-manifest
`-Check` and the routine current-match refresh were reproduced by the Python emulation used since
round 54.

## 1. Freeze anchors reproduced

- **Raw commit-real porcelain:** **1,551 bytes**, SHA-256
  `d1c4d30a0d83d29f1dfb070c40f761ff4be00351ed375c3aab8837be149ac591`, byte-identical to the frozen
  manifest. Nineteen entries (thirteen modified, six untracked), none staged, index tree equal to the
  `HEAD` tree.
- **Scoped content record:** **18 rows, 2,741 bytes**, SHA-256
  `129aa624937ccb3b171186a18313b26b40c7baf03371788f21ccc9850d735aad`. The review-provenance fixture
  is its only exclusion, and `activeFreeze` names the same values.
- No path under `src/**` or `openspec/specs/**` changes.

## 2. The superseded freeze is preserved exactly

- The base is unchanged from Round 60.
- The Round 60 request (4,591 bytes, `3a070ab1…`), manifest (1,211 bytes, `8c7cb3f4…`), and `REJECT`
  verdict (15,345 bytes, `05c9f280…`) are byte-identical to what Round 60 reviewed and issued.
- Rejected freeze `7.2-initial-rejected` registers exactly those values.
- In a fresh worktree, all fourteen archived freezes reproduce manifest, content record, and tree
  from their checkpoint blobs.
- All ten entries reproduce their historical rows, and the maximal current-match pins are
  order-exact.

## 3. PP-1 closed: the baseline is checkout-independent

I rebuilt the baseline directly from `git ls-tree` and `git cat-file` at `261d578b`, excluding the two
mutable surfaces and normalizing CRLF and lone CR to LF:

| Quantity | Recomputed from blobs | Fixture and guard source |
|---|---|---|
| Records | 427 | 427 |
| Record bytes | 66,459 | 66,459 |
| Record SHA-256 | `5be2e456…6e180` | `5be2e456…6e180` |
| Rows equal, ordinal order | yes | yes |
| Blobs containing CR | 0 | — |
| Paths needing Git quoting | 0 | — |

The same 25 author-checkout files still differ from their blobs byte-for-byte. After normalization,
all 427 match, so the Round 60 failure mode is gone. The fresh worktree and the committed checkpoint
are both green (section 6).

In the CRLF clone, the historical files are checked out with CRLF. Even so, the guard's 427-record
baseline loop passes, because it runs before the only CRLF-sensitive step (see UU-1).

The mutations behave as they should:

- CRLF-only transform of a baseline file: green (U1).
- Appended content: red (U2).
- Deletion: red (U4).
- Rehashing a record, or the record plus the fixture aggregate: red, the latter on the guard-source
  constant (U5, U6).
- Broadening the mutable set, dropping a record, or duplicating one: red (U11 to U13).

## 4. QQ-1: closed for harmonization evidence, open for two classes

### 4.1 What the admission model now does (measured)

On the simulated checkpoint (tree `4c1330bb…`, section 9), I replayed the Task 7.1 pattern from
`3142ab22` and `e7f8e26c`:

1. **Evidence commit:** a simulated approving verdict, `activeFreeze` cleared, a new
   `7.2-round-60-remediation` archived freeze, and a new Task 7.2 entry registering both 7.2
   verdicts in state `ApprovalAwaitingEvidenceCommit`.
2. **Activation commit:** `approvalEvidenceCommit` set and the state moved to `Approved`.

The Infrastructure lane is **223/223** in that state, with no guard-source change. The durable pins
then hold even after the routine refresh:

- Editing the registered manifest is red on its manifest byte pin (T2).
- Editing the approving verdict is red on "immutable review evidence must remain byte-exact" (T3).
- Editing the Round 60 `REJECT` verdict is red the same way (T4).
- An unregistered review record is red (T5, U7).

Archive successors behave as documented:

| Probe | Case | Result |
|---|---|---|
| A1, U8 | unclassified successor | **red** |
| A2, U9 | listed in `appendOnlyRecords` but outside the active freeze | **red** |
| U10 | added to the active freeze | green on both guards |
| A3 | committed | green |
| A4 | committed content edited | **red** |
| A5 | edit plus a rehashed entry | **red**, on the first-addition binding |
| A6 | CRLF-only transform | green |
| A7 | deleted and re-added | **red**, two additions |

A file moved into the archive with `git mv` is reported as an addition, so moved successors bind
correctly.

### 4.2 SS-1 (P2, blocking): review evidence outside the harmonization registry has no admission path

The guard admits a post-baseline `docs/review/` file only when `review-manifest-provenance.json`
names it (`OpenSpecCorpusGuards.cs:1823`). `appendOnlyRecords` is restricted to `docs/archive/`
(`OpenSpecCorpusGuards.cs:1773`). The registry cannot hold anything but harmonization evidence:

- entry verdicts must equal the discovery glob
  `harmonize-downstream-capability-specs-task-{task}*verdict-*.md`;
- the set of registered manifests must equal the discovered
  `harmonize-downstream-capability-specs-task-*-dirty-manifest-*.txt` set.

`docs/review/` has other active families, and the largest is still accruing records:

- 111 dated `developer-facing-interface-*` records, the latest committed on 2026-08-18 at reshape
  task 7.22;
- the reusable template this target keeps mutable, which writes its review to
  `docs/review/developer-facing-interface-phase-<NN>-<SLUG>-review-<DATE>.md`;
- the binding phased plan (`docs/implementation/developer-facing-interface-refactor-phased-plan-2026-07-14.md`,
  section 5), which requires every phase to "Create a dated phase status report and filled
  copy-ready reviewer prompt under `docs/review/`".

The remaining reshape work (8.0 through 10.11, including the 10.10 dated re-review request and the
10.11 independent re-review) must follow that protocol.

In the approved state from 4.1, measured:

| Probe | Action | Result |
|---|---|---|
| S1 | add `developer-facing-interface-section-08-independent-review-verdict-2099-01-01.md` | **red**, `Task72_…` unclassified |
| S2 | register S1 as verdict evidence of an existing entry | **red**, the entry's verdict glob set no longer matches |
| S3 | register a reshape `REJECT` freeze as a rejected freeze | **red**, registered manifests no longer equal the discovered harmonization manifests |

So the next mandatory reshape phase review breaks the must-be-green lane. The only remedy is a
guard-source or schema change, which is the Round 60 QQ-1 failure moved to another family.

Archive README rule 5 compounds this. It tells authors to "Register review evidence in
`review-manifest-provenance.json`", and for these records that instruction cannot be followed.

### 4.3 TT-1 (P2, blocking): an approved review request is admitted by path, not by bytes

The registry byte-pins manifests, verdicts, and rejected-freeze requests. It does not byte-pin the
request of an archived freeze or of an entry:

- `ValidateArchivedReviewFreeze` (`OpenSpecCorpusGuards.cs:3461`) and
  `ValidateReviewManifestProvenance` (`OpenSpecCorpusGuards.cs:3218`) check only that the request
  exists and names its manifest.
- The archived freeze's content record is rebuilt from checkpoint blobs, not from the worktree.
- The only worktree check is the entry's `currentWorktreeMatchPaths` pin, which is refreshable by
  design.
- `Task72_…` then admits the path without reading it.

Every existing request sits inside the fixed baseline, so this gap opens with the first post-baseline
approval, which is this target's own request.

In the approved state from 4.1:

| Probe | Action | Result |
|---|---|---|
| T1 | edit the approved Task 7.2 request, keeping its manifest name (`223/223` to `999/999`) | **red**, only the Task 7.2 current-match pin |
| T1b | T1 followed by the routine current-match refresh (7.2 pins 18 to 17) | **green** on `Task72_…` and the review-manifest guard |

This contradicts the committed text. The design states that later review "requests, manifests, and
verdicts are admitted only through their existing byte-pinned review-manifest registration"
(`design.md:228-229`). The Task 7.2 completion says the guard "rejects missing, modified,
unclassified, duplicate, or broadened records" (`tasks.md:335-337`).

A dated review request records what was asked and authorized. Task 7.2 exists to make exactly these
records immutable.

## 5. RR-1 closed

- **Token-ignoring body restored:** the first protected body again ends at
  `await gate.Release.Task;`, with no cancellation acknowledgement.
- **Bounded polling:** `AwaitSignalBeforeWorkflowTerminalAsync` polls the real instance snapshot at a
  delayed 10 ms interval under a 15-second `TimeProvider.System` deadline.
  - It rechecks the signal after each snapshot.
  - It covers all five terminal `WorkflowInstanceStatus` values.
  - It raises a named failure when neither side progresses.
- **Source guard:** re-adding `ThrowIfCancellationRequested()` (L1) and replacing the delay with
  `Task.Yield()` (L2) are both red.
- **Stability:** the six lease scenarios passed in every full lane run here, and 20/20 in five
  rounds at 4× concurrency. The only failures in the stress worktree were the 3.11d scenarios, which
  need ProviderCertification built there; they pass in the full lane.
- **Product unchanged:** no `src/**` path changes, so product arbitration is untouched.

## 6. Validation reproduced from a clean checkout

| Gate | Result |
|---|---|
| Debug and Release builds, non-incremental, warnings as errors | 0 warnings, 0 errors each |
| Exact package feed | 12 packages |
| Core / Ephemeral / Durable / Acceptance / Hosting / Certification | 350 / 79 / 99 / 37 / 24 / 96 |
| PostgreSQL / SQL Server / Integration | 101 / 72 / 11 |
| Full guard lane, dirty target | 223 passed, 14 failed, 237 total |
| Classification of the 14 failures | all `ExecutableBehaviorExpectedRedGuards.Scenario_…`, zero others |
| Infrastructure lane, Release, CI filter `Disposition=Infrastructure` | 223/223 |
| Expected-red lane | exactly 14 |
| Committed simulated checkpoint, full guard lane | 223 passed, 14 expected red |
| Simulated evidence and activation, Infrastructure lane | 223/223 |
| OpenSpec `validate --all --strict` | 18 / 18 |
| Harmonization ledger | 26 complete, 8 open, 34 total |
| Review-manifest current matches | order-exact for all ten entries (emulated) |
| `git diff --check` / changes under `src/**` or `openspec/specs/**` | clean / zero |
| Porcelain after validation | byte-identical to the manifest |

## 7. Mutation and probe results

| Probe | Mutation | Result |
|---|---|---|
| U1 | CRLF-only transform of a baseline file | green |
| U2 | content appended to that file | **red** |
| U3 | `\n\n` rewritten to `\r\n` (removes a blank line) | **red**, correctly a content change |
| U4 | baseline file deleted | **red** (`FileNotFoundException`) |
| U5 | file and its record rehashed together | **red**, fixture aggregate |
| U6 | U5 plus the fixture aggregate rehashed | **red**, guard-source constant |
| U7 | unregistered review verdict | **red** |
| U8–U10 | archive successor: unclassified / outside freeze / inside freeze | **red** / **red** / green |
| U11–U13 | broadened mutable set / dropped record / duplicated record | **red** each |
| L1, L2 | cancellation acknowledgement re-added / delay replaced by `Task.Yield()` | **red** each |
| L3 | CRLF-only transform of `LeaseExitScenarioHost.cs` | **red** (UU-1) |
| L4 | `TimeSpan.FromSeconds(15)` replaced by `Timeout.InfiniteTimeSpan` | green (VV-1) |
| T1, T1b | approved request edited / plus routine refresh | **red** pin / **green** (TT-1) |
| T2–T4 | approved manifest / approving verdict / `REJECT` verdict edited, plus refresh | **red** each |
| S1–S3 | reshape-family verdict / as entry evidence / as rejected freeze | **red** each (SS-1) |
| A1–A7 | archive successor lifecycle, section 4.1 | as documented |

Baseline and final controls were green in every environment. Restoration was verified byte-exact
against the frozen target after each probe.

## 8. Non-blocking observations

### UU-1 (P3): a CRLF checkout adds two new Infrastructure failures

`.editorconfig` declares `end_of_line = crlf`, and the host's system Git default is
`core.autocrlf=true`.

| CRLF clone | Infrastructure result |
|---|---|
| Base | 216 passed, 6 failed |
| Committed target | 215 passed, 8 failed |

Two of the base failures need a local package feed the clone lacks. The other four are pre-existing
corpus guards that read raw bytes. The target adds two failures:

- **`LeaseDiscoveryAndGovernanceInfrastructureGuards` (new pin):** it reads the lease host raw and
  requires the LF-literal text `"TerminalObservationTimeout,\n            TimeProvider.System"`
  (`LeaseDiscoveryAndGovernanceContractGuards.cs:84`), while its sibling patterns accept `\r?\n`.
  L3 isolates it.
- **`Task72_…` (inherited reader):** in the checkpoint state, `ValidateAppendOnlyHistoricalRecords`
  reads the active manifest through the existing LF-only `ReadReviewManifest`, even when
  `appendOnlyRecords` is empty.

The request's statement that "an `end_of_line = crlf` checkout remains green" therefore holds for the
baseline comparison, not for the guard. CI checks out LF, and CRLF checkouts already failed at base,
so this does not block. Two remedies would close it:

- make the lease pin `\r?\n`-tolerant, and read the active manifest only when an uncommitted
  append-only record needs it;
- add a `.gitattributes` `eol=lf` or `-text` rule for review manifests, which would also clear the
  pre-existing review-manifest failure.

### VV-1 (P3): two values the guard does not pin

- **The deadline value:** the source guard pins the name `TerminalObservationTimeout`, not its
  value. Replacing `TimeSpan.FromSeconds(15)` with `Timeout.InfiniteTimeSpan` stays green (L4), which
  reintroduces unbounded observation.
- **The fixture's `baselineCount`:** it is never asserted. Setting it to 999 stays green. The
  guard-source count constant still pins the real count, so the field is only a misleading header.

Note: the baseline check also requires the `261d578b` commit object to be present. That matches the
existing archived-freeze guards and CI's `fetch-depth: 0`, so it is consistent with current
practice. The record rows and guard-source digest pin the baseline without it.

## 9. Simulated checkpoint and sequencing

Committing the nineteen frozen entries on `261d578b` in a disposable worktree produced exactly
nineteen paths, six added and thirteen modified. The resulting tree is
`4c1330bbe9547a52cb3a9b7f788d64d97165bc72`, equal to the simulated checkpoint tree submitted with
this freeze. The worktree was
clean afterwards, and its full guard lane is 223 passed and 14 expected red.

Because this verdict rejects the target, **do not commit it**. This verdict is a new untracked file
matching the Task 7.2 discovery glob `harmonize-downstream-capability-specs-task-7-2*verdict-*.md`. The
next freeze must carry it byte-exact and register it as a second Task 7.2 rejection, for example
as rejected freeze `7.2-round-60-remediation-rejected` pinning this request, manifest, and verdict.
It must not rewrite the Round 60 record.

## 10. Required remediation

1. **Close SS-1 and TT-1 together with one durable rule.** Every file under either historical root
   outside the fixed baseline and the two mutable surfaces should satisfy two conditions:
   - while uncommitted, it belongs to the active freeze manifest, which byte-pins it through the
     content record;
   - once committed, its normalized bytes equal its first Git addition.

   This is the `appendOnlyRecords` mechanism extended to `docs/review/`, applied to every
   post-baseline path. The harmonization registry can keep its extra semantics, but admission must
   not depend on it. Explicit request byte pins in archived freezes and entries would close TT-1
   alone.
2. **Correct the committed text:** the design statement at `design.md:228-229`, the Task 7.2
   completion, and archive README rule 5.
3. **Prove the rule with the probes this round used:**
   - a reshape-family phase review record admitted without a guard-source change;
   - an approved request edited after activation and then routinely refreshed, which must stay red.
4. **Optionally, address UU-1 and VV-1.**

## 11. Reviewer hygiene

`HEAD` remained at `261d578b11a4b2d09e033aa44c8123cbc4ec653e` throughout. Nothing was staged, and I
created no commit in the reviewed repository. It still showed exactly the nineteen frozen entries
when this verdict was written. All disposable worktrees and the CRLF clone are removed and pruned.

## Determination

This remediation is careful and mostly correct:

- **PP-1** is fixed at the root. The baseline comes from committed blobs, is normalized, and is
  green on a fresh worktree, on the committed checkpoint, and across a real CRLF checkout of every
  baseline file.
- **RR-1** restores the token-ignoring coverage with bounded, delayed observation.
- **QQ-1:** the harmonization evidence path is proven end to end without guard-source changes.

The admission model is still incomplete, however. The binding reshape phase-review protocol has no
path into it (SS-1). The first approved request admitted after the baseline, this target's own, can
be rewritten with nothing but the routine refresh (TT-1). Task 7.2's purpose is exactly the
immutability of these records, and the committed design and task text claim both properties, so the
target cannot be checkpointed as complete.

**Verdict:** **REJECT**
