# Temporary native validation authority — 2026-10-21

The human explicitly answered “Prepare the GitHub Actions workflow” for the missing
Windows Server 2025 / Ubuntu 24.04 native hosts, then “Authorize temporary validation
branch” to the request to create and push/update `codex/issue-3-native-evidence`
solely for this matrix. The request explicitly preserved the uncommitted issue
branch and excluded PR and issue edits. That authorization covers validation
iterations on this one temporary branch; it is not task 2 semantic approval or
permission to checkpoint/publish `codex/issue-3-ephemeral-aot`.

The first temporary snapshot is `d8bb3c3ff12c48667a8dc3968eed4ffaf5b5fc16`, parent
`dbd829e2104fba70f6f678513fcaa08bf17a6045`, tree
`3785e1f5bfc1e38bfaa1ca33f901ae832ac48fac`. It was made through a private Git index
without moving issue HEAD or staging its worktree. The first native discovery run
is [39719579320](https://github.com/dekrevedko/Workflow-orca/actions/runs/39719579320).

Temporary validation snapshots are explicitly unreviewed evidence executions.
The final task 2 target still requires an independent verdict before checkpointing.
No PR was opened, issue changed or issue branch pushed by this authorization.
