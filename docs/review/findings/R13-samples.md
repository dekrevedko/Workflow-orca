# R13 — `samples/` examples audit — Findings

> **Scope:** `v3-gpt/samples/**` — `OrcaCore.Examples` (console walkthrough), `OrcaCore.SampleHost`
> (minimal host), and the `OrcaCore.Dashboard` sample's workflow services (now relocated under
> `samples/`, resolving the R12 §15.10 dashboard-scope decision toward "reference sample").
> **Lens:** examples are the first code users copy — correctness, idiomatic API usage, and whether
> they teach safe patterns weigh heaviest.
> **Baseline:** `OrcaCore.Examples` builds under `-warnaserror` and runs all six examples to
> completion; the fixes below are applied and re-verified.

## Findings

### [P2] ForEach body cannot identify its item — example taught item identity via a mutable shared counter — `v3-gpt/samples/OrcaCore.Examples/ExampleRunner.cs:387`
- **Requirement/convention:** CR-044 (branch-state mutations serialized), 08 CP (ForEach); example-quality
- **Evidence:** `StepContext<TState>` exposes only `State`, `ResumedEvent`, `TimeProvider` — no per-item / branch accessor. The fanout example's `CountFanoutItemStep` did `var itemIndex = context.State.ProcessedItemCount++;` then indexed `context.State.Items[itemIndex]` to name "its" item, under `ForEach(..., maxConcurrency: 2)`.
- **Failure scenario:** it is correct only because the demo step is fully synchronous (`ForEachNodeRunner` awaits each item sequentially, so no overlap). The moment a ForEach body suspends (any real I/O) under `maxConcurrency>1`, item sequences interleave and the call-order counter no longer maps to item identity — silent misindexing. ForEach's primary purpose is exactly concurrent I/O fan-out, so the pattern breaks under its main use case.
- **Root cause:** an **API gap** — there is no correct way for a ForEach body to know which item it is processing.
- **Fix applied:** the example now increments a completed-item counter and logs "processed a fanout item" (no false per-item lookup) with a comment explaining the limitation. **API recommendation (open):** give the ForEach body a per-item accessor (e.g. `StepContext.CurrentItem` / a typed ForEach body context, or pass the item through partitioned state) so item-scoped work is expressible safely.
- **Confidence:** CONFIRMED (traced `ForEachNodeRunner`/`ForEachWorkScheduler`; verified synchronous-only safety).

### [P2] Business step failures threw `WorkflowDefinitionException` — wrong exception category — `v3-gpt/samples/OrcaCore.Examples/ExampleRunner.cs:350`
- **Requirement/convention:** CR-014 (failure semantics); example teaches which exception to throw
- **Evidence:** `ValidateOrderStep` and `FailSagaStep` returned `StepResult.Failed(new WorkflowDefinitionException("Order total must be positive." / "payment capture failed"))`. `WorkflowDefinitionException` is semantically for **malformed definitions**, not runtime business failures.
- **Failure scenario:** users copying the example throw the definition-error type for ordinary business failures, muddying error triage (a "definition" exception on a live instance implies an authoring bug, not a rejected order).
- **Fix applied:** switched both to `new OrcaCoreException(...)` (the constructable general base). **Recommendation (open):** consider a dedicated `WorkflowStepException` / business-failure type so examples and users have a semantically precise option rather than the base class.
- **Confidence:** CONFIRMED (`OrcaCoreException` is a public constructable base).

### [P3] `Short()` id helper rendered every instance identically — `v3-gpt/samples/OrcaCore.Examples/ExampleRunner.cs:308`
- **Evidence:** `value[..8]` took the **leading** 8 chars of a version-7 GUID; v7 ids share a leading time prefix, so all instances in one run printed `019f3407`, defeating the id column in a walkthrough about inspecting instances.
- **Fix applied:** use the trailing 8 chars (`value[^8..]`); re-run now shows distinct ids (`072c98e1`, `3b701813`, `ea86c55e`).
- **Confidence:** CONFIRMED (observed in run output).

### [P3] Durable example leaves the instance `Running` with no explanation — `v3-gpt/samples/OrcaCore.Examples/ExampleRunner.cs:190`
- **Evidence:** example 06 defines `Init → End("DurableStarted")` but the snapshot reports `Running` forever, because `DurableWorkflowRuntime.StartOrGetAsync` records the start event and does **not** interpret the definition to its `End` (durable execution is command-driven). A learner expects `Completed`.
- **Fix applied:** added a comment explaining the command-driven model and why the snapshot is `Running`. (No behavior change — this is faithful to the durable API.)
- **Confidence:** CONFIRMED (run output: stream 8, `Running`).

### [P3] Telemetry `Information` logs pollute the walkthrough output — `v3-gpt/samples/OrcaCore.Examples/ExampleRunner.cs:263`
- **Evidence:** example 06's `Host.CreateApplicationBuilder()` enables console logging, so `OrcaCoreTelemetryObserver[1001]` Info lines interleave with the curated example output.
- **Recommendation (not applied):** set the examples host min level to `Warning` (`builder.Logging.SetMinimumLevel(LogLevel.Warning)`) so the walkthrough stays legible; left as a suggestion since it only touches sample host config.
- **Confidence:** CONFIRMED.

## Positives verified (no finding)

- **`OrcaCore.SampleHost`** — minimal, correct `AddOrcaCore().AddOrcaCoreHostedServices()` + `RunAsync()`; the canonical host registration. Good.
- **`KubernetesWorkflowSampleService`** (dashboard sample) — the kubectl invocation is
  **injection-safe**: `ProcessStartInfo.ArgumentList` with `UseShellExecute = false` (no shell
  concatenation); Job/node names are sanitized to `[a-z0-9-]` via `JobName` before templating into
  YAML and the pod `/bin/sh -c` args; stdout/stderr reads are started before `WaitForExit`
  (no pipe deadlock); kubectl-absent and timeout paths return typed failures. Solid for a sample.
- **Examples build `-warnaserror` clean and run end-to-end**; saga compensates in reverse
  (reserve→authorize→refund→release, `Compensated`), start idempotency dedups
  (`duplicate-created=False`), and inbox dedup returns `NoOp` on the repeated completion.

## Coverage note

Verified the six `OrcaCore.Examples` scenarios (ephemeral authoring, event/correlation, ForEach
fanout, transient timers, ephemeral saga, durable host APIs), `OrcaCore.SampleHost`, and the
dashboard's Kubernetes/read-model sample services. Not deeply reviewed: the dashboard Razor
components/UI (out of scope for API-teaching review) and `DashboardReadModel` tile math. Two open
API recommendations surfaced (ForEach per-item accessor; a business-failure exception type) —
neither is a defect in the engine, but both would remove the need for the fragile patterns the
examples previously showed.
