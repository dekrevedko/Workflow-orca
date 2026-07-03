# Harness, CI & Fixtures — Integration Testing Infrastructure

How to run, organize, and gate integration tests. Complements `03-tdd-workflow.md` §2 taxonomy.

---

## Recommended solution changes

### 1. New project `OrcaCore.Integration.Tests`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\OrcaCore.Hosting.Tests\..." /> <!-- reuse RecordingHost helpers -->
    <ProjectReference Include="..\..\src\OrcaCore.Hosting\..." />
    <ProjectReference Include="..\..\src\OrcaCore.Providers.PostgreSql\..." />
    <ProjectReference Include="..\..\src\OrcaCore.Providers.RabbitMq\..." />
    <ProjectReference Include="..\OrcaCore.TestSupport\..." />
    <PackageReference Include="Testcontainers.PostgreSql" />
    <PackageReference Include="Testcontainers.RabbitMq" />
    <PackageReference Include="Microsoft.Extensions.Hosting" />
    <PackageReference Include="Microsoft.Extensions.TimeProvider.Testing" />
  </ItemGroup>
</Project>
```

Add to `OrcaCore.slnx` under `/tests/`.

### 2. Move vs duplicate

| Current location | Recommendation |
|------------------|----------------|
| `OrcaCoreHostingServiceCollectionTests` host tests | Keep; **also** add PG variants in Integration |
| `PostgreSql*Tests` certification | Keep; Integration calls same fixtures |
| `RabbitMqDispatcherIntegrationTests` | Keep; stack tests compose with pump |
| Acceptance tests | Keep InMemory; Integration ports high-value ACs |

### 3. Shared fixtures in `OrcaCore.TestSupport` (or Integration/Fixtures)

| Fixture | Responsibility |
|---------|----------------|
| `PostgreSqlOrcaFixture` | Start container once; `InitializeAsync` migrations; expose connection string |
| `RabbitMqOrcaFixture` | Exchange/queue declare; connection string |
| `OrcaStackFixture` | PG + RabbitMQ + connection strings bundle |
| `OrcaHostFactory` | `IHost` with `FakeTimeProvider`, configurable `AddOrcaCore*` |
| `MultiHostFixture` | Two `IHost` instances, shared DB |
| `JobCompletionHarness` | Queue consumer → inject inbox events |

Use **xUnit collection fixtures** to share containers:

```csharp
[CollectionDefinition(nameof(PostgreSqlCollection))]
public sealed class PostgreSqlCollection : ICollectionFixture<PostgreSqlOrcaFixture> { }
```

---

## Traits and filters

| Trait | Values | CI usage |
|-------|--------|----------|
| `Category` | `Integration`, `Unit` (default implicit) | `--filter Category!=Integration` on PR |
| `Container` | `PostgreSql`, `RabbitMq`, `Redis`, `SqlServer` | Subset when debugging |
| `AC` | `AC-xxx`, `JS-AC-xxx` | Traceability |
| `Speed` | `Slow` | Optional exclude on PR |

**Repository guard:** extend `RepositoryGuardTests` to assert Integration project exists and
every `INT-*` P0 scenario in docs has at least one matching test method (optional future).

---

## CI workflow (suggested)

```yaml
jobs:
  unit:
    runs-on: ubuntu-latest
    steps:
      - run: dotnet test v3-gpt/OrcaCore.slnx --filter "Category!=Integration" -c Release

  integration:
    runs-on: ubuntu-latest
    # Docker available on ubuntu-latest
    if: github.event_name == 'push' && github.ref == 'refs/heads/main'
    steps:
      - run: dotnet test v3-gpt/OrcaCore.slnx --filter "Category=Integration" -c Release
```

**PR strategy:** Unit + acceptance only (fast). **Main + nightly:** full integration.

**Windows dev:** Document Docker Desktop requirement; parallel test collections can lock DLLs —
use `--no-build` per project or disable parallel for Integration collection.

---

## Stability rules (from 03-tdd-workflow.md)

- **No `Task.Delay`** — `FakeTimeProvider` + `PeriodicTimer` with manual advance in tests
- **Deterministic races** — `RaceCoordinator` / `TaskCompletionSource` gates
- **Container reuse** — one PG container per collection, not per test
- **Cleanup** — truncate tables between tests if sharing container (`TRUNCATE orcacore_* CASCADE`)
- **Timeout** — xUnit test timeout 60s for integration; container start ~30s budget

---

## Docker resource budget

| Container | Image | RAM (approx) |
|-----------|-------|--------------|
| PostgreSQL | `postgres:17-alpine` | 256MB |
| RabbitMQ | `rabbitmq:4-management-alpine` | 256MB |
| Redis | `redis:7-alpine` | 64MB |
| SqlServer | `mssql` | 2GB+ (run selectively) |

**Compose profile `minimal`:** PG + RabbitMQ only for job scheduler tests.

---

## Implementation phases

### Phase I — Foundation (1–2 days)
- [ ] Create `OrcaCore.Integration.Tests` project + solution entry
- [ ] `PostgreSqlOrcaFixture` + collection
- [ ] `INT-EP-001` wait survives processor restart on PG
- [ ] CI filter `Category!=Integration` on default test step

### Phase II — Host spine (2–3 days)
- [ ] `OrcaHostFactory` with real PG store
- [ ] `INT-HO-002` outbox from processor through host pump
- [ ] `INT-HO-003` timer host E2E

### Phase III — Stack (3–5 days)
- [ ] `OrcaStackFixture` (PG + RabbitMQ)
- [ ] `INT-ST-001` outbox to queue
- [ ] `INT-JS-003` external job round-trip

### Phase IV — Multi-node & E2E ports (ongoing)
- [ ] `MultiHostFixture`
- [ ] `INT-MN-001`…`005`
- [ ] Port acceptance ACs per [04-durable-workflow-e2e.md](04-durable-workflow-e2e.md)

---

## Mapping INT IDs to files

| Prefix | Doc file |
|--------|----------|
| `INT-HO-*` | [01-hosting-and-hosted-services.md](01-hosting-and-hosted-services.md) |
| `INT-EP-*` | [02-engine-with-real-providers.md](02-engine-with-real-providers.md) |
| `INT-ST-*` | [03-provider-composition-stacks.md](03-provider-composition-stacks.md) |
| `INT-E2E-*` | [04-durable-workflow-e2e.md](04-durable-workflow-e2e.md) |
| `INT-MN-*` | [05-multi-node-and-restart.md](05-multi-node-and-restart.md) |
| `INT-JS-*` | [06-job-scheduler-e2e.md](06-job-scheduler-e2e.md) |

---

## Open decisions

1. **Single Integration project vs split** `OrcaCore.Hosting.Integration.Tests` + `OrcaCore.Stack.Integration.Tests` — start with one project; split when file count > 30.
2. **Activation layer** — if durable interpreter/activation moves to hosting, INT-E2E tests should target host public API only.
3. **SqlServer** — exclude from default CI until store is real (R5 P0).
4. **Testcontainers Ryuk** — enable cleanup on Windows; document `TESTCONTAINERS_RYUK_DISABLED` only for local debug.
