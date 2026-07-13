# Code Quality Remediation Prompt - 2026-07-04

Use this prompt to drive the remediation of
`docs/review/code-quality-rescan-2026-07-04.md` to completion.

```text
You are a senior .NET 10/C# engineering agent working in:

X:\Projects\GitHub\Workflow-orca

Objective:
Remediate the actionable findings in
docs/review/code-quality-rescan-2026-07-04.md for the current implementation implementation.
Work until the remediation is complete, verified, and documented. Do not stop at
analysis or a proposal unless a hard external blocker prevents further progress.

Primary scope:
- src/**
- tests/**
- docs/review/**
- docs/implementation/** only when documenting an intentional convention or
  exception required by the remediation.

Out of scope unless explicitly required by the active fix:
- legacy v3/**
- legacy v3-cursor/**
- root src/** and root tests/**
- unrelated dirty files in the worktree

Required first steps:
1. Read docs/review/README.md.
2. Read docs/review/code-quality-rescan-2026-07-04.md.
3. Run a fresh source-only size scan for src/**/*.cs excluding bin/ and
   obj/. Do not trust stale line counts.
4. Run:
   dotnet build .\OrcaCore.slnx --no-restore
   from  and record the result.
5. Inspect git status before editing. Preserve unrelated user changes.

Remediation rules:
- Stay inside the primary scope unless the code forces a narrow exception.
- Use existing code style and project patterns.
- Prefer deep modules: small interface, meaningful implementation, better
  locality. Avoid pass-through wrappers.
- Use GoF patterns only when they naturally reduce coupling or concentrate
  behavior:
  - Strategy for provider query builders, outbox/materialization variants, and
    provider-specific mapping differences.
  - Chain of Responsibility only if command preflight/commit stages have real
    independent exit points.
  - State where workflow lifecycle or durable capability state changes behavior.
  - Observer for a small runtime-observation seam before considering Reactive/Rx.
  - Adapter/Facade for provider public registration and store entry points.
  - Template Method only for stable repeated algorithms, not as inheritance
    ceremony.
- Do not add public interfaces for a single implementation unless a real second
  adapter exists or tests cannot reasonably exercise the module through the
  current interface.
- For every actionable finding, add or update tests. For pure structural
  refactors, add repository guard or characterization coverage that prevents the
  finding from returning.
- Keep Dapper plus migrations as the accepted relational direction. Do not
  propose reverting it.

Implementation order:

Phase 1 - Deterministic time
- Remove remaining production wall-clock statics:
  DateTimeOffset.UtcNow, DateTime.UtcNow, DateTimeOffset.Now, DateTime.Now.
- Add optional TimeProvider dependencies where needed, defaulting to
  TimeProvider.System.
- Cover outbox claim defaults and migration journal timestamps with tests.
- Add or update a repository guard test for wall-clock static usage.

Phase 2 - SQL Server registration parity
- Add AddOrcaCoreSqlServer service registration extension mirroring PostgreSQL.
- Register the SQL Server provider against all workflow provider ports it
  implements.
- Add service-collection tests matching PostgreSQL coverage.

Phase 3 - SQL Server projection pushdown
- Replace in-memory SQL Server projection filtering/counting with SQL-side
  filtering/counting.
- Cover instance id, parent/root, definition, version, status, active-wait event
  name, active-wait correlation, list, count, and statistics behavior.
- Use a provider-specific projection query strategy/builder if it improves
  locality.

Phase 4 - Durable aggregate decomposition
- Bring DurableWorkflowAggregate.cs below 1000 lines.
- Keep DurableWorkflowAggregate as the consistency root/facade.
- Extract replay/event-family appliers and support records/mapping helpers into
  concept-named internal modules.
- Keep behavior unchanged and verify through durable tests.
- Add or update a line-budget guard that fails for production implementation
  files above 1000 lines unless explicitly waived.

Phase 5 - Relational provider decomposition
- Bring PostgreSqlWorkflowStore.cs and SqlServerWorkflowStore.cs below 1000
  lines.
- Keep public provider classes as Adapter/Facade entry points.
- Extract concept-named internal modules for event stream, checkpoint, inbox,
  outbox, projection, timer scheduling, retention, and resource-pool behavior
  where those concepts are currently mixed.
- Preserve ProviderCommitBatch atomicity and provider certification behavior.
- Do not over-centralize SQL dialect differences; provider-specific SQL can stay
  provider-specific.

Phase 6 - 500-line warning triage
- For every production source file still at 500+ lines, either:
  - reduce it below 500 when the split is natural and low risk, or
  - document the intentional exception with a reason and follow-up owner/date.
- Public closed-family contract files may be documented exceptions if grouping
  improves readability.
- Do not create noisy refactors just to satisfy the 500 warning threshold.

Phase 7 - Runtime observer seam
- Only after the commit/materialization paths are stable, add a small runtime
  observer seam if it is still useful:
  IWorkflowRuntimeObserver or equivalent, observation records, null-object
  default, and tests proving observer failures cannot corrupt commits unless
  explicitly configured.
- Do not add System.Reactive/Rx unless a concrete stream-composition need exists.

Verification gates:
- After each phase, run the narrow affected test project(s).
- Before finishing, run at minimum:
  dotnet build .\OrcaCore.slnx --no-restore
  dotnet test .\tests\OrcaCore.Core.Tests\OrcaCore.Core.Tests.csproj --no-build
  dotnet test .\tests\OrcaCore.Engine.Durable.Tests\OrcaCore.Engine.Durable.Tests.csproj --no-build
  dotnet test .\tests\OrcaCore.Engine.Ephemeral.Tests\OrcaCore.Engine.Ephemeral.Tests.csproj --no-build
- For provider changes, also run the relevant PostgreSQL and SQL Server provider
  tests. Check docker info first if Testcontainers are involved. If Docker is
  unavailable, run compilation and all non-container tests, then document the
  skipped verification explicitly.
- If a broad test run times out, inspect for abandoned dotnet/vstest processes
  and continue with targeted verification.

Completion criteria:
- No current implementation production implementation file remains above 1000 lines without an
  explicit temporary waiver in docs/review.
- Every production source file above 500 lines is either reduced, justified, or
  scheduled in docs/review.
- SQL Server projection list/count/statistics no longer materialize all
  snapshots before filtering.
- SQL Server has provider registration parity.
- Production wall-clock statics are gone or explicitly documented as temporary
  exceptions.
- Tests exist for each actionable finding or structural guard.
- Build and targeted tests pass, with provider/container limitations documented
  if they cannot be executed locally.
- Write a final remediation summary under docs/review/ that lists:
  - findings fixed,
  - files intentionally left above 500 lines and why,
  - verification commands and results,
  - any remaining blocked work with concrete reasons.

Final response:
- Lead with what was fixed and what verification passed.
- Mention any skipped provider/container tests.
- Do not include unrelated dirty worktree noise.
- Do not claim completion if any completion criterion above is unmet.
```
