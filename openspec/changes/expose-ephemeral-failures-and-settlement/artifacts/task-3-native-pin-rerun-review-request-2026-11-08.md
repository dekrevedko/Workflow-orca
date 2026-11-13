# Issue #16 diagnostic pin rerun for independent review

**Hosted qualification passed.** Run [42554435708, attempt 1](https://github.com/dekrevedko/Workflow-orca/actions/runs/42554435708) passed all six cells and the [aggregate job](https://github.com/dekrevedko/Workflow-orca/actions/runs/42554435708/job/128958102389). All 392 runtime cases passed. The local replay of the original protocol 4 gate passed against the six raw reports.

The validation commit is `d859a9515f47ff8b786679166b9c584e38b6ecc5` with parent `5960f0b5e920e2a53b34e38290d20c6e545fbf2a` and tree `5ca0cd451c92065e9de64d4202fe1fc1db425852`. Relative to the prior review tree `ba2e6b44dbe97a9fd14b8405745010d04f727095`, the only changed path is `tools/ephemeral-native-assessment/support_v2/diagnostic-decisions.json`. The issue worktree holds the source and evidence for independent review.

| Host | Profile | Runtime cases | Job | Warning rows |
| --- | --- | ---: | --- | ---: |
| windows-2025 | JIT | 84/84 | [success](https://github.com/dekrevedko/Workflow-orca/actions/runs/42554435708/job/128957563837) | 0 |
| windows-2025 | Full trimming | 56/56 | [success](https://github.com/dekrevedko/Workflow-orca/actions/runs/42554435708/job/128957563643) | 4 |
| windows-2025 | Native AOT | 56/56 | [success](https://github.com/dekrevedko/Workflow-orca/actions/runs/42554435708/job/128957563736) | 8 |
| ubuntu-24.04 | JIT | 84/84 | [success](https://github.com/dekrevedko/Workflow-orca/actions/runs/42554435708/job/128957563679) | 0 |
| ubuntu-24.04 | Full trimming | 56/56 | [success](https://github.com/dekrevedko/Workflow-orca/actions/runs/42554435708/job/128957563608) | 4 |
| ubuntu-24.04 | Native AOT | 56/56 | [success](https://github.com/dekrevedko/Workflow-orca/actions/runs/42554435708/job/128957563741) | 8 |

The previous run remains the failure record. Its raw logs supported a one-to-one mapping of 24 diagnostics from the existing handwritten metadata counterexample. The reviewed change rebinds those pins to the new line positions and materialized program hash, with the exact counts and classifications retained. The new run reproduced the same 16 IL2026 and 8 IL3050 warning rows at the reviewed positions, and the handwritten-rejection witness passed in every cell. [Prior review packet](task-3-native-review-request-2026-11-08.md)

The verifier checked the GitHub artifact digests, all 360 source inputs, all 23 harness inputs and all 12 prerelease package hashes. Each restored product DLL in the journey and metadata consumers matches the shared feed. The two runs have identical source inputs, with only the diagnostic decisions file changed among harness inputs. Trimmed and AOT consumers report JSON reflection disabled. Both AOT cells report dynamic code disabled.

The evidence archive includes the six original GitHub cell ZIPs, Actions logs and job records, the package and input verification receipt, the exact 24-pin change, and the prior review freeze. The package feed is available from the linked run.

Archive SHA-256 `e5a3b20eeb3638c7bf7f87f2071e639e3c19057f4234de25c2f30ae74adbaec2`. [Evidence archive](task-3-native-pin-rerun-evidence-2026-11-08.zip)

The supplemental freeze binds the revised pins and the two new review files to the prior 66 paths. The native execution result is ready for independent review. Task 3 remains uncommitted at the canonical verdict commit.
