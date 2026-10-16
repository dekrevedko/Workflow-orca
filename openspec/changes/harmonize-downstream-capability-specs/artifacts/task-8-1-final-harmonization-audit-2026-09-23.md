# Task 8.1 — final harmonization audit

**Date:** 2026-09-23

**Change:** `harmonize-downstream-capability-specs`

**Audit base:** `b660d2d5b58d4b06663dd24f6a94a9c71ab37342` (tree `ec1da3eaecc8b1c0193fef00b125f1d3bddbb323`)

**Authority:** diagnostic evidence for task 8.1, not an independent exit verdict or permission to run task 8.3.

## Prerequisites and disposition

The reduced harmonization change has only the `event-driven-prototype` and
`state-driven-runtime` delta capability directories. Its tasks 1.1–7.7 are complete, including
the distinct approval-evidence and activation checkpoints for task 7.7. The coordinating reshape
tasks 7.22 and 7.23 are complete. Reshape task 8.0 remains explicitly blocked until this change's
independent exit approval and checkpoint. Tasks 8.2 and 8.3 remain open.

## Exact canonical diff

The post-Section-7 reference is
`923ab0633cf565debee790e235d679b17f4e450d`; the audited canonical result is the tree at
`b660d2d5b58d4b06663dd24f6a94a9c71ab37342`. Reproduce the exact binary-safe patch with:

```text
git -c diff.algorithm=myers -c diff.indentHeuristic=false diff --no-ext-diff --no-textconv --no-color --binary --no-renames --no-indent-heuristic --diff-algorithm=myers --src-prefix=a/ --dst-prefix=b/ --unified=3 923ab0633cf565debee790e235d679b17f4e450d b660d2d5b58d4b06663dd24f6a94a9c71ab37342 -- openspec/specs
```

SHA-256 over that command's raw stdout is
`e07826ad8dfeb18d6ba4f48f1f0ea6d392ce96e44048ca3eebc521ff8a589453` (132,727 bytes).
The diff has 11 modified canonical files, 471 added and 107 removed lines, and no added or deleted
canonical file. The three unchanged capabilities are `event-driven-prototype`,
`runtime-resource-governance`, and `structured-fiber-execution`; the first was already synchronized
at the reference checkpoint. Each row below is `openspec/specs/<capability>/spec.md`.

| Capability | + / - | Reference blob | Audited blob |
|---|---:|---|---|
| `developer-facing-surface` | 77 / 16 | `22b5cb6ba7d27f69e731c544762284e9a05a6b6e` | `244e6c87cf3a6b58a88d9ed6c64f5041ecf6ef1d` |
| `durable-persistence-and-outbox` | 78 / 12 | `6401af35fad450f5220cd77a134d72a929aa35e6` | `6e8fed7f2762b63d7f5335f73a0d431aed30e945` |
| `durable-runtime` | 68 / 14 | `51a87be6adac0bdf4ab2a09772379d03f7f2de8e` | `f53f70e4d6c69f9e61d423357b4ac24dda1006cc` |
| `event-routing-and-waits` | 99 / 30 | `8d24a9b7e7f1b2585f6a223e7ee597e6f4b6133c` | `7fae043b99d962488597373ab9ee7d34aa4d1d25` |
| `management-and-querying` | 5 / 1 | `839c8a7c76ef96515b4f93eeeefcde3c1cce609b` | `c8e37b7dcae38fa27c9feaeeaf8122bd77fcad26` |
| `quality-and-verification` | 37 / 6 | `f6d297a15bfedeb008f840949c3af70aa21cdec4` | `d40d52c8eb9c90f3f524ae2469c7bc8f6c7a1dfe` |
| `repository-foundation` | 9 / 5 | `2cecd5272e50b074e4616fb6404287d75d1fae9e` | `2d39cd696aa24a8edfdd15246a2d139bab71b201` |
| `saga-orchestration` | 1 / 1 | `7b6240fc9db755851a3e7cfa5dedc4bc70e418e7` | `f021413ab707957d08e4256fc2f5406e94d4dc24` |
| `state-driven-runtime` | 10 / 7 | `7f7d0a52eb9210770aa899de185835c2f4fe98e5` | `f50425ce4c907893bcbc9688630d41d1d8a919f0` |
| `workflow-authoring` | 36 / 8 | `a64684927884d6c630e51b55962f27f7a996babf` | `de11a14bf76cf9203b91c4deafa121ab244a3ada` |
| `workflow-contracts` | 51 / 7 | `dad02bb009f538e8e15e222d81edab60aec8fa6b` | `62d01d6a9926c5e616495aeb0f0dc2be58bfd0f6` |

This is a net diff from a named committed reference, not a claim that all eleven edits belong to
harmonization. The approved reshape synchronization owns its requirements; harmonization owns only
its two unique deltas and the later, reviewed cross-tree citations.

## Corpus, ownership, vocabulary, and links

