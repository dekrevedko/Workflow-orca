# Harmonization task 5.2 independent review verdict

**Date:** 2026-08-21
**Reviewer:** independent audit-only reviewer, no prior involvement in this work
**Verdict:** **REJECT**

This is an independent verdict, not an implementation-owner statement. It reviews the exact frozen
bytes named in
`docs/review/harmonize-downstream-capability-specs-task-5-2-independent-review-request-2026-08-20.md`
and its self-inclusive manifest
`docs/review/harmonize-downstream-capability-specs-task-5-2-dirty-manifest-2026-08-20.txt`.

## Freeze anchors

### Pre-validation

```
git status --porcelain=v1 --untracked-files=all
```
produced the same 12 lines as the checked-in dirty manifest, byte for byte, in Git's emitted order
(diffed directly against the manifest file; zero differences).

```
git rev-parse HEAD                  -> ff11ead781f8fef343fafc6e6bc8307d746e4a05
git rev-parse HEAD^{tree}            -> 3baddd97a6919bf5f674daed33bc236496a234c2
```

Independently recomputed Decision 6 / content anchors (Python, UTF-8, no BOM, per the recipe in the
review request):

| Anchor | Claimed | Reproduced |
|---|---|---|
| Raw porcelain bytes / SHA-256 | 947 / `9a1cda49...5781b0f` | 947 / `9a1cda49...5781b0f` — MATCH |
| Normalized porcelain records / bytes / SHA-256 | 12 / 947 / same | 12 / 947 / same — MATCH |
| Content record bytes / SHA-256 | 1,793 / `86ffab08...685cd` | 1,793 / `86ffab08...685cd` — MATCH |
| Capability directories / SHA-256 | 16 / `5e8a9725...ebd5` | 16 / `5e8a9725...ebd5` — MATCH |
| Capability directories missing `spec.md` | 0 | 0 — MATCH |

### Post-validation

Re-ran `git status --porcelain=v1 --untracked-files=all`, `git rev-parse HEAD`,
`git rev-parse HEAD^{tree}`, and the raw-porcelain SHA-256 after all builds, tests, `openspec
validate`, and the deliberate mutation-test probes below (each probe was reverted, verified by
`git diff --stat` returning to the pre-mutation 14-line delta for the touched file). All anchors
reproduced identically: HEAD, tree, 12-entry worktree, 947-byte raw porcelain, SHA-256
`9a1cda496e71eb2f40ec02cb834478c3906df9b89d18b7461816a70845781b0f`. Zero drift.

## Commands and results

```
dotnet build OrcaCore.slnx -c Release --no-incremental -m:1 -warnaserror
  -> Build succeeded. 0 Warning(s), 0 Error(s)

dotnet build OrcaCore.slnx -c Debug --no-restore --no-incremental -m:1 -warnaserror
  -> Build succeeded. 0 Warning(s), 0 Error(s)

dotnet test .../OrcaCore.DeveloperSurface.Guards.csproj -c Release --no-build --no-restore
  --filter "Disposition=Infrastructure"
  -> Passed: 211, Failed: 0, Total: 211

dotnet test .../OrcaCore.DeveloperSurface.Guards.csproj -c Release --no-build --no-restore
  --filter "Disposition=ExpectedRed"
  -> Failed: 14, Passed: 0, Total: 14 (all 14 are the named Section 8 executable-driver gaps,
     e.g. 3.10/opaque-direct-output-validation, 3.11b/terminal-report-inside-lease,
     3.10/fixed-codec-input-once, 3.10/ordered-snapshot-output-wait-cancel,
     3.9/ambiguous-create-or-observe, 3.10/parked-node-admission, etc.)

dotnet test .../OrcaCore.ProviderCertification.csproj -c Release --no-build --no-restore
  -> Passed: 96, Failed: 0, Total: 96

openspec.cmd validate --all --strict
  -> Totals: 18 passed, 0 failed (18 items)

git diff --check
  -> exit 0; output is only CRLF/LF line-ending advisories, no whitespace errors
```

All five lanes match the claimed table exactly: Infrastructure 211/211, ExpectedRed exactly 14/14,
ProviderCertification 96/96, OpenSpec strict 18/18, both builds 0/0.

## Independently re-derived canonical/provenance evidence

### Canonical synchronization (claim 1 and 2)

- `git diff --stat` confirms exactly 3 canonical spec files touched:
  `management-and-querying/spec.md` (+6/-1), `quality-and-verification/spec.md` (+43/-14),
  `repository-foundation/spec.md` (+14/-5).
