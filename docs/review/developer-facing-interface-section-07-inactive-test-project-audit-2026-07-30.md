# Section 7 inactive-test-project audit

Date: 2026-07-30

Status: live remediation ledger; this is not an exit verdict or approval.

## Purpose

The Section 7 exit review identified five inactive test projects containing
146 `[Fact]`/`[Theory]` declarations. This ledger preserves every source file,
records its disposition, and prevents the reduced first-release lane from
silently converting exclusions into claimed retirement.

The recount command was:

```powershell
Get-ChildItem -Recurse -File <project> -Filter *.cs |
  Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
  Select-String -Pattern '\[(Fact|Theory)'
```

## Reconciled declaration counts

| Project or subset | Declarations | Current disposition |
|---|---:|---|
| `OrcaCore.Integration.Tests` provisional sources | 115 | Physically retained, explicitly noncompiled; replaced incrementally by public-only `CurrentSurface` journeys |
| `OrcaCore.Providers.RabbitMq.Tests` | 7 | Physically retained; deferred non-first-release transport |
| `OrcaCore.Providers.Redis.Tests` | 8 | Physically retained; deferred non-first-release transport |
| `OrcaCore.Providers.SqlServer.Tests` | 12 | Physically retained; unresolved staged-plan obligation described below |
| `OrcaCore.Providers.ZeroMq.Tests` | 4 | Physically retained; deferred non-first-release transport |
| **Original inactive total** | **146** | **No source file deleted** |

The reactivated integration project adds five new current-surface declarations.
Those five are not part of the original 146 and do not reduce the 146-source
recovery inventory.

## Integration-source classification

The 115 provisional integration declarations are split in the project file so
their exclusion is reviewable:

| Category | Declarations | Files / reason |
|---|---:|---|
| Provisional catch-all host, raw/internal command, or superseded facade coupling | 81 | `E2E` (19), PostgreSQL `Engine` (17), `Hosting` (17), `MultiNode` (15), `Observability` (13) |
| Deferred DAG/job-scheduler capability | 18 | `JobScheduler/JobSchedulerStackIntegrationTests.cs` |
| Providers outside the exact first-release package set | 16 | SQL Server engine (1), mixed provider stacks (13), Redis stack (1), SQL Server stack (1) |
| **Total** | **115** | All supporting fixtures and command helpers also remain on disk |

The active replacement lane uses the public `IWorkflowDefinitionRegistry`,
typed definition and instance handles, `IWorkflowEventClient`, the Ephemeral
and Durable engines, the InMemory and PostgreSQL provider roles, and the exact
durable-engine/callback-ingress hosting entries. It references none of the
retired catch-all host, provisional raw durable commands, DAG/Saga surfaces,
RabbitMQ, Redis, SQL Server, or ZeroMQ.

## Provider-project decisions

RabbitMQ, Redis, and ZeroMQ are classified as deferred non-first-release
transports. Their tests and product projects remain recoverable but are not
references of the active integration project and are not activated in
`OrcaCore.slnx`.

SQL Server is deliberately not given that final classification. The approved
first-release surface names PostgreSQL as the complete production durable
provider and InMemory as development/test only (`tasks.md` 3.2 and 7.10;
`design.md` Decision 16), so SQL Server is not part of the active package
closure. However, pending task 10.2 still explicitly requires SQL Server
certification. Before task 10.2 or the final gate can be credited, the staged
plan must therefore do one of the following through its normal approval flow:

1. restore and port the SQL Server certification lane into an approved package
   scope; or
2. amend task 10.2 to remove SQL Server from the v1 verification contract.

Until that decision, the 12 SQL Server declarations are preserved as an open
plan-reconciliation obligation, not described as obsolete and not deleted.

## Re-enablement rule

Legacy sources are not re-enabled by directory or project wholesale. Supported
behavior is ported one public consumer journey at a time, made green, and then
credited. Deferred capabilities remain visible in this ledger until their
owning section or an approved amendment resolves them.

## Current reactivation evidence

The reactivated project now appears in `OrcaCore.slnx`. Its focused evidence is:

| Command / lane | Result |
|---|---|
| `dotnet build OrcaCore.slnx --no-restore` | Passed, 0 warnings / 0 errors with the integration project active |
| Fresh targeted restore plus resolved-library audit | Passed; no RabbitMQ, Redis, SQL Server, ZeroMQ, or associated transport/Testcontainers package is resolved |
| `dotnet build tests/OrcaCore.Integration.Tests/OrcaCore.Integration.Tests.csproj --no-restore` | Passed, 0 warnings / 0 errors |
| Non-container current-surface lane | 4 passed / 0 failed / 0 skipped |
| PostgreSQL replacement-host lane (Docker) | 1 passed / 0 failed / 0 skipped |
| Complete reduced integration lane | 5 passed / 0 failed / 0 skipped |
| `--list-tests` | Exactly the five `CurrentSurface` declarations; no provisional declaration compiled |
| Targeted `git diff --check` | Passed |

These results credit the reduced integration lane only. They do not approve
Section 7, retire the preserved 146-declaration inventory, resolve the SQL
Server/task-10.2 mismatch, or authorize task 8.0.
