# Section 7 Rejection-Remediation Exit — Second-Reviewer Corroboration

Date: 2026-07-31

Status: **corroboration note, not a verdict.** The single exit verdict for this target is
`developer-facing-interface-section-07-rejection-remediation-exit-independent-verdict-2026-07-31.md`.
This note neither supersedes, amends, nor re-decides it. It records a second, independently executed
reproduction of the same frozen target, plus one comparison the request did not require — active test
coverage measured against the clean recovery worktree.

No reviewed source, test, task, spec, plan, manifest, request, or existing review artifact was edited.
No commit was created.

## 1. Provenance and manifest

Reproduced before reading the claims and again after completing validation.

| Item | Expected | Independently observed |
| --- | --- | --- |
| HEAD | `d76192f089dd07f68e310c21fe4e5a38dd93cf7f` | matches |
| HEAD tree | `2264e670493ecc76359d42ee5273028eb287a566` | matches |
| `8c2dd712` is an ancestor | yes | yes |
| Ordered porcelain entries | 705 | 705, byte-identical ordered content |
| Entry classes | 404 modified / 52 deleted / 249 untracked | 404 / 52 / 249 |
| Raw-manifest SHA-256 | `E42D9C8C686C64ED9372DD837B89D0EC412184C29F810726F3B6FF4260842C99` | matches |
| LF-normalized sorted SHA-256 | `3856C150A07D3C5FD07167671E5552C8E2D66B0EE3B8DEA9D3E241DE74760BE0` | matches |

The recovery worktree at `X:\Projects\GitHub\Workflow-orca-recovery` is detached at the same HEAD with
zero porcelain entries.

One expected accounting note: the self-inclusive manifest necessarily predates any verdict, so the
working tree reaches 706 entries once the exit verdict is written and 707 once this note is written.
Those additions are `docs/review/` artifacts only; no product, test, task, spec, or plan path differs
from the frozen 705.

## 2. Validation independently reproduced

Run from the repository root against Release output.

| Lane | Result |
| --- | --- |
| `dotnet build OrcaCore.slnx -t:Rebuild -c Release` | 24 projects; 0 warnings / 0 errors |
| Core | 413 passed / 0 failed / 0 skipped |
| Ephemeral | 64 / 0 / 0 |
| Durable | 72 / 0 / 0 |
| Hosting smoke | 1 / 0 / 0 |
| Acceptance | 42 / 0 / 0 |
| Provider certification | 80 / 0 / 0 |
| PostgreSQL Docker | 79 / 0 / 0 (2 m 09 s) |
| Reactivated integration | 5 / 0 / 0 |
| **Active product/integration total** | **756 passed / 0 failed / 0 skipped** |
| Section 4 scenarios | 4 / 0 / 0 |
| Section 5 scenarios | 8 / 0 / 0 |
| Section 6 scenarios | 32 / 0 / 0 |
| Section 7 scenarios | 37 / 0 / 0 |
| `Disposition=Infrastructure` ×3 | 162 / 0 / 0 on each of three consecutive runs |
| `Disposition=ExpectedRed` | 14 failed / 0 passed / 0 skipped |
| Green compile fixtures | exit 0; 26 source-fixture and 26 product-package forbidden-member CS1061 diagnostics; incomplete package rejected |
| Expected-red compile fixtures | exit 0; 0 gaps |
| Green package fixtures | exit 0; 6 built from the repository-local feed through Section 7 |
| Expected-red package fixtures | exit 1 with exactly `dag-hosting` and `kubernetes-companion` |
| Strict OpenSpec | both active changes valid; `--all --strict` 17 passed / 0 failed |
| Vulnerability audit | 24 projects, 0 vulnerable |
| `git diff --check` | exit 0; line-ending notices only |

Section 4–7 scenarios total 81 and all execute through the strict path. Docker-backed suites were run
sequentially and separately from the fixture builds.

## 3. Independent source derivation

