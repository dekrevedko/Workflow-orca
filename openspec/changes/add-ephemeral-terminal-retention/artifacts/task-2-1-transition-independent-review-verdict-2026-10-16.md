# Issue #1 task 2.1 canonical transition — independent review verdict

**Date:** 2026-10-16
**Reviewer:** independent review
**Request:** `artifacts/task-2-1-transition-independent-review-request-2026-10-16.md`. This review did
not modify it.
**Target:** branch `issue/1-ephemeral-retention`, base `f1eb86e31306d0aab88d8f84d38c925ef2eaf4c7`. The
contract chain is `5501cad` → `3a08796` → `f1eb86e`. The target is the 38 entries named by
`task-2-1-transition-dirty-manifest-2026-10-16.txt`: 31 tracked modifications and 7 untracked files.

## Verdict: APPROVE

I approve the exact frozen transition. That covers:
- the canonical append of five requirements;
- the numbered-document and guide reconciliation;
- schema 12 and its two `ContractApprovedCanonicalReviewPending` records;
- both guard adaptations;
- the five carried P3 resolutions;
- all 499 occurrence decisions.

There are no P1 or P2 findings. Seven P3 notes are carried to later gates (section 5).

### Authorized next actions for task 2.2, in this order

1. **Checkpoint.**
   - Re-verify zero drift against section 1.
   - Stage **exactly** the 38 manifest paths. Do not stage this verdict.
   - Confirm the staged tree is `83a08eaad4511500f482b7beace3a9455678a3ff` with parent `f1eb86e`.
   - Commit on `issue/1-ephemeral-retention`.
2. **Direct child.** It adds this verdict byte-exact and nothing else. Report both SHAs and the clean
   state.
3. **Activation is not checkbox-only.** My probes Q1 and Q2 show that ticking 2.1, or 2.1 and 2.2,
   turns `CanonicalSynchronizationGate` red. The new validator requires both tasks to be open while
   the stage is `ContractApprovedCanonicalReviewPending` and `canonicalApprovalEvidencePath` is null.
   That is correct for the checkpoint tree, but it means completing 2.1/2.2 needs **a separate small
   binding target, independently reviewed**. That target must:
   - tick only 2.1 and 2.2;
   - move both records to a canonical-approved, source-pending stage;
   - bind this transition's checkpoint commit and tree, the verdict child, and this verdict's
     SHA-256 (as `canonicalApprovalEvidencePath`), verified through git exactly as the contract
     evidence is today;
   - adapt the validator, refreeze any provenance or registry pin it changes, and pass the full
     Infrastructure lane.

   Task 3.1 ("After 2.2") must not start until that binding target is checkpointed.

### Not authorized

- a checkbox-only activation;
- any source, test-first slice or executable API, compile or package baseline;
- a rebase or merge toward `master`, a push or a pull request;
- issue comments or closure;
- issue #2 or #3 work.

## 1. Freeze anchors

| Anchor | Value | Result |
|---|---|---|
| HEAD / index | `f1eb86e…`; nothing staged | Unchanged at start and end of review |
| Raw porcelain (`core.quotepath=false`, `--untracked-files=all`, ordinal, LF + final LF) | 38 lines, 2,493 B, `9e9b3f28704f22956d5a8c6e2cad15984011e0fea207ea3efb2ec9ffc1a0c4ed` | Matches the author's report and the self-inclusive manifest |
| Content record (`status\tpath\tlength\tsha256`, LF + final LF) | 38 rows, 5,183 B, `c8940fbf0481e28238b51039ac5d4481b888fab264b9587a2e4049a76c4ff6c3` | Identical at start and end of review |
| Candidate tree, staged in a disposable copy at the base | `83a08eaad4511500f482b7beace3a9455678a3ff`; 38 files; `git diff --cached --check` clean | Matches |

**Base drift (informational).** `origin/master` is at `1c86c23`. The target stays on `f1eb86e`, as the
request states. See P3-3.

## 2. Validation reproduced independently

All runs used a fresh short-path disposable detached worktree, `E:/ocv194`, at `f1eb86e`, holding a
byte copy of the 38 entries. Its content record reproduced as `c8940fbf…`.

| Check | Result |
|---|---|
| `openspec validate --all --strict` | 22 passed, 0 failed |
| `dotnet build OrcaCore.slnx -c Release -warnaserror` | 0 warnings, 0 errors |
| Exact package feed (Windows PowerShell) | 12 packages |
| Full `Disposition=Infrastructure` lane | **247 / 247 passed** |
| Canonical sync, recomputed block by block | Both files are **append-only**: the base text is an exact prefix, and the preambles and all 18 + 14 pre-existing blocks are unchanged. The 1 + 4 added blocks are byte-equal to their delta blocks |
| Approved-to-synced block hashes | The four `management-and-querying` blocks equal the R1-approved hashes. `developer-facing-surface` changed `8fd88b19…` → `2151e1df…`, solely through the carried P3-3 scenario narrowing |
| Provenance TSV | 189 rows, 49,879 B, `0f56a33d2aa17e6848ac8f3bf4769ec1a8604d0dadb25a7f9f9b8092a673f459`: 178 Synchronized, 8 superseded, 3 outside canonical, 0 pending. The five issue rows have delta hash equal to canonical hash |
| Current provenance artifact; registry artifact (LF-normalized) | `8b4e385a…`; `de760a4a…`. Both equal their guard and fixture pins |
| Superseded catalog, recomputed from guard source | 16 rows, 2,824 B, `08f0c3ef…`. All 16 predecessor files are byte-exact and unmodified, and the R1 artifact is appended at `833de7f6…` |
| Task 7.3 live table | All 13 refreshed hash cells belong to files this target edits, and each equals the file's current hash, as `Task73PinRefreshDecision` requires |
| New identifiers | AC-533–542, MG-054–058 and OB-026 have no prior definition or mention at the base. OB-026 sits in §15.3 Metrics, and the doc 10 addition sits under PR-040 |

