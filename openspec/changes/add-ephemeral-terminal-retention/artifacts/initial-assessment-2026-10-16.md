# Issue #1 initial assessment — 2026-10-16

Status: **draft contract prepared; no independent approval, canonical transition, source implementation, checkpoint, publication, or issue closure**.

## Baseline and issue order

- Repository: `dekrevedko/Workflow-orca`; GitHub's actual default branch is `master`. A live `git ls-remote --heads origin main master` returned only `master`.
- `git fetch origin --prune` completed. Worktree HEAD and `origin/master` both resolved to `370b239402c8974a45fc0a54f539a823ab470cf4` when the branch was created.
- Managed worktree: `issue-1-ephemeral-retention/Workflow-orca`; branch: `issue/1-ephemeral-retention`. Original checkout branch and tracked files are preserved.
- Read the full issue body/discussion live: #1 has no comments. All three open issues are enhancements labelled `needs-triage`; no open PR already owns them.
- Order: #1 bounded terminal retention, then #2 cooperative host shutdown, then #3 Native AOT/trimming assessment. Every issue gets its own branch from the freshly synced default branch; later issues have not started.

## Finding and concrete draft

The current facade retains `starts` and `instances` independently of the runtime registry. Internal terminal eviction removes runtime entries without coordinated facade/key retirement. Physical attempt tasks can remain alive after logical timeout; the facade output waiter uses commit notifications. These live source paths substantiate a missing lifetime contract, not a measured production memory leak.

The proposal/design/deltas specify an optional immutable host policy, unchanged disabled defaults, host-wide settled-terminal count/age limits, coordinated payload/binding/key removal, protected physical work, stale-handle not-found behavior, and pin-protected detached results for already admitted queries/waiters. Tasks define the approval, canonical transition, deterministic tests, source slices and fresh package-consumer experiment. Proposed signatures are review material and are not available in released packages.

## Validation actually performed

| Check | Result | Scope/limit |
|---|---|---|
| `openspec.cmd validate add-ephemeral-terminal-retention --strict --no-interactive` | PASS | Draft structure only |
| `openspec.cmd validate --all --strict --no-interactive` | PASS: 22/22 | 8 changes + 14 canonical specs; structure is not approval |
| `openspec.cmd status --change add-ephemeral-terminal-retention` | 4/4 artifacts complete | Artifact availability only; tasks remain gated |
| Release build performed by focused guard command | Completed without reported warnings/errors | Guard project and its referenced product/behavior projects; not a full solution build |
| Focused canonical synchronization guard below | EXPECTED FAILURE: 1 failed / 0 passed | Pinned fixture expects 184 provenance rows; draft has 189 |
| Isolated-index stage and `git diff --cached --check` | PASS | All nine new draft files included; ordinary index untouched |
| Proposed routing JSON and candidate provenance recomputation | PASS | Five unactivated identities; candidate hash unchanged after final prose/task edits |

Final scope is the nine files listed in the contract review request. The incidental one-line `openspec/config.yaml` created by the CLI was inspected and removed; it adds no required issue behavior. Other publishing changes visible in the original checkout at the final check were left untouched. No product/test/canonical tracked file changed in this worktree.

Focused command:

```powershell
dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj -c Release --filter FullyQualifiedName~CanonicalSynchronizationGate_EnumeratesCapabilitiesDeltasAndRequirementOwners --nologo -v minimal
```

The failure is `Expected collection to contain 184 item(s), but found 189`, in `ValidateChangeToCanonicalProvenance`. It reaches provenance comparison after validating requirement ownership; the five added rows are `PendingAddition`. The existing fixture/guards were deliberately preserved. This one failure does not establish the result of later assertions or other infrastructure tests. No repository-wide test run, product regression result, consumer allocation result, or independent review is claimed.

## Candidate provenance inventory

Recomputed from all active deltas and canonical blocks using the checked-in guard's parsing, LF-normalized UTF-8 block hashes, superseded-predecessor classification, ordinal row ordering, tabs, and final newline. The retained 184-row subset matches all three existing fixture values exactly; the draft contributes only five new rows.

| Inventory | Rows | UTF-8 bytes | SHA-256 |
|---|---:|---:|---|
| Existing approved fixture and recomputed unchanged subset | 184 | 48504 | `fd6ad0449e706d5ecf3f9dab552a9ad5fc3ef1be95df13304afba91e1d9ca8c8` |
| Candidate with this draft | 189 | 49579 | `b717202106d488a443c949a724952df9ac0b91e1ccbb5e7e98f88bd0e166f311` |

Unresolved-operation table (all operations `ADDED`, capability declaration kind `MODIFIED`, canonical block hash absent):

| Capability | Exact requirement heading | Delta block SHA-256 |
|---|---|---|
| `developer-facing-surface` | Ephemeral terminal retention policy is explicit and host-owned | `3c0163a860f5ee958de7957f92de4b04412976fc2edc9826b369c86e40078a18` |
| `management-and-querying` | Ephemeral terminal retention requires physical settlement | `c2d7eb8e4547c83422b8f33a60ca519e45e7ca77d7e25d1e4ad8057a3d855e11` |
| `management-and-querying` | Ephemeral terminal retention retires one complete ownership unit | `0db89b83ef433ea9b25c4778a39ea6a4efe9dcd7e573a77d7b00904afc47ccc2` |
| `management-and-querying` | Retired ephemeral handles have deterministic outcomes | `94260d122100bd920c7d9f0a6b4c03dfde4332db7943fb91e8830351d9579da2` |
| `management-and-querying` | Ephemeral start idempotency expires with atomic retirement | `58e479b5f25e3c5d051c5e7953c9059f1b4a84bf9a46f73e427803e29b165fae` |

Semantic approval eligibility: **false**. Pending additions: `developer-facing-surface` (1), `management-and-querying` (4). Task 1.2 owns process/provenance preparation; task 2.2 owns eventual approved canonical synchronization. The existing fixture's `semanticApprovalEligible: true` describes its approved baseline only and must not be asserted for this working tree.

## Authorized next step and blockers

Tasks 1.1 and the initial preparation in 1.2 are complete. Task 1.3 is open: freeze and independently review the complete contract/routing scope listed in `contract-review-request-2026-10-16.md`. That request contains reverse occurrence dispositions; `proposed-amendment-route-2026-10-16.json` supplies the exact unactivated identities, provenance and gate routing. Existing fixtures are not promoted. This assessment is not a substitute verdict. All issue acceptance criteria remain open. Do not implement source, synchronize canonical specs, or begin another issue phase until the applicable approval/checkpoint gate is satisfied.

This boundary comes directly from issue #1's acceptance criterion, "Independently review the contract amendment and exact public signatures under the existing source gates," and repository `CLAUDE.md`, "Do not commit while an independent review is pending because changing HEAD invalidates the frozen provenance." The source gates were verified live; historical memory did not supply implementation authority.
