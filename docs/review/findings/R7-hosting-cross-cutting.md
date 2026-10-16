# R7 — Hosting & Cross-Cutting — Findings

> Phase scope: `src/OrcaCore.Hosting/**`, `samples/OrcaCore.SampleHost`,
> `docs/production-readiness.md`, `benchmarks/OrcaCore.Benchmarks`, CI workflow,
> and a meta-review of `tests/**` for NF-040/NF-030, MG-004, TDD discipline (03), and
> AC-trait coverage vs `docs/specs/12-acceptance-criteria.md`. Primary lenses: security,
> performance, test quality. Code review only. Prior phase findings (R3–R6) are referenced where
> they remain the authoritative defect record.

## Findings

### [P0] Hosted background services are no-ops — outbox pump and timer sweep never run — `OrcaCoreOutboxPumpHostedService.cs:11`
- **Requirement/convention:** PR-040 / DU-032 / EV-050 / T6-03
- **Evidence:**
  ```csharp
  public Task StartAsync(CancellationToken cancellationToken)
  {
      return Task.CompletedTask;
  }
  ```
  Same pattern in `OrcaCoreTimerHostedService.cs:11` and `OrcaCoreOperationalSweepHostedService.cs:11`. `OrcaCore.SampleHost/Program.cs` calls `AddOrcaCoreHostedServices()` and runs the generic host — operators expect background dispatch and timer claiming.
- **Failure scenario:** Host starts successfully; durable outbox records accumulate forever, durable timers never fire, pool expiry/resource sweeps never run — production host appears healthy while workflows stall after the first commit.
- **Recommendation:** Implement hosted services using `BackgroundService`: wire `DurableOutboxPump.PumpOnceAsync` on an interval, claim due timers and issue `FireTimerCommand`, run operational sweeps (pool expiry, etc.). Add integration tests that start the host and assert outbox drain within fake clock advancement.
- **Confidence:** CONFIRMED

### [P1] `DurableOutboxPump` exists but is not registered or invoked by hosting — `DurableOutboxPump.cs:5`
- **Requirement/convention:** PR-015 / PR-040 / DU-032
- **Evidence:** `DurableOutboxPump` is `internal` in `OrcaCore.Engine.Durable` and only constructed in `DurableOutboxTests`. `AddOrcaCore` / `AddOrcaCoreHostedServices` never register it; `OrcaCoreOutboxPumpHostedService` does not reference it.
- **Failure scenario:** Teams extend sample host assuming pump is live; only unit tests exercise dispatch — production path is disconnected.
- **Recommendation:** Expose a public hosting adapter (or move pump to `OrcaCore.Hosting`) and inject `IWorkflowOutboxStore` + `IMessageDispatcher` into the hosted service loop.
- **Confidence:** CONFIRMED

### [P1] `AddOrcaCore` registers in-memory providers only — no durable SQL/Redis hosting path — `OrcaCoreServiceCollectionExtensions.cs:28`
- **Requirement/convention:** PR-040 / NF-002 / T6-03
- **Evidence:** All durable ports resolve to `InMemoryWorkflowProvider` and `InMemoryResourcePoolStore`. No `AddOrcaCorePostgreSql`, retention-store registration, or `IWorkflowRetentionStore` explicit binding beyond cast-from-projection-store in `DurableManagement`.
- **Failure scenario:** Operator copies sample host for staging; restart loses all workflow state; multi-node deployment shares no store — violates durable semantics while using `DurableCommandProcessor`.
- **Recommendation:** Add explicit extension methods per plugin (PostgreSQL, Redis projection cache) with documented replacement of default registrations; sample host should show durable profile behind a config flag.
- **Confidence:** CONFIRMED

### [P1] Durable `PurgeAsync` / `TerminateAsync` lack destructive-breadth safety — `DurableManagement.cs:97`
- **Requirement/convention:** MG-004 / NF-040
- **Evidence:** `DurableManagement.TerminateAsync` and `PurgeAsync` take only `InstanceId` / `RetentionPolicy` — no `DestructiveCommandSafety` or scope-breadth guard. Ephemeral management requires `DestructiveCommandSafety.Confirmed` for broad `All().Terminate()` (`EphemeralManagement.cs:277-295`); tested in `TerminalAcceptanceTests`.
- **Failure scenario:** Host exposes `DurableManagement.PurgeAsync` on an HTTP admin endpoint without its own guard; single call purges instance metadata with no confirmation contract from the library.
- **Recommendation:** Mirror ephemeral destructive-safety on durable purge/terminate (and fluent `All()` commands when added); document host must still authenticate operators.
- **Confidence:** CONFIRMED