### Mutation probes

Each mutation ran against a byte-restored copy. Q0–Q10 ran the OpenSpec, DAG and normative guard
classes; Q11–Q13 ran the full lane.

| Probe | Mutation | Result |
|---|---|---|
| Q0 | none | green, 34/34 |
| Q1 | tick 2.1 only | **red** (`CanonicalSynchronizationGate`) |
| Q2 | tick 2.1 and 2.2 | **red** |
| Q3 | edit pre-existing `management-and-querying` canonical text | red (DAG transition, canonical pins and provenance) |
| Q4 | edit an unrelated canonical capability (`durable-runtime`) | red |
| Q5 | canonical added block drifts from its delta | red |
| Q6 | delta and canonical edited identically | red (canonical content pin) |
| Q7 | registry stage set to `Complete` | red |
| Q8 | verdict SHA-256 changed in the registry fixture | red |
| Q9 | approval-verdict bytes edited | red |
| Q10 | `canonicalApprovalEvidencePath` set before approval | red |
| Q11 | doc 09 MG-056 section deleted | red (Task 7.3 doc pin) |
| Q12 | doc 17 §17.4 `TerminalRetention` property line deleted | **green**, see P3-2 |
| Q13 | doc 12 AC-539 deleted | red (Task 7.3 doc pin) |
| Final | after all restores | green, 34/34 |

The copy's content record returned to `c8940fbf…`. No commit, stage or ref was created in the reviewed
repository.

## 3. Substance

### Canonical and numbered transition

The five canonical additions are exactly the approved requirements. The developer-facing scenario is
narrowed to accepted-instance retention plus mandatory abort cleanup, which closes R1's P3-3.

Doc 09 carries the requirements as MG-054–058. The other numbered and guide edits:

| Location | Change |
|---|---|
| Doc 09 | MG-003 and MG-010 gain admission and expiry cross-references. MG-020 gains the internal `evicted` and abort-fence classification. MG-030/032 are scoped to durable or retained records. MG-050/051/053 are qualified as durable activation and residency rules |
| Doc 17 | §17.4 carries the complete options block and policy type. The namespace/assembly row gains the policy. Retention is distinguished from the distinct-limit taxonomy. Shared start/wait prose carries the start-to-observe gap and the abort fence. The authoring-only companion is explicitly excluded |
| Docs 03, 04, 06, 10, 11, 13, 15 | Qualify activation, rehydration, terminal memory and start-key lifetime. Add PR-040 ownership. Separate NF-031 residency caps from the retained-data count. Record the policy outside the deferred destructive-command set. Add OB-026 telemetry classification |
| Doc 12 | AC-533–542, all explicitly source-pending |
| Guides | Every guide edit marks the feature as approved but not implemented, and warns against configuring it with current binaries |

The deferred registry entries for archive/purge/history in docs 09/13/17 are retained unchanged. No
removed concept is reintroduced.

### Guard adaptations

- **`DagStreamIdentitySliceGuards`.** The DAG no-source tripwire stays closed for `src/`, DAG deltas
  and API baselines. Under `openspec/specs/` it admits only the two exact files, and only when each
  equals `prior + "\n\n" + ADDED blocks + "\n"`. Probes Q3–Q6 confirm it still rejects every other
  canonical change.
- **`NormativeContractGuards`.** It maps exactly one old ownership-row string to the new one. That
  keeps `v1-public-contract.json`, an input to package-source provenance, unchanged until 4.2, as R1's
  F3 requires.
- **`OpenSpecCorpusGuards` (schema 12).** `ValidateEphemeralRetentionSourcePending`:
  - re-derives the checkpoint's parent and tree, the child's single-path diff, and the verdict bytes
    and APPROVE line through git;
  - checks the requirement identities against the deltas, and the canonical blocks against the delta
    blocks;
  - checks the literal task states and owned numbered paths;
  - checks the null canonical/source approval fields.

  Completed-record and DAG predecessor rules are untouched.

  CI checks out with `fetch-depth: 0`, and `master` merges are true merge commits (for example
  `021b2bd` and `1c86c23` have two parents). The pinned objects therefore remain reachable after a
  normal merge.

### Carried P3 dispositions (from the R1 approval): all resolved