| Check | Audited result | Executable or reproducible basis |
|---|---:|---|
| Canonical capability directories / requirement headings | 14 / 190 | Enumerate `openspec/specs/*/spec.md` and `### Requirement:` headings; zero duplicate headings within a capability. |
| Active changes / delta requirement headings | 4 / 176 | Enumerate non-archived `openspec/changes/*/specs/*/spec.md`; zero duplicate `(capability, requirement)` owners across changes. |
| Delta-to-canonical provenance | 173 synchronized, 3 declared bootstrap-only, 0 pending, 0 duplicate owners | Recomputed by `CanonicalSynchronizationGate_EnumeratesCapabilitiesDeltasAndRequirementOwners`; `openspec-provenance-checkpoint.json` records 176 rows / 46,211 bytes / SHA-256 `ea8719e768182f4eea097cb280d3487426a9a7450ca7becb72616e567e30b756`. |
| Removed/deferred positive calls and stale Section 7B negatives | 0 current findings | Both `Task71_ActiveGuidesContainNoPositiveRemovedOrDeferredApiCalls` and `Task75_ActiveTreeRejectsRemovedDeferredCallsAndStaleSection7BClaims` pass; Task 7.5 also replays its exact 56 historical findings. |
| Active local Markdown links | 195 file targets, 8 fragment targets, 0 missing | Scan the 85 active Markdown files under `docs/`, `openspec/specs/`, and non-archived `openspec/changes/`, excluding immutable `docs/review/`, `docs/archive/`, and change `artifacts/`. Ignore fenced code and external URI schemes; resolve URL-decoded relative paths and GitHub-style heading slugs. |
| Active delta capability directories | 16, 0 without `spec.md` | Ordinal-sort repository-relative `openspec/changes/*/specs/*/` paths with trailing `/`; LF-join with a final LF. The 1,321-byte record hashes to `5e8a9725979d702ff2f639fef587828958c92533139602ab13f1401d6df7ebd5`. |

The link check is local-only: it does not fetch external URLs. Its eight fragment targets resolve
within the checked files, including the §13.4 registry and the quality/structured-fiber
requirement links from the acceptance criteria.

## Normative-source classification and task accounting

`docs/specs/` is the hand-maintained product requirements/acceptance package (19 normative files;
document 18 is one expressly non-normative guide), and `openspec/specs/` is the derived canonical capability
tree. Active `openspec/changes/*/specs/` are proposed deltas, not a third independently authoritative
tree. `docs/implementation/` is binding method/stack guidance (9 files). Active root guides,
observability guides (2), and the Orleans future-hosting note (1) explain the approved contract.
The 269 `docs/review/` records are immutable dated evidence, while 200 `docs/archive/` files are
historical; neither is scanned as current normative guidance. The active
`docs/normative-source-map.md` now labels those last two counts as a dated base snapshot rather than
a live assertion. That intentional edit refreshed only row 10 of the Task 7.3 22-source artifact,
whose normalized source SHA-256 is now
`1bc5a4182949cbfbe87870aebf258a9fdfcfb22a8cd627d2c0b0d7fc7e61d949`;
the artifact's reviewed normalized digest is
`532be8676350ae24e8ce2b19558e9850691d380600b3bddf914ece7f633fa5b9` and was refreshed
in guard source in this same target.

The same guard-source edit closes the two Task 7.7 review observations before the final exit
freeze. Historical record and archived-prompt worktree bytes are compared after the existing
CRLF-to-LF normalization; the archived prompt's committed `HEAD` blob still must equal the exact
move-commit blob. The archive index check now binds the predecessor path as the routing key to
both the archived prompt and its provenance record, instead of checking the destination link alone.
No immutable archived or review file was edited.

At the audited base, harmonization has 31 complete and 3 open tasks out of 34. Completing this
audit marks task 8.1 complete, leaving 32/34: task 8.2 requires a new immutable independent exit
approval, and task 8.3 is post-approval only. Reshape has 129/159 complete and 30 open; its task
8.0 remains blocked by this change's final approval/checkpoint gate. This audit does not change
product runtime source or canonical OpenSpec files.

## Validation and exit boundary

Debug and Release `dotnet build OrcaCore.slnx -warnaserror --no-incremental` completed with 0
warnings and 0 errors in each configuration. `openspec validate --all --strict` passed 18/18.
The focused canonical ownership and
vocabulary guards passed (1/1 and 2/2 respectively); the Task 7.3 source-digest guard passed 1/1
after its intentional refresh. The full `Disposition=Infrastructure` lane passed 226/226. The
separate `Disposition=ExpectedRed` lane produced exactly its 14 documented scenario failures and
no pass; these are pending reshape tasks 3.8–3.11b, not harmonization regressions.
Product and provider lanes passed: Core 350, Ephemeral 79, Durable 99, Acceptance 37, Hosting 24,
ProviderCertification 96, PostgreSQL 101, SQL Server 72, and Integration 11. The PostgreSQL,
SQL Server, and integration lanes ran against their configured real-container fixtures.

Task 8.1 is an audit, not an exit approval. Freeze this target under task 8.2, obtain a dated
independent verdict, and run task 8.3 only after an exact-target `APPROVE`.
