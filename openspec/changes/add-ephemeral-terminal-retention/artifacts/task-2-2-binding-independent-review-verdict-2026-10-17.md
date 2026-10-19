# Issue #1 task 2.2 canonical binding — independent review verdict

**Date:** 2026-10-17
**Reviewer:** independent review
**Request:** `artifacts/task-2-2-binding-independent-review-request-2026-10-17.md` (not modified by this
review)
**Target:** branch `issue/1-ephemeral-retention`, base `3eee5165e94ef91a0a1ef6eb4ec4b80ae1ea528f`. The
target is the six entries named by `task-2-2-binding-dirty-manifest-2026-10-17.txt`: three tracked
modifications and three untracked files.

## Verdict: APPROVE

This verdict approves the exact frozen binding target:

- the two checkbox flips, for 2.1 and 2.2;
- schema 13, with both records at `CanonicalApprovedSourcePending` and bound to the actual transition
  evidence;
- the narrow canonical-evidence validator;
- the dated binding record.

There are no P1, P2 or new P3 findings. The seven P3s from the transition verdict remain carried
unchanged, as the binding record states.

### Authorized next actions, in this order

1. **Checkpoint.** Re-verify zero drift against section 1. Stage **exactly** the six manifest paths,
   leaving this verdict out. Confirm that the staged tree is
   `baf557450590d86178e041194002a32cedc2b04c` with parent `3eee516`. Commit on
   `issue/1-ephemeral-retention`.
2. **Direct child.** It adds this verdict byte-exact and nothing else. Report both SHAs and the clean
   state.
3. **No further activation commit is needed.** The checkpoint itself carries 2.1 and 2.2 as complete,
   and its tree is the one validated green here.

After steps 1–2, task 3.1 may be **prepared** as a test-first source slice under its own independent
review and checkpoint gate. Its target should record this binding checkpoint and verdict child in its
evidence. This verdict does not authorize any of the following:

- source or test code;
- executable API, compile or package baselines;
- canonical, delta, numbered-document or guide edits;
- a rebase or merge toward `master`, a push or a pull request;
- issue comments or closure;
- issue #2 or #3 work.

## 1. Freeze anchors

| Anchor | Value | Result |
|---|---|---|
| HEAD / index | `3eee516…`; nothing staged | Unchanged at start and end of review |
| Raw porcelain (`core.quotepath=false`, `--untracked-files=all`, ordinal, LF + final LF) | 6 lines, 544 B, `f88f2e72e8f1a7e30e6dc6803ce2873a1cc9bccc681b97752ba29dca630a2497` | Matches the author's report and the self-inclusive manifest |
| Content record (`status\tpath\tlength\tsha256`, LF + final LF) | 6 rows, 967 B, `dd53784688d96418cd3ca58b1d02fc82eecd6f7b7d80db54fe99d190c5ad2daf` | Identical at start and end of review |
| Candidate tree, staged in a disposable copy at the base | `baf557450590d86178e041194002a32cedc2b04c`; `git diff --cached --check` clean | Matches |

`origin/master` is still `1c86c23`. The target stays on the issue branch, as the request states.

## 2. Scope verification

**Task list.** `tasks.md` has identical length to the base (10,042 B). `cmp` reports exactly two
differing bytes, at offsets 1717 and 2864, each a space changed to `x`. They are the 2.1 and 2.2
checkboxes. Tasks 1.1–1.4 stay checked, and 3.1–5.4 stay open.

**Registry fixture.** The fixture changes only these values:

- the schema version, from 12 to 13;
- both records' `stage`, now `CanonicalApprovedSourcePending`;
- both `canonicalApprovalEvidencePath` values, which now name
  `task-2-1-transition-independent-review-verdict-2026-10-16.md`;
- one new `ephemeralRetentionCanonicalEvidence` object.

The new object carries checkpoint `d1d3cf6…`, parent `f1eb86e…`, tree `83a08eaa…`, child `3eee516…`,
verdict SHA-256 `67abed67…` and binding artifact `9620df73…`. `sourceApprovalEvidencePath` stays
null. `requiredStages` and all other records are unchanged.