- Extracted every `### Requirement:` block from each canonical file before/after and from the
  authoritative `openspec/changes/reshape-developer-facing-interfaces/specs/**/spec.md` deltas,
  normalized to LF, and compared programmatically:
  - `management-and-querying`: 1 block changed (`Management API is scope-oriented and fluent`) —
    canonical text byte-identical to the delta's `## MODIFIED Requirements` block. **MATCH.**
  - `quality-and-verification`: 5 blocks changed (`Known workflow-engine failure patterns stay
    covered`, `Every code and test removal has complete burden-of-proof evidence` [added],
    `Selected-mode capability separation is compile verified`, `Typed workflow contract is compile
    and behavior verified`, `Exact facade, event, hosting, and path-token contracts are guarded`) —
    all 5 byte-identical to the delta. **MATCH.**
  - `repository-foundation`: 2 blocks changed (`Documented application packages are sufficient`,
    `Package topology and ownership are exact`) — both byte-identical to the delta. **MATCH.**
  - Total: exactly 8 requirement-level blocks, matching the claimed 1 + 5 + 2 = 8.
- Requirement order: a `SequenceMatcher` diff of heading order before/after
  `quality-and-verification/spec.md` shows a single `insert` of the new requirement at its correct
  position (index 10) with everything else `equal` — no reordering, no incidental edits elsewhere.
  Canonical order for `repository-foundation` likewise shows the two changed requirements remain in
  their original positions among all 10 headings.
- Preambles (`## Purpose`) were not in any changed diff hunk for any of the three files (diff hunks
  all begin at or after the first `### Requirement:` line) — unchanged.

### Provenance record (claim 3) and preamble record (claim 4)

`tests/OrcaCore.DeveloperSurface.Guards/Fixtures/openspec-provenance-checkpoint.json` was read
directly and cross-checked against the artifact text and the request's claims:

| Field | Claimed | Fixture |
|---|---|---|
| recordCount | 176 | 176 |
| recordBytes | 46,211 | 46211 |
| recordSha256 | `e1420f36...da509b4021b` | `e1420f36...da509b4021b` |
| synchronized (derived) | 173 | 173 (176 − 0 pending − 3 outside) |
| pendingCanonicalOperations | 0 | `[]` (0) |
| activeNewCapabilitiesOutsideCanonical | 3 (`spec-driven-planning`) | matches |
| semanticApprovalEligible | true | `True` |
| canonicalPreambleCount/Bytes/Sha256 | 14 / 1,233 / `595528c6...ecd3c` | 14 / 1233 / `595528c6...ecd3c` |
| capabilityDirectoryCount/Bytes/Sha256 | 16 / 1,321 / `5e8a9725...ebd5` | matches (also independently recomputed above) |
| historicalCanonicalRemovalCatalogSha256 | `81c06519...15008ebd` | `81c06519...15008ebd`, 11 entries |

This is not a tautological fixture-vs-fixture check: `OpenSpecCorpusGuards.cs`
(`CanonicalSynchronizationGate_EnumeratesCapabilitiesDeltasAndRequirementOwners`, verified by
reading the source at lines 34–200+) walks the live `openspec/specs/*/spec.md` and
`openspec/changes/*/specs/*/spec.md` trees, computes the record fresh, and asserts equality against
this fixture. That test is part of the 211/211 Infrastructure pass above, which is therefore a real,
independent reproduction of every count and hash in this section — not a recorded claim taken on
faith.

### Permanent historical-removal catalog (claim 5)

Confirmed via the same fixture: `historicalCanonicalRemovalCatalogSha256 =
81c06519ae95846b697df5e895e6bcbc9792c3e36afbe441529b3add15008ebd`, `historicalCanonicalRemovals`
length 11. **MATCH**, and reconfirmed green by the passing Infrastructure lane.

## Mutation-tested safeguards (claim 6)

1. **`actions/checkout@v5` detection** — extracted the guard's exact regex
   (`actions/checkout@[^ \t\r\n]+`) from `OpenSpecCorpusGuards.cs` line 1105 and ran it in Python
   against `.github/workflows/ci.yml` with `actions/checkout@v4` textually replaced by `@v5`: both
   checkout steps are still detected. **Disposition: effective, version-agnostic.**
2. **Missing `fetch-depth: 0` fails** — removed one `fetch-depth: 0` line from a copy of `ci.yml` in
   memory and re-ran the guard's `OnlyContain` predicate: it returns `False` (not all checkout steps
   have `fetch-depth: 0`), which drives `Should().OnlyContain(...)` to fail the assertion.
   **Disposition: effective.**
