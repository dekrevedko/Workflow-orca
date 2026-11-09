# Issue #16 task 3 native evidence for independent review

**Qualification hold.** All 392 runtime cases passed. Both JIT jobs succeeded. Four trimmed or Native AOT jobs rejected the diagnostic identities, so GitHub skipped the aggregate job. A replay of the original protocol 4 gate rejects the captured reports with the same diagnostic error.

GitHub Actions run [42552942701, attempt 1](https://github.com/dekrevedko/Workflow-orca/actions/runs/42552942701) used the temporary validation commit `4f43e33f74d608eaef0392b195dbf7fdc36661b1`, with parent `5960f0b5e920e2a53b34e38290d20c6e545fbf2a` and exact source tree `4e7b4300446664819ca364495483f92393118975`. The 64-path source freeze is still byte-for-byte intact. The issue branch remains at the canonical verdict commit and this evidence is uncommitted.

| Host | Profile | Runtime cases | Job | Warning rows |
| --- | --- | ---: | --- | ---: |
| windows-2025 | JIT | 84/84 | [success](https://github.com/dekrevedko/Workflow-orca/actions/runs/42552942701/job/128952828874) | 0 |
| windows-2025 | Full trimming | 56/56 | [failure](https://github.com/dekrevedko/Workflow-orca/actions/runs/42552942701/job/128952828719) | 4 |
| windows-2025 | Native AOT | 56/56 | [failure](https://github.com/dekrevedko/Workflow-orca/actions/runs/42552942701/job/128952828640) | 8 |
| ubuntu-24.04 | JIT | 84/84 | [success](https://github.com/dekrevedko/Workflow-orca/actions/runs/42552942701/job/128952828686) | 0 |
| ubuntu-24.04 | Full trimming | 56/56 | [failure](https://github.com/dekrevedko/Workflow-orca/actions/runs/42552942701/job/128952828663) | 4 |
| ubuntu-24.04 | Native AOT | 56/56 | [failure](https://github.com/dekrevedko/Workflow-orca/actions/runs/42552942701/job/128952828642) | 8 |

The metadata consumer adds two failure-settlement dispatch arms above `HandmadeContext.GetTypeInfo`. The existing handwritten metadata counterexample moved from materialized lines 363 and 365 to 365 and 367. For every IL2026 and IL3050 row, restoring the earlier line number in the normalized message reconstructs exactly one prior reviewed ID with the same code, column, text and count. Removing the two arms reconstructs the earlier materialized Program.cs SHA-256 recorded in the prior decisions. The handwritten-rejection test and the new failure-settlement witness passed in all six cells.

The full 24-row comparison is in `diagnostic-comparison.json`. A separate in-memory comparison with the historical diagnostic coordinates passes the other matrix checks. The stored job results, reports and qualification verdict retain the actual failure. The warning decisions require independent review before a revised candidate can qualify.

The downloaded ZIPs match GitHub’s published digests. Every report binds the same 360 source inputs, 23 harness inputs and 12 prerelease package hashes. The three restored product DLLs for each journey and metadata consumer match the shared feed. Trimmed and AOT cells recorded reflection disabled, and both AOT cells recorded dynamic code disabled. The original journeys and new type, failure and settlement controls passed.

The evidence archive contains the raw six GitHub cell ZIPs, Actions run and job records, artifact inventory, package and input verification receipt, diagnostic comparison and original source freeze. The feed bytes are available from the linked GitHub run. Archive SHA-256 `1484df261322b58b526179b09975f57264003a043bde204a3676864868a48903`. [Evidence archive](task-3-native-evidence-2026-11-08.zip)

The supplemental freeze record binds the two evidence files to the original 64 paths. Review the diagnostic comparison and the warning gate before any correction and hosted rerun.
