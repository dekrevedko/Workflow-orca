# Section 7 Provisional Test Retirement Record

Date: 2026-07-30

This is an implementation audit record, not an independent approval or exit verdict.

## Why these tests are no longer compiled

Section 7 enforces the exact package graph and exactly two production friend-assembly edges. Test
assemblies are deliberately not friends of product assemblies. The retired test files directly
target provisional internals, deleted saga/DAG and catch-all-hosting surfaces, compiled IR, or
management/projection contracts replaced by the approved application facade. Keeping them green
would require compatibility shims or test-only `InternalsVisibleTo` edges forbidden by the
greenfield contract.

The project exclusions are explicit and reviewable. They remove 128 files containing 707 prior
xUnit test cases:

| Test project | Retired files | Prior test cases |
| --- | ---: | ---: |
| `OrcaCore.Core.Tests` | 22 | 144 |
| `OrcaCore.Engine.Ephemeral.Tests` | 28 | 171 |
| `OrcaCore.Engine.Durable.Tests` | 52 | 295 |
| `OrcaCore.Acceptance.Tests` | 21 | 65 |
| `OrcaCore.Hosting.Tests` | 4 | 16 |
| `OrcaCore.Providers.PostgreSql.Tests` | 1 | 16 |
| **Total** | **128** | **707** |

These are retired tests, not passing tests. Their exclusion reduces legacy-suite counts and must be
considered explicitly by the independent reviewer.

## Replacement evidence

The Section 7 target replaces internal-coupled coverage with consumer- and contract-facing
evidence:

- 158 infrastructure guards covering package identity, dependency and friend edges, public
  signatures, architecture, exact hosting/provider roles, fixed codec, and operational telemetry;
- 37 executable current-physical Section 7 scenario drivers;
- exact-package green and ExpectedRed consumer fixtures plus positive and forbidden compile lanes;
- 78 provider-certification cases against the exact provider abstractions;
- the live PostgreSQL provider suite, including role registration and governance persistence;
- surviving product suites that compile only against the supported package/friend graph.

The independent exit review must determine whether this replacement evidence is sufficient. This
record does not make that determination.
