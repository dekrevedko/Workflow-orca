# Issue #16 Task 4.1 publication preparation R1 review request

Base `8667ea5e7894e7e64859070d7556594e559a4fe6` is the verdict-only child of the approved 69-path Task 3 checkpoint `5a9147721a0b8a0e7ffecacd07572a6ddebcd0ef` at tree `ed06e4888769fc4fceae8cf08122f25c722d0586`. R1 supersedes Task 4.1 tree `66e06f5ab0084495fda753272ad82c9dd50e1933` and keeps the same 12-path manifest.

R1 resolves the Closed/Stopping wording finding. Accepted workflows can drain with ordinary operations in Closed. Stopping rejects new live operations subject to the existing identity and not-found checks. An already-issued handle can observe settlement passively in both phases.

The developer guide dates the issue #13 source-preparation status and points to published preview.3 and its 384/384 exact-asset results. The native-support guide links to the updated heading. The binding refreshes those two documentation digests and its guard pin. [Preview.3 qualification](https://github.com/dekrevedko/Workflow-orca/actions/runs/40998543857)

The Task 3 checkpoint, verdict, PR draft, task ledger, source, baselines, and native evidence are unchanged from the prior Task 4.1 target. Task 3 qualification remains limited to the public ephemeral recipe and the matched phase0 feed. All 392 cases in the six native cells and the aggregate passed. [Task 3 qualification](https://github.com/dekrevedko/Workflow-orca/actions/runs/42554435708)

Fresh validation passed the warnings-as-errors Release build, the 12-package feed pack, OpenSpec 26/26, Infrastructure 314/314, all 15 Python gate tests and the historical manifest check. The binding guard rejected each stale document in isolation. The restored control passed.

Scoped ReSharper 2026.2.3.1 ran for Release/Any CPU/net10.0. It reported zero errors, 50 warnings outside the edited lines and 2 existing notes on edited lines. The shared task-ledger helper is called across classes. The explicit array in the cell-profile assertion is a style note.

The exact tree, content and status hashes and compact evidence are in `.artifacts/publishing-validation/issue-16-task-4-1-r1-2026-11-08/`.
Validation archive SHA-256 `be4c43581d4c944e4ffabeccfa0d568d58a636078d816f8c254b2ae6bddcfa3b`.

Tasks 4.1 and 4.2 remain open. The preparation is for checkpoint review. Push, PR, merge, issue closure and a future release are separately authorized.
