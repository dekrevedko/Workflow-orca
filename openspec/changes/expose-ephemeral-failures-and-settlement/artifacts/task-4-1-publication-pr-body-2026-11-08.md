Ephemeral callers can inspect the exact original business-step exception on
`WorkflowOutputUnavailableException.InnerException` and read its detached type
name from `WorkflowFailure.ExceptionType`. An issued handle can observe
`WaitForSettlementAsync` before its application owner releases resources that a
step may still use. Output completion and physical settlement are separate.

Typed failures use the closed seven-field version-2 codec. All-null-type
failures keep the legacy five-field bytes. Durable engine-produced stored bytes
are unchanged, and durable handles reject settlement without provider I/O.

The Task 3 source checkpoint is `5a91477` and its verdict-only child is `8667ea5`.
The candidate feed passed 392 cases across Windows Server 2025 and Ubuntu 24.04
x64 in JIT, full trimming and Native AOT, including the aggregate. Qualification
covers the selected public ephemeral recipe and the exact phase0 candidate
packages. Preview.3 predates these APIs. [Native run](https://github.com/dekrevedko/Workflow-orca/actions/runs/42554435708)

Task 3 validation passed the warnings-as-errors build, 1,043 unit tests, 314
Infrastructure checks, PostgreSQL 104, SQL Server 75, integration 11, and 20
ephemeral stress runs. Engine coverage was 79.20%. The 22 focused mutations
were killed. Scoped ReSharper reported zero errors with warnings triaged.
[Review verdict](https://github.com/dekrevedko/Workflow-orca/blob/codex/issue-16-ephemeral-failure-settlement/openspec/changes/expose-ephemeral-failures-and-settlement/artifacts/task-3-independent-review-verdict-2026-11-08.md)

Closes #16