**Start binding (2.1.1).** `StartedWorkflowIdempotencyRecord`
(`src/OrcaCore.Provider.Abstractions/ProviderPorts.cs:78-84`) carries the key, instance, definition
id/version, definition fingerprint, and input fingerprint. `DurableCommitMaterializer`
`CreateStartIdempotencyWrites` derives the write from the `WorkflowStartedEvent` inside the same
decision, and it travels in `ProviderCommitBatch.StartIdempotencyOperations`
(`ProviderCommitContracts.cs:80`), so the mapping commits atomically with the start. In PostgreSQL,
events, inbox operations, and start-idempotency operations share one `ReadCommitted` transaction
(`PostgreSqlWorkflowStore.cs:164-226`). `DurableStartService.Matches`
(`DurableStartService.cs:91-100`) compares definition id, version, definition fingerprint, and input
fingerprint; a mismatch returns the binding as `ConflictingBinding`. A replacement process has an
empty local cache and resolves through `commandProcessor.GetStartedAsync`, so the closed conflict
survives process replacement.

**Event deduplication (2.1.2).** `InboxRecord` is keyed `(InstanceId, EventId)` with the normalized
`EnvelopeFingerprint` (`ProviderPorts.cs:56-60`), and `IWorkflowInboxStore.GetAsync` takes both the
instance and the event id. In `DurableWorkflowFacade.DeliverToInstanceCoreAsync` the fingerprint is
computed at line 815, the in-process cache is consulted at 820-830, the **persisted** inbox at
832-845, and only then is terminal status checked at 847 and no-active-wait at 852. The ordering
claim — dedup precedes terminal/no-wait classification — holds in source, not only in test.

**Cancellation and serialized results (2.2).** `WorkflowInstance.TryRequestCancellation`
(`WorkflowInstance.cs:808-830`) performs the terminal check and the
`Interlocked.CompareExchange` request latch inside `lock (snapshotGate)` and publishes the snapshot
before releasing, returning `AlreadyTerminal` / `AlreadyRequested` / `Requested` from that atomic
transition. `EphemeralWorkflowEngine.RequestTerminationAsync` (lines 444-460) computes
`Terminated` versus `AlreadyTerminal` **inside** `executionLane.RunAsync`, so concurrent terminators
read the serialized winner rather than a stale pre-read. On the durable side,
`DurableWorkflowFacade.RequestCancellationAsync` and `TerminateAsync` (lines 456-512) map
`result.LifecycleDisposition` from the committed command; neither reads a snapshot first.

**Guard corrections (2.3).** `ExceptionCameFromExpectedProduct`
(`Phase0BehaviorContract.cs:483-504`) now requires the exact declaring assembly, type, and member;
the former assembly-wide `OrcaCore.Core` fallback is absent, and
`ThrowingArgumentExpression_CannotMintAnObservationForAnUninvokedFacade` covers the argument-evaluation
mutation. `IsProductFramePresent` walks outward, records the consuming frame, and returns false on
reaching `_harnessBoundaryAssemblies`, so a direct harness call cannot mint deterministic-seam
evidence. `AssertExecutableAgainstCurrentPhysicalAssemblyAsync` and every hard-coded call
substitution are gone; Sections 4–7 all call `AssertExecutableAsync`. The `cancel-every-barrier`
driver (`Section7GovernanceScenarioHost.cs:221`) observes the exact gate member and separately drives
the barrier through a real `DurableCommandProcessor` with the gate injected.

**Task accounting.** Parsed directly from the task files: reshape 106 complete / 30 pending / 136
total, governance 16 / 0 / 16, zero duplicate IDs in either. Task `8.0` is unchecked.

## 4. Ledger reconciliation

Recounting excluded sources with the record's own declaration regex reproduces **98 files / 549
declarations** exactly, once scoped to the five product test projects. A whole-tree recount returns
100 files / 567 declarations; the difference is precisely
`Integration.Tests/Engine/EnginePostgreSqlIntegrationTests.cs` (17) and
`Integration.Tests/Engine/EngineSqlServerIntegrationTests.cs` (1), which the inactive-project audit
already carries as "PostgreSQL `Engine` (17)" and "SQL Server engine (1)" inside its 115. The two
ledgers partition the space without double-counting.

The inactive inventory also reconciles: Integration 120 on disk minus the 5 new `CurrentSurface`
declarations = 115, plus RabbitMQ 7, Redis 8, SQL Server 12, ZeroMQ 4 = **146**. The four transport
test projects are absent from `OrcaCore.slnx`; the integration project is present.