### [P2] Tests still use wall-clock delays — `ExecutionLaneTests.cs:131`
- **Requirement/convention:** NF-020 / 03 §4
- **Evidence:** `Task.Delay(timeout, cancellationToken)` in `ExecutionLaneTests`; `Task.Delay(10, …)` in `ResourceGovernanceTests.cs:237` and `OperationsAcceptanceTests.cs:192`. TDD workflow requires `FakeTimeProvider` and rejects sleeps.
- **Failure scenario:** CI timing flakes on loaded agents; race tests pass/fail non-deterministically.
- **Recommendation:** Replace with `FakeTimeProvider` + `TaskCompletionSource` gates (pattern already used in `YieldTests`, `TimerEventRaceTests`).
- **Confidence:** CONFIRMED

### [P2] AC trait coverage is partial — many behavioral tests untagged — `tests/**`
- **Requirement/convention:** NF-012 / 03 §2
- **Evidence:** Spec lists 100+ behavioral ACs in `docs/specs/12-acceptance-criteria.md`. Tagged tests exist across acceptance, durable, ephemeral, and certification projects (~200 trait annotations, but many ACs have no tagged test). Examples with **no** `[Trait("AC",…)]` in tests: `AC-315` (multi-node mutator, advanced), large swaths of ephemeral unit tests (`MailboxTests`, `RoutingTests`, `LoopWaitTests`), and provider-sensitive ACs exercised only indirectly. `OrcaCore.Hosting.Tests` has zero AC traits.
- **Failure scenario:** Regression of a spec AC ships without CI traceability; coverage matrix cannot be automated.
- **Recommendation:** Tag remaining behavioral tests; add a CI check that every non-structural AC in doc 12 has ≥1 tagged test (allow explicit waiver list for advanced/provider-only ACs).
- **Confidence:** CONFIRMED (inventory by grep; not every AC manually traced)

### [P2] CI runs tests but does not collect coverlet output — `.github/workflows/ci.yml:29`
- **Requirement/convention:** 03 §6
- **Evidence:** Test projects reference `coverlet.collector`, but CI step is `dotnet test … --verbosity normal` with no `--collect:"XPlat Code Coverage"` or report upload.
- **Failure scenario:** Coverage gate described in TDD workflow never executes; regressions in untested paths go unnoticed.
- **Recommendation:** Add coverage collection and publish (even informational) in CI; enforce threshold when baseline stabilizes.
- **Confidence:** CONFIRMED

### [P2] `ReconstructDagRunAsync` loads unbounded event tail — `DurableManagement.cs:210`
- **Requirement/convention:** NF-030 / DU-071
- **Evidence:** `LoadTailAsync(new WorkflowStreamId(rootInstanceId), StreamVersion.Empty, …)` loads the entire stream from version 0 for every DAG reconstruction call.
- **Failure scenario:** Long-running EKS scheduler run with many waves; observability API scans full history per request — latency and memory grow without bound.
- **Recommendation:** Persist DAG node index in projections or load from last checkpoint + bounded tail.
- **Confidence:** CONFIRMED

### [P2] Redis projection list path scans all instance keys — `RedisProjectionStore.cs:194`
- **Requirement/convention:** NF-030 / PR-023
- **Evidence:** `ListRedisAsync` calls `SetMembersAsync(InstanceIndexKey)` then `StringGetAsync` for every member, deserializes each snapshot, filters in memory with `Matches`.
- **Failure scenario:** Large instance cardinality makes management queries O(n) on Redis despite indexed metadata filters.
- **Recommendation:** Secondary indexes (status, definition) or RediSearch; at minimum paginate `SetMembers` and push filters to Redis when possible.
- **Confidence:** CONFIRMED

### [P2] Hosting tests verify DI registration only — `OrcaCoreHostingServiceCollectionTests.cs:18`
- **Requirement/convention:** 03 §2 / PR-040
- **Evidence:** Three tests assert service types resolve from `ServiceCollection`; no test starts hosted services and observes outbox/timer behavior.
- **Failure scenario:** P0 no-op hosted services remain green forever.
- **Recommendation:** Add hosting integration test with in-memory store + fake dispatcher counting `PumpOnceAsync` invocations once implemented.
- **Confidence:** CONFIRMED

### [P3] Redis projection reads deserialize JSON without integrity check — `RedisProjectionStore.cs:213`
- **Requirement/convention:** NF-040 / PR-016
- **Evidence:** `ListRedisAsync` does `JsonSerializer.Deserialize<WorkflowInstanceSnapshot>(value.ToString(), JsonOptions)` on strings stored in Redis. Trust model assumes Redis ACL protects keys; compromised or cross-tenant Redis write could feed malformed snapshots into management queries.
- **Failure scenario:** Shared Redis misconfiguration allows write to `orca:instance:*` keys; management API surfaces attacker-controlled snapshot fields.
- **Recommendation:** Document Redis as trusted persistence boundary; optional HMAC/version prefix on stored blobs for defense in depth.
- **Confidence:** PLAUSIBLE

