# Issue #1 task 2.1 transition — independent review request

Review the exact uncommitted canonical/numbered-document transition on
`issue/1-ephemeral-retention`, based on `f1eb86e31306d0aab88d8f84d38c925ef2eaf4c7`.
The contract chain remains `5501cad` → `3a08796` → `f1eb86e`. Tasks 1.1–1.4 are
checked; tasks 2.1 onward remain open. This request does not consume checkpoint or source
authority. Nothing has been rebased, committed, pushed, commented on or closed for this target.

## Exact scope and freeze

The self-inclusive native porcelain manifest is
`task-2-1-transition-dirty-manifest-2026-10-16.txt` beside this request. It includes
this request, itself, all changed normative/docs/process-guard paths, and the five new
registry/occurrence/provenance artifacts. The reviewer's verdict is not part of the target.

The accompanying freeze record supplied with this request pins native LF porcelain and
NUL porcelain, the full raw-content inventory, and the candidate Git tree from a copied
index at the base. The ordinary issue-worktree index remains empty. A short disposable
copy at `E:/ocv193` holds the exact manifest bytes; staged scope/tree and whitespace are
checked there. No parent other than f1eb86e belongs to this transition.

Freeze recipe: capture native `git -c core.quotepath=false status --porcelain=v1
--untracked-files=all`, also with `-z`. For each nonempty LF row, use its first two status
bytes and path after the third byte. Record `status<TAB>path<TAB>raw byte length<TAB>raw
lowercase SHA-256`; ordinal-sort the complete rows and append one final LF. Include every
manifest path, including both freeze/request files. Stage only those paths in a copied
index seeded from base, then run `git write-tree`. Git tree normalization is distinct from
the raw-content record; compare both. Recheck all anchors after validation.

## Review substance

- Canonical developer-facing-surface gains one requirement; management-and-querying gains
  four. Existing blocks/preambles remain unchanged. The omitted-policy scenario is narrowed
  to accepted instances under the carried P3 and therefore belongs to this new review.
- Numbered documents and guides reconcile accepted lifetime, settlement protection,
  provisional abort cleanup in both policy modes, atomic retirement/key expiry, wait pins,
  the start-to-observe gap, sizing, tiny sweep overhead and internal telemetry.
- Two permanent records bind actual contract checkpoint/verdict evidence while explicitly
  retaining `ContractApprovedCanonicalReviewPending`. Canonical/source approval fields
  remain null and implementation/consumer tasks stay open. Schema 12 adds this named stage
  without weakening completed-record or DAG predecessor rules.
- Current provenance is 189 rows / 49,879 bytes /
  `0f56a33d2aa17e6848ac8f3bf4769ec1a8604d0dadb25a7f9f9b8092a673f459`:
  178 synchronized, 8 superseded, 3 planning-only outside canonical, zero pending.
  Semantic eligibility describes working text equality, not a reviewer verdict or source approval.
- The predecessor provenance catalog has sixteen unchanged files. The Task 7.3 live table
  refreshes its permitted current-content hash cells; the review-manifest fixture removes
  only a stale current-worktree match. Existing dated contract/request/verdict files stay exact.
- Executable public-API baselines, positive compile fixtures, package-source records,
  product source and the authoring-only doc 17 companion remain unchanged. The exact
  normative hosting-row check is adapted without adding executable API availability.
  The DAG guard permits only the two exact append-only canonical targets and still rejects
  any prior canonical edit or unrelated capability change.

Read `task-2-1-occurrence-review-2026-10-16.md` for all five carried P3s and the numbered
document map. Review every one of the 499 source-bound decisions in
`task-2-1-occurrence-ledger-2026-10-16.tsv`: 449 Retain, 33 Augment, 17 Edit. Each has
its own rationale and old/current text. The old R1 template dispositions remain historical;
they are not the current review evidence. A unique rationale alone is not proof of correctness.

Master was verified read-only at `1c86c239fe11b893cc9bd526ce2cf64472323024`.
Moving this branch toward it later requires a new freeze and independent review.

## Validation and reproduction

The final freeze evidence accompanies this request with exit statuses and totals. Required
lanes are strict OpenSpec, Release warnings-as-errors, all Infrastructure guards, copied-index
tree/blob agreement, staged whitespace and zero drift. No product conformance or consumer
qualification is claimed by those lanes.

```powershell
openspec validate --all --strict
dotnet build OrcaCore.slnx -c Release -warnaserror --nologo -v minimal
& C:/Windows/System32/WindowsPowerShell/v1.0/powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/OrcaCore.DeveloperSurface.Guards/pack-exact-package-feed.ps1 -OutputDirectory artifacts/phase0-packages -NoBuild
dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj --no-build --no-restore -c Release --filter 'Disposition=Infrastructure' -v minimal
```

Use a fresh short worktree with full Git history at the exact base and copy/stage only the
manifest paths. Infrastructure includes package-only consumer builds, so populate the exact
local package feed first. Run builds and tests sequentially on Windows to avoid apphost locks.

Targeted disposable mutation probes reject: a false Complete stage, a future source approval,
a missing requirement identity, a missing normative owner, an early 2.2 checkbox, changed
approval-verdict bytes, edited pre-existing canonical text, an unrelated canonical capability,
and a wrong policy ownership row. Restore original raw bytes between probes and recheck the
candidate tree before the final green lane. The final evidence records actual probe results.

During preparation the initial full lane found three failures: the exact old ownership row,
the global DAG canonical prohibition, and the stale source-map current-worktree match. All
three are addressed explicitly in this target. One overlapping rebuild hit a Windows apphost
file lock; the subsequent sequential build passed. These diagnostic runs are not final evidence.

## Requested verdict and remaining gate

Return APPROVE or REJECT for this exact transition, including the two guard adaptations,
five carried P3 dispositions and all 499 occurrence decisions. An approval must explicitly
state checkpoint/activation authority for task 2.2; neither this request nor validation
authorizes a commit, task checkboxes, source slice, rebase, push or issue change. Source
tasks 3.1–5.3 require their own reviewed evidence after the transition checkpoint.