3. **Purpose-only rewrite fails the infrastructure lane** — this was executed for real, not merely
   reasoned about: appended a sentence to `openspec/specs/repository-foundation/spec.md`'s `##
   Purpose` paragraph (no requirement text touched), ran
   `dotnet test ... --filter "FullyQualifiedName~CanonicalSynchronizationGate_..."`, and it failed
   with `differs at index 7` on the `repository-foundation` canonical-preamble hash, inside
   `ValidateCanonicalCapabilityInventory`. The file was then restored from a pre-mutation backup and
   `git diff --stat` was confirmed to return to the original 14-line (+9/-5) delta with no residual
   probe text. **Disposition: effective, mutation-proven.**
4. **Raw manifest order preserved, not path-sorted/status-grouped** — `diff <(git status
   --porcelain=v1 --untracked-files=all) docs/review/.../task-5-2-dirty-manifest-2026-08-20.txt`
   produced no differences: the manifest is Git's live emitted order, not a resorted copy.
   **Disposition: confirmed empirically.** Note: no C# guard in
   `OpenSpecCorpusGuards.cs` enforces this property automatically (only the Decision 6 procedure and
   manual reproduction do); this is a documentation/process control, not an executable one, which is
   consistent with how the review request describes it but is worth naming as a residual manual
   dependency for a future task.

All four safeguards check out as claimed.

## Gate and scope accounting (claim 7)

- `tasks.md` line 123 shows `- [x] 5.1 ...` recording "all 42 vocabulary-bearing canonical mismatches
  ... `developer-facing-surface` (7), `durable-persistence-and-outbox` (5), `durable-runtime` (8),
  `event-routing-and-waits` (8), `state-driven-runtime` (2), `workflow-authoring` (3), and
  `workflow-contracts` (9)" as unchanged and complete. Line 150 shows `- [ ] 5.3` still open.
  **MATCH** with claim 7.
- No `src/**` path appears anywhere in the 12-entry manifest or the live `git status` output.
  **MATCH.**

## Findings, ordered by severity

### P1 (blocking) — Missing Task 5.1 independent-approval verdict; unresolved checkpoint-provenance gap

`docs/review/` contains
`harmonize-downstream-capability-specs-task-5-1-independent-review-request-2026-08-20.md` and its
matching dirty manifest, but **no**
`harmonize-downstream-capability-specs-task-5-1-independent-review-verdict-*.md` exists anywhere in
the repository. A repo-wide search for `task 5.1` approval language across `docs/`, `openspec/`, and
`tests/` finds only the review *request*, `tasks.md`'s own self-reported "Completed" note (which
records what was implemented, not that it was independently approved), and the task-4.3 artifact,
which treats "harmonize task 5.1 is complete" as an input fact without citing an independent
verdict for the 5.1 checkpoint itself. `git log --all` shows no commit trailer, tag, or note
recording an approval for `ff11ead` (task 5.1's own checkpoint commit) — contrast this with the
task-4.2 artifact's explicit "Checkpoint process correction" section, which *does* disclose a dated,
named retroactive owner approval for commit `12de180` when its committed target drifted from its
originally reviewed one. No equivalent disclosure exists for task 5.1.

This project's own `CLAUDE.md` states: "A phase is not complete merely because its tasks and
validation are green. Completion requires an approval for the exact frozen target followed by a Git
commit containing that approved target," and "Do not accumulate multiple approved phases in one
dirty worktree. The next phase remains blocked until the preceding approved phase has its checkpoint
commit." Task 5.2's own frozen target is defined as `HEAD` (`ff11ead`) plus the task-5.2 manifest —
i.e., it is built directly on top of the task-5.1 checkpoint commit as its base. Per this repository's
own mandatory-checkpoint discipline, `ff11ead` itself required a prior independent approval before it
was permitted to be committed. No such approval is discoverable anywhere in this repository.

**Disposition:** this is classified as a blocking checkpoint-provenance failure, not a stylistic gap.
Approving task 5.2 now would create a second commit stacked on an already-unverifiable checkpoint,
compounding the gap and making it materially harder to unwind later if task 5.1's own approval is
never produced or is found deficient. The correct remediation is to locate or reconstruct an
immutable task 5.1 verdict (or obtain and disclose an explicit, dated retroactive owner approval for
`ff11ead`, following the precedent already used for `12de180` in the task-4.2 artifact), commit that
disclosure, and only then re-submit task 5.2 for independent review.

No other blocking (P1) or high-severity (P2) findings were found: every re-derived hash, count, test
result, and mutation-test outcome for the substantive task 5.2 content matched the claims exactly.

### P3 (non-blocking, for future task attention)

- The "raw manifest order preserved" property (claim 6, item 4) is verified manually/procedurally
  per review but has no dedicated executable guard in `OpenSpecCorpusGuards.cs`. A future
  regression (someone checking in a path-sorted manifest) would not be caught by the Infrastructure
  test lane, only by a reviewer rerunning the `diff` shown above.

## Verdict

**REJECT**

The substantive Task 5.2 canonical-synchronization content, all validation lanes, and all four
mutation-tested safeguards independently reproduce exactly as claimed with zero drift before and
after validation. This review is rejected solely on the P1 checkpoint-provenance finding: the
absence, anywhere in the repository, of an immutable independent-approval record for Task 5.1's own
checkpoint commit `ff11ead`, which Task 5.2's frozen target is built directly on top of. This
verdict does not authorize the Task 5.2 checkpoint commit, Task 5.3, the harmonization exit gate,
archival, or reshape Task 8.0.