### [P3] `OrcaCore.Hosting` hard-references `OrcaCore.Providers.RabbitMq` — `OrcaCore.Hosting.csproj:15`
- **Requirement/convention:** PR-002 / 01 §3
- **Evidence:** Hosting project includes RabbitMQ provider reference solely for `AddOrcaCoreRabbitMq`; hosts using ZeroMQ or in-memory dispatch still pull RabbitMQ assembly into the hosting package dependency graph.
- **Failure scenario:** Minimal host binary carries unused broker dependency; version coupling to RabbitMQ.Client in hosting layer.
- **Recommendation:** Move `AddOrcaCoreRabbitMq` to `OrcaCore.Providers.RabbitMq` extension class (pattern used elsewhere in .NET ecosystem).
- **Confidence:** CONFIRMED

### [P3] Local SDK pin blocked audit-time test execution — `global.json`
- **Requirement/convention:** R0 baseline (review README §3)
- **Evidence:** `dotnet test OrcaCore.slnx` failed: SDK `10.0.200` required, `10.0.301` installed message (environment mismatch). R0 findings file was not produced in this audit series.
- **Failure scenario:** Reviewers cannot confirm green build locally without SDK alignment.
- **Recommendation:** Complete R0 with build/test baseline recorded in `findings/R0-foundations.md`; align `global.json` with CI SDK or document roll-forward.
- **Confidence:** CONFIRMED (command output this session)

## Cross-phase rollup (for remediation planning)

| Theme | Phases | Representative severity |
|-------|--------|-------------------------|
| Hosted pump/timer/sweep not implemented | R7 | P0 |
| Durable child throttle + resume token consume | R6 | P0 / P1 |
| StartOrGet / early-event mailbox / pool ordering | R4 | P0 / P1 |
| Branch-blind wait matching | R3, R4, R6 | P1 |
| SqlServer provider stub / provider certification gaps | R5 | P0 / P1 |
| Management API incomplete (commands, history, stats) | R3, R4 | P2 |

## Coverage note

**Reviewed hosting:** `OrcaCoreServiceCollectionExtensions.cs`, all three `Services/*HostedService.cs`, `OrcaCore.SampleHost`, `OrcaCore.Hosting.Tests`, `production-readiness.md`, `benchmarks/OrcaCore.Benchmarks` (README + skeleton presence), `.github/workflows/ci.yml`.

**NF / MG / TDD:**
| ID | Verdict |
|----|---------|
| NF-001…003 | Pass — .NET SDK project; library embeddable; pre-production documented |
| NF-010…011 | Not fully audited (R0 scope); spot-check: nullable/warnings in Directory.Build.props assumed |
| NF-012 | **Partial** — AC traits incomplete; certification suite exists (R5) |
| NF-020 | **Partial** — core uses `TimeProvider`; tests still have `Task.Delay` in places |
| NF-021 | Pass — `production-readiness.md` states delivery guarantees |
| NF-030 | **Partial** — traps in PostgreSQL list/count (R5), DAG reconstruct, Redis scan |
| NF-040 | **Partial** — host-mediated security documented; durable destructive safety gap; provider deserialize is type-explicit |
| NF-050 | Partial — `production-readiness.md` + provider READMEs; full contract matrix not verified |
| MG-004 | **Fail** on durable purge/terminate breadth |
| MG-040 / hosting | **Fail** — background services are stubs |
| 03 §2–§4 | **Partial** — good patterns in acceptance tests; delays and missing AC tags |
| 03 §6 | **Fail** — coverlet not in CI |

**AC coverage (spot-check gaps):** `AC-315` untested; `AC-609` tagged but behavior not verified (R6); many ephemeral unit tests lack traits despite proving AC-adjacent behavior. Acceptance project has strong AC discipline for core flows (wait, parallel, foreach, terminal, management).

**Build/test baseline:** Not executed successfully in this session (SDK resolution). CI workflow builds and tests `OrcaCore.slnx` on `ubuntu-latest` — assumed green unless CI proves otherwise.

**Stopped at:** End of R7 scope. **R0 (foundations/banlist/csproj graph)** was not executed as a dedicated session; recommend a follow-up R0 pass for `Directory.Build.props`, solution reference graph, and banlist grep across all projects.

**Audit series complete** for phases R3–R7 as requested. Prior findings remain in `R3-ephemeral-engine.md` through `R6-composition-dag-saga.md`.