## 5. Coverage measured against the recovery worktree

Comparing `tests/` in the clean worktree (`d76192f`) with the review target:

| Measure | Baseline | Target |
| --- | ---: | ---: |
| Test `.cs` files | 239 | 299 |
| `[Fact]`/`[Theory]` declarations | 1007 | 1229 |
| Active (compiled) | — | 168 files / 565 declarations |
| Excluded but present on disk | — | 131 files / 664 declarations |

**Baseline test files absent from the target: 0 of 239.** No test source was deleted. Sixty new test
files add 189 declarations; the 565 active declarations expand to the 932 executed cases reported
above (756 product/integration + 162 infrastructure + 14 intentional reds).

Eighteen files carry fewer declarations than baseline. Most are accounted for by the recovery record:
`WorkflowBuilderTests` 29→24 (five child/legacy deferred), `ManagementAcceptanceTests` 6→1 (five broad
list/count/statistics cases outside reduced v1 management), `ModeFirstWorkflowBuilderTests` 13→12 (one
child declaration held for Section 8), `MailboxAcceptanceTests` 3→2 (early buffering superseded by
`NoActiveWait`). Two are net-zero relocations rather than losses:
`R4DurableEngineFindingsTests` 12→7 with the new `R4DurableEngineDeferredTests.cs` at 5, and
`OperationsAcceptanceTests` 3→1 with the preserved `LegacyOperationsAcceptanceTests.cs` at 2. Seven
more reductions occur inside files that are themselves excluded and therefore carry no active-coverage
consequence.

## 6. Notes recorded for completeness — none blocking

1. **`R` classification is imprecise for at least three files.** The record defines `R` as
   "internal/compiled-IR white-box coverage." Compiled out-of-tree against the frozen target,
   `Policies/RetryPolicyTests.cs`, `Management/TerminalCommandTests.cs`, and
   `Execution/DeadlineExecutionTests.cs` produce zero `CS0122` — they are public-surface sources whose
   failures are the mechanical reshape signature (`CS1593`/`CS0411`/`CS1061`/`CS1503`). By contrast
   `Execution/RoutingTests.cs`, `Execution/WaitMatchingTests.cs`, and `Execution/InterpreterTests.cs`
   do fail on `CS0122` and are labelled correctly. The substantive `R` requirement — named executed
   replacement evidence — is satisfied in each case, so this is ledger wording, not lost coverage.
2. **Two in-place reductions in active files are not itemized.** `Timers/EphemeralTimerTests.cs` is
   6→4 against baseline while the record states 5→4, and `TimerAcceptanceTests.cs` is 2→1 while the
   record credits 1 restored of 1. The dropped timer-race assertion appears relocated into the
   restored `Timers/TimerEventRaceTests.cs` (3 declarations, active), so the behavior is covered; only
   the arithmetic is unreconciled.
3. **Two guard methods were removed** — `StructuredFanoutContractGuards.Product_ContainsFinalEmptyParallelAndClosedOutcomeContract`
   and `DeadlineRetryContractGuards.Product_ContainsFinalDeadlineDiagnosticAndOperationCoordinate`.
   Both were source-text greps for a diagnostic code plus an exported-type existence assertion.
   `SFE-AUTH-BRANCH-004` and `SFE-AUTH-DEADLINE-001` are now asserted by tests that build a workflow
   and inspect the emitted diagnostic (`BuilderAcceptanceTests`, `AuthoringLifecycleTests`,
   `StagedWorkflowBuilderTests`), and `StepOperationId` remains in the v1 public-contract manifest.
   This is a strengthening; it is recorded only so the guard-count change is not silent.

## 7. Concurrence

Every claim I reproduced matched. The three rejection blockers are closed in source and in named
regressions, the guard relaxations are gone rather than suppressed, the retirement ledger reconciles
to the source at declaration level, and no test file was deleted anywhere in the tree. On the evidence
I gathered independently, I concur with the recorded `APPROVE`. The three notes above are accuracy
items for a future ledger pass, not conditions on that verdict.

This note authorizes nothing. The checkpoint-commit sequencing and the Section 8 gate remain exactly
as the exit verdict states.
