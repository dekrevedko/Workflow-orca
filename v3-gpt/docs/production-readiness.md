# OrcaCore Production Readiness Notes

OrcaCore is still pre-production in this implementation track. Public package publishing,
package IDs, signing, SourceLink release configuration, and semantic-versioned package
release automation are deferred until the owner reopens packaging.

## Delivery Guarantees

- Durable mode targets at-least-once outbox dispatch.
- Durable mode targets exactly-once committed effect per workflow instance through
  event-id deduplication and serialized expected-version commits.
- OrcaCore does not claim exactly-once delivery to external systems. External consumers
  must remain idempotent.
- Ephemeral mode is in-process only. Ephemeral lifecycle events and saga audit state are
  queryable only while the process-owned runtime state exists.
- `Wait` and `WaitLong` remain separate public concepts. `WaitLong` is durable-only and
  restart-safe; regular `Wait` does not imply cold eviction or restart survival.
- Durable history retention is policy governed. Providers are not required to retain full
  stream history forever, but must preserve the active retention window and essential
  operational facts required for management, recovery, audit, and compliance-oriented
  inspection.

## Security Checklist

- The library exposes no network listener by itself. Host applications own every HTTP,
  queue, worker, dashboard, and operator-facing endpoint.
- Host applications own authentication, authorization, tenant isolation, rate limits, and
  destructive-operation approval for management commands.
- Operator actions such as pause, resume, terminate, retry, retention purge, force release,
  and pool resize must be protected by host policy before exposing them outside trusted
  process code.
- Payloads are opaque data at provider boundaries. Do not add dynamic type resolution from
  untrusted payload content.
- Providers and dispatchers must keep credentials in host configuration or platform secret
  stores. Do not bake credentials into workflow definitions or serialized payloads.
- Destructive retention commands must preserve active instances and in-flight dispatch
  safety.

## Versioning Policy

- This workspace has no published package stability promise yet.
- API shapes, provider schemas, and projection formats may change before the packaging
  gate.
- Breaking changes must update implementation docs, provider certification expectations,
  and sample host guidance in the same change.
- Provider schemas need explicit migration notes before any production packaging gate.
- DynamoDB implementation is deferred. Shared provider contracts should remain compatible
  with a future adapter, but no AWS package or table design is part of this run.

## Benchmarks

BenchmarkDotNet scenarios live in `v3-gpt/benchmarks/OrcaCore.Benchmarks`. Normal PR CI
builds the benchmark project but does not run benchmarks.

Run a short local smoke pass:

```powershell
dotnet run --project v3-gpt/benchmarks/OrcaCore.Benchmarks/OrcaCore.Benchmarks.csproj -c Release -- --filter *ProviderCommitBenchmarks.AppendCommitBatch* --job Dry
```

Run the full local benchmark suite:

```powershell
dotnet run --project v3-gpt/benchmarks/OrcaCore.Benchmarks/OrcaCore.Benchmarks.csproj -c Release
```

The current suite covers the ephemeral execution loop, provider
serialization/materialization, management query/projection path, resource pool and timer
scheduling, and provider commit path. External provider benchmark profiles and hard
threshold gates are later production-readiness work.

## Sample Host

The sample host at `v3-gpt/samples/OrcaCore.SampleHost` uses Microsoft hosting and explicit
OrcaCore service registration:

- `AddOrcaCore()` for engines and in-memory provider defaults.
- `AddOrcaCoreHostedServices()` for the outbox pump, timer, and operational sweep service
  registrations.

The sample intentionally uses in-memory providers so it can build and smoke-test without
external infrastructure.
