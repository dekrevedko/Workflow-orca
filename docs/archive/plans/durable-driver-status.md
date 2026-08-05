# Durable Driver Implementation Status

Status as of 2026-07-13 on `feature/v3-rebuild`. The repository root is the active
implementation. This document describes the structured-fiber driver that replaced the
provisional cursor interpreter.

Normative sources:

- [Durable driver requirements](specs/16-requirements-durable-driver.md)
- [Selected-mode capability matrix](specs/17-selected-mode-capability-matrix.md)
- [Structured-fiber OpenSpec change](../openspec/changes/archive/2026-07-15-adopt-structured-fiber-execution/)
- [Durable lane host](durable-driver-lane-host.md)
- [Development store reset](durable-development-store-reset.md)

## Current architecture

Definitions are compiled before registration into one immutable `CompiledWorkflowPlan`.
The compiler validates mode capabilities, root entry/exit, branch returns, merge contracts,
serializer/copy contracts, limits, progress boundaries, and structural continue-as-new.
The plan carries a compiler format and canonical fingerprint.

Both engines execute the same linear fiber and recursive execution-scope model:

- one runnable fiber receives one bounded quantum at a time;
- branch and item fibers own private serialized state and typed results;
- `Parallel` joins in authored result order;
- `WhenFirst` selects one deterministic terminal winner and cancels every loser;
- scope joins run one explicit replacement-state merge;
- blocked waits, timers, jobs, resources, child groups, retries, and pending resumes carry
  explicit fiber/scope ownership.

Durable commits persist `DurableExecutionEnvelopeV2` together with business state. The
envelope contains instance/generation binding, plan binding, fibers, scopes, scheduler
position, loop and scope-entry sequences, results, ownership, and diagnostics. Runtime
identities derive from committed generation, parent fiber, scope plan, and scope-entry
sequence, so host replacement and duplicate continuation claims reuse them.

## Implemented durable behavior

- Linear steps, `If`, `While`, cooperative `Yield`, waits, delays, and root `End`.
- Nested `Parallel`/`WhenAll` and `WhenFirst` with isolated state, typed results, deterministic
  scheduling, strict cleanup, and one parent continuation.
- Retry, backoff timer, timeout deadline, operator cancellation, failed-attempt rollback, and
  stable logical operation identity across restart.
- Structural root-only `ContinueAsNew` after quiescence, with generation increment before the
  new root identity is minted.
- Child workflow and child-group dispatch as owned obligations.
- External jobs and durable resource acquisition/release with explicit ownership.
- Saga compensation eligibility and transfer across structured scopes.
- Segment command/time budgets, successor continuation records, conflict reload/retry, poison
  accounting, parked diagnostics, and explicit re-arm.
- Correlation-targeted and definition-fanout event routing, management, hosted continuation
  pumping, and driver telemetry.
- Format-2 round trips and host-replacement coverage for in-memory, PostgreSQL, and SQL Server
  providers.

Ephemeral `ForEach` is implemented as a dynamic isolated-item scope. It is absent from the
durable builder and compiler-rejected in durable mode. Durable fanout uses child workflows.

## Removed legacy surfaces

The following production paths no longer exist:

- `DurableDriverCursor`, cursor frames, cursor split/join, candidate scanning, and
  `MergeCompletedCursors`;
- the format-1 `DurableExecutionEnvelope` model and source-generation metadata;
- the durable catalog fallback to a cursor executor;
- shared-state ephemeral `Parallel`, `WhenFirst`, and `ForEach` nodes/runners;
- `WhenFirst.Ignore`, `WhenFirst.LetRemainingComplete`, and
  `ForEachResidualPolicy.LetRemainingComplete`.

A format-1 content type is recognized only to produce an explicit parked diagnostic. It is
never deserialized or resumed. Development stores and copied fixtures must be reset as
described in [the reset procedure](durable-development-store-reset.md).

## Regression coverage

The structured-fiber suites cover:

- stable plan/scope/fiber identities across replay, loop re-entry, duplicate claims, and
  alternating hosts;
- fairness and persisted scheduler rotation;
- crash points after scope creation, branch return, pre-merge, post-merge response loss, and
  sibling turns;
- nested loser cleanup and the former deferred-parallel/ready-WhenFirst stranding shape;
- one deterministic winner under conflicts and same-transition ties;
- branch alias isolation, authored-order results, merge exceptions, and no branch rerun;
- dynamic `ForEach` admission, ordering, strict `WhenAny`, failure policies, and cleanup;
- zero orphan waits, timers, jobs, resources, child groups, and retry obligations;
- plan fingerprint/compiler format/definition mismatch parking;
- retry/timeout/cancellation and command/elapsed segment boundaries.

Repository guards reject references to retired cursor execution and require durable
capability validation at compilation/registration rather than runtime feature parking.

## Current verified baseline

Verified during the structured-fiber apply on 2026-07-13:

| Project | Result |
|---|---:|
| `OrcaCore.Core.Tests` | 349 passed |
| `OrcaCore.Engine.Ephemeral.Tests` | 153 passed before selected-policy additions; final rerun pending |
| `OrcaCore.Acceptance.Tests` | 71 passed before selected-policy migration; final rerun pending |
| `OrcaCore.Engine.Durable.Tests` | 273 passed |
| `OrcaCore.Benchmarks` | Release build clean |
| `OrcaCore.slnx` | Release build clean before final documentation/policy pass |

Provider and full-solution verification is the remaining apply gate. Counts above are evidence
for completed runs, not a substitute for the final clean-container pass.

## Remaining apply gates

- Complete final canonical documentation, links, samples, and feature-matrix validation.
- Run all root suites with Release warnings treated as errors.
- Run PostgreSQL and SQL Server integration/provider suites against clean containers.
- Run the seeded reference-model comparison harness and final orphan/continuation invariants.
- Record the final verified baselines in the implementation review status.
