# Issue #20 durable handoff diagnostic review verdict

**Approved for checkpoint and PR.** A separate read-only Codex review verified the diagnostic change and its local evidence. The owner authorized committing the frozen tree, adding this verdict as its direct child, and opening a PR against master.

- Base `068667d9283ce1c97df7bf8b218fc7d6c15a833c`.
- Checkpoint `98ac116bb2586ad30b30005b06af3a6defc8b718` at tree `9ae30a4afee13adf0e444574cc24650c171f49a2`.
- Frozen content SHA-256 `60b2ca032570c0628e5548ce58c5abb85675779a87367e70763e26d20a9fd875`.
- Frozen porcelain SHA-256 `027c85ec648641aa1feec310a338cb866db20de8da957fbecb94aa65662406fd`.
- Local validation archive SHA-256 `11e8a2f4db1f34bda9275f6d6946617e1964452794244b566a7e0623d62305dc`.
- Hosted evidence archive SHA-256 `08c3b4d080e57fae9de50c57d3725688b2cc4d5508cb2b4ebf7985d9435ef162`.

The scenario still requires Completed and exactly one invocation. On failure it now reports the observed status, invocation count, and failure code. Forced mismatches in each condition confirmed rejection and the diagnostic output.

The Release build with warnings as errors, 12-package pack, OpenSpec 26/26, focused VSTest with coverage, Infrastructure 314/314, and 1,000 constrained replays passed. Scoped ReSharper reported zero errors and no warnings on edited lines.

Hosted Windows Infrastructure passed 314/314 on all three attempts with the exact candidate tree at validation commit `99e7bcc8126e41164c5bc9b887ea98c1f754cf25`. The original failure did not recur, so its failing values and root cause remain unknown. [Hosted validation](https://github.com/dekrevedko/Workflow-orca/actions/runs/42948404217)

Owner authorization in this chat, 2026-11-10 America/Los_Angeles

> Recheck tree 9ae30a4, commit the diagnostic and its verdict-only child, then push the issue #20 branch and open a PR against master. Wait for green CI, but don’t merge or close #20 yet.