| R1 note | Resolution |
|---|---|
| P3-1 integration | Base deliberately unchanged; see P3-3 below |
| P3-2 proposal | `proposal.md` gains an explicit both-modes abort-correction bullet |
| P3-3 scenario | Narrowed, and re-reviewed here |
| P3-4 telemetry | OB-026 plus AC-542 classify the abort fence as internal and exclude it from caller termination command metrics |
| P3-5 per-hit occurrences | 499 individual decisions; see section 4 |

## 4. Occurrence ledger (all 499 decisions)

`task-2-1-occurrence-ledger-2026-10-16.tsv` is 499 data rows, 294,432 B, `d275769f…`.

### Mechanical audit, every row

- Its path:line multiset equals the R1 record's exactly.
- Every baseline literal equals that line at `f1eb86e`.
- All 486 same-literal rows equal the current line.
- All 13 replaced ranges (`/`-joined) equal the current lines at their stated start.
- Every Edit or Augment row is in a changed file.
- All 499 rationales are distinct.
- Decision counts: 449 Retain, 33 Augment, 17 Edit.

### Semantic review

I read every decision in these groups:
- the numbered specs: 01–17, the matrix and the companion;
- all canonical capabilities;
- all 50 Augment and Edit rows.

I sampled the repository-history, implementation and archive-pointer Retains.

Each Retain I read was sound:
- durable-scoped text (DU-050/051, MG-052, PR-022, AC-311 in the Durable section, JS-AC-009);
- strong-value or signature identifiers, where I confirmed the doc 17 handle owners at lines 2490
  (ephemeral) and 2520 (durable);
- still-deferred destructive commands;
- canonical blocks that the appended requirements qualify without contradiction.

A separate grep of the current tree found no surviving ephemeral claim of "always queryable",
"never evicted" or permanent retention. The root `README.md` and samples carry no conflicting
lifetime claim.

**One bookkeeping slip.** Row 209 (doc 09:114) is labelled `Retain`, but its line was edited to add
"for durable records". The meaning is preserved, so it should read `Edit`. This is P3-1.

## 5. Remaining P3 notes (non-blocking; carry them forward)

- **P3-1 — Ledger label.** Correct row 209 to `Edit` in the next target that touches the ledger. Do
  not rewrite this frozen record in place; record the correction there.
- **P3-2 — Doc 17 §17.4 signatures are unguarded.** Probe Q12 deleted `TerminalRetention` from
  the §17.4 options block and the full lane stayed green. Doc 17 is not among Task 7.3's pinned sources,
  and the ownership guard reads only the table row. The checkpoint commit freezes the text, but no
  executable guard protects the primary signature owner. Close this no later than slice 4.2, for
  example by comparing the §17.4 host-options and policy declarations with the executable public-API
  baseline, or by pinning doc 17's content.
- **P3-3 — Guaranteed integration conflicts.** `master` (`021b2bd`) also rewrites row 11 of the
  Task 7.3 table (`docs/production-readiness.md`, old cell `1af7480c…`), the
  `Task73DocumentationArtifactSha256` constant, and `docs/production-readiness.md` itself. Any move
  toward `master` needs a reviewed integration target that:
  - recomputes those hashes from the merged content;
  - adds a dated refresh note like `master`'s 2026-10-16 note, which this target's refresh lacks;
  - reruns the full lane.

  Merge with a merge commit, never a squash: the schema 12 validator resolves `5501cad` and `3a08796`
  through git.
- **P3-4 — Temporary ownership-row shim.** When 4.2 updates `v1-public-contract.json` to the new
  family, delete the hard-coded old-to-new mapping in `NormativeContractGuards`. Name this in task 4.2.
- **P3-5 — Archival coupling.** `DagStreamIdentitySliceGuards` reads this change's deltas by their
  active path. Archiving `add-ephemeral-terminal-retention` would break it, so resolve the path through
  the archive-aware change lookup when the change is archived, or when the DAG tripwire is replaced.
- **P3-6 — Stale "pending" guide text after source ships.** Tasks 3.x–5.x do not own replacing the
  "approved, implementation pending" text once the API and runtime exist. Add guide ownership to
  4.5 or 5.3. The text is in:
  - the developer guide, production readiness, prerelease notes, technical overview, end-to-end plan,
    diagrams and conventions;
  - the doc 13 and doc 17 status notes;
  - the README issue line.

  CLAUDE.md requires guides to be updated with the contract, and these would otherwise go stale.
- **P3-7 — Wording that ages.** "This canonical-transition draft does not yet ship…" in the developer
  guide stops being a draft at checkpoint. Reword it at the binding target or at 4.5.

## 6. Hygiene

- **Read-only target.** The target worktree was only read, apart from writing this file. This file is
  outside the frozen 38-path manifest and must not enter the checkpoint tree.
- **Probes.** All probes ran in `E:/ocv194`, and every script asserted that location. The copy was
  restored to the frozen content record.
- **Removal.** Remove the copy with `git worktree remove --force E:/ocv194`; it contains build outputs
  and the package feed. Probe backups and logs are at `E:/ocv194-bk` and `E:/ocv194-*.log`/`.txt`.