**Untouched paths.** Nothing changes under `src/`, `docs/`, `openspec/specs/`, this change's deltas,
the PublicApi fixtures or the provenance fixture. Every earlier change artifact is byte-identical to
the base, including both prior verdicts. The binding artifact's LF-normalized SHA-256 equals its
fixture and guard pin, `9620df73d7809757833560bd51739186c2a58021b511a995dc49364b058b5698`. The
transition verdict on disk is still 14,826 B with SHA-256 `67abed67…`.

## 3. Validator review

`ValidateEphemeralRetentionCanonicalEvidence` is narrow and fail-closed.

- **Pinned values.** It compares the entire evidence record against values pinned in guard source.
- **Commit shape.** It uses `rev-list --parents -n 1` to require that the checkpoint and the child
  each have exactly one parent, and that those parents are `f1eb86e` and `d1d3cf6`. This is stricter
  than a `^` lookup, because it rejects merge parents.
- **Checkpoint tree.** It requires the checkpoint's tree to be `83a08eaa…`.
- **Child contents.** It requires the child's `diff-tree` to be exactly `A\t<verdict>`.
- **Ancestry.** It requires the child to be an ancestor of `HEAD`.
- **Verdict bytes.** It checks the verdict's raw bytes three ways: length (14,826), SHA-256, and
  byte equality with the git blob in the child. It also requires the `## Verdict: APPROVE` heading.
- **Git failures.** Every git call asserts a zero exit code, so a missing object fails the check.

The per-record checks move from requiring 2.1 and 2.2 open to requiring them complete. Each record's
canonical evidence path must equal the shared verdict path. These checks still pass through unchanged:

- source approval stays null;
- tasks 3.1–5.3 stay open;
- requirement identities and counts;
- canonical blocks equal to delta blocks;
- owned numbered and acceptance paths;
- the contract-evidence pins.

CI's `fetch-depth: 0` and the repository's merge-commit history keep these objects reachable. That is
P3-3 of the transition verdict: never squash.

## 4. Validation reproduced independently

I ran everything in a fresh short-path disposable detached worktree, `E:/ocv197`, at `3eee516`,
holding a byte copy of the six entries. Its content record reproduced as `dd537846…`.

| Check | Result |
|---|---|
| `openspec validate --all --strict` | 22 passed, 0 failed |
| `dotnet build OrcaCore.slnx -c Release -warnaserror` | 0 warnings, 0 errors |
| Exact package feed (Windows PowerShell) | built |
| Full `Disposition=Infrastructure` lane | **247 / 247 passed** |

### Mutation probes

Each probe below ran `CanonicalSynchronizationGate_EnumeratesCapabilitiesDeltasAndRequirementOwners`,
and I restored the raw bytes between probes.

| Probe | Mutation | Result |
|---|---|---|
| B0 | none | green |
| B1 | uncheck 2.2 | red (task-state transition) |
| B2 | uncheck 2.1 | red |
| B3 | check 3.1 early | red |
| B4 | stage rolled back to `ContractApprovedCanonicalReviewPending` | red |
| B5 | `sourceApprovalEvidencePath` fabricated | red |
| B6 | one record's canonical evidence path set to null | red |
| B7 | wrong checkpoint tree in the evidence object | red (pinned record) |
| B8 | approval child pointed at the contract child `3a08796` | red |
| B9 | transition verdict byte flipped, length preserved | red (SHA-256) |
| B10 | binding artifact edited | red |
| B11 | contract-approval verdict edited | red (existing contract pin) |
| Final | after all restores | green |

The copy's content record returned to `dd537846…`. No commit, stage or ref was created in the reviewed
repository.

## 5. Hygiene

The target worktree was only read, apart from writing this file. This file sits outside the frozen
six-path manifest and must not enter the checkpoint tree. All probes ran in `E:/ocv197`, and every
script asserted that location before doing anything.
