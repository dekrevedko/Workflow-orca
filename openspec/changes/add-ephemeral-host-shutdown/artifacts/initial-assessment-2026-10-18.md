# Issue #2 initial assessment — 2026-10-18

Status: initial planning draft. No frozen target, independent approval, canonical
transition or source implementation is asserted.

## Completed repository operations

- PR #7 was live-verified OPEN, mergeable, with the approved head
  `a73a3b3e77fdc921b4e36822d0cc7949ed982519` and all three CI jobs successful.
- The owner's explicit instruction authorized a merge commit and issue closure.
  GitHub merged it as `fa88986aaac219c998b15e96748efdf2f245de51`, with ordered
  parents `1c86c239fe11b893cc9bd526ce2cf64472323024` and
  `a73a3b3e77fdc921b4e36822d0cc7949ed982519`. No squash or rebase occurred.
- Issue #1 is CLOSED as completed; issue #2 remains OPEN.
- Fetched GitHub master into local `master` and `origin/master`; both equal
  `fa88986aaac219c998b15e96748efdf2f245de51`.
- Created the managed worktree `issue-2-ephemeral-shutdown/Workflow-orca` and branch
  `codex/issue-2-ephemeral-shutdown` at that exact commit. No issue #1-only branch
  commit is missing from this GitHub master base. Other checkouts remain intact.

## Live issue and current source

Read the full [issue #2](https://github.com/dekrevedko/Workflow-orca/issues/2) body
and [pre-handle start clarification](https://github.com/dekrevedko/Workflow-orca/issues/2#issuecomment-6224478658).
The request is stop-and-join with honest incomplete/stuck outcomes, not forced
external process termination. The existing per-instance ownership from issue #1
is a prerequisite; the host-wide admission/join operation is still absent.

| Source owner | Finding relevant to this issue |
|---|---|
| Ephemeral hosting readiness service | `StopAsync` immediately returns a completed task |
| `EphemeralWorkflowEngine.Dispose` | Disposes timer services; no host admission closure or physical join |
| Facade `StartCoreAsync` | Holds `startGate` while awaiting execution; an active start may have no returned handle |
| `EphemeralInstanceLifetime` | Owns physical work and provisional publication; its settlement task may be replaced on later acquisition |
| Ephemeral/Core instance lanes | Per-instance serialization and queued work; no host-wide closed-and-drained proof |
| Workflow timer and retention services | Entered callbacks/dispatch/sweeps need joining as well as disposal |
| Query/output wait facade | Caller wait cancellation releases that observer, not workflow/host ownership |
| Resource/retry continuations | Can produce later commits and must participate in the final handoff barrier |

The design links the pinned Microsoft 10.0.9 host/DI implementations checked during
this assessment. A hosted StopAsync exception does not keep RunAsync's final disposal
from executing. Preventing arbitrary external disposal of caller-owned dependencies
cannot be promised solely by adding an engine disposer. The exact supported ownership
protocol and the public observation surface must be resolved before contract freeze.

## Acceptance routing

| Issue criterion | Planned evidence |
|---|---|
| Independently reviewed exact lifetime/API | Tasks 1.2–1.4, exact catalogue and approved checkpoint/verdict |
| No post-close new starts | Admission-race tests, pre-Init invocation counter, source barrier |
| Cooperative steps/waits settle in order | Deterministic root/branch/item, wait/delay/retry/resource cases |
| No owned callbacks or late commits after completion | Entered callback/dispatcher/sweep/commit barriers and stable host-wide join |
| Explicit incomplete/stuck outcome | Noncooperative attempts and blocking/throwing cancellation callbacks; retained physical slots |
| Dependency lifetime through successful join | Generic Host/direct-DI package consumer and disposable dependency sentinels |
| Concurrent/repeated lifetimes | Start/stop and observer races, pre-handle cancellation, local output-wait cancellation and fresh host repetitions |

## Draft provenance and authority boundary

The two draft delta files contain six new ADDED requirement identities: two in
`developer-facing-surface`, four in `management-and-querying`. None is canonical.
The existing fixture describes the merged baseline only: 189 rows, 49,879 bytes,
SHA-256 `0f56a33d2aa17e6848ac8f3bf4769ec1a8604d0dadb25a7f9f9b8092a673f459`,
zero pending baseline operations. Its semantic-eligible flag is not approval of
this working draft. This draft's six additions are pending and semantic approval
is false. Recomputed inventory/directory/artifact/guard pins are task 1.3, not done.

No Infrastructure result or new frozen candidate tree is claimed. The current
provenance pins have deliberately not been promoted while exact public API and
dependency-ownership decisions remain open. Before this can become a checkpoint
candidate, task 1.3 must include all prerequisites and the complete Infrastructure
lane must pass. Never commit this initial unrefrozen draft as an approved target.

Only task 1.1 is complete. Product source, tests, guides, canonical specs, API and
package baselines, earlier evidence, the ordinary index and issue #2's GitHub state
are unchanged. No issue #2 push or commit has been made. No build, dump, package
feed or additional disposable validation checkout was created for this assessment.

## Validation performed

- This change's strict structural validation: PASS.
- All active changes/canonical specs, strict structural validation: 23/23 PASS.
- Proposal/design/specs/tasks: all four artifact types exist. This is availability,
  not implementation authority or a completed contract decision.
- Existing tracked files and index: unchanged. The seven new files are confined
  to this change directory; only the initial assessment task is ticked.
- No build, unit, Infrastructure, consumer or coverage run is claimed for this
  initial draft. Task 1.3 must reconcile the six pending additions before a green
  checkpoint candidate can be frozen.
