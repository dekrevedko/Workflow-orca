# OrcaCore Ephemeral Engine Developer Guide

This guide describes the selected v1 application surface. The exact authority is the
[selected-mode capability matrix](specs/17-selected-mode-capability-matrix.md) together with the
[public authoring declaration companion](specs/17-public-authoring-contract.cs). If an example or
implementation detail conflicts with either source, the matrix and declaration companion win.

## Packages and host role

An ephemeral application references `OrcaCore` for authoring and application contracts and
`OrcaCore.Engine.Ephemeral` for the engine host role. Register that role with
`AddOrcaCoreEphemeralEngine(EphemeralEngineHostOptions)`.

`EphemeralEngineHostOptions` is programmatic configuration. It contains:

- `StructuredExecution`, including the positive per-instance execution-path ceiling;
- `TransientPools`, the copied host-local catalog used by named-step transient throttles;
- nullable `TerminalRetention`, the optional immutable host-wide terminal policy.

`EphemeralTerminalRetentionPolicy` is implemented in `OrcaCore.Hosting`, owned by this engine
package. For example, set `TerminalRetention = EphemeralTerminalRetentionPolicy.Create(128,
TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(1))`. The source, exact API baseline and compile
fixtures include these members together. The [fresh package-consumer experiment](../openspec/changes/add-ephemeral-terminal-retention/artifacts/final-consumer-2026-10-18/experiment.md)
qualifies candidate local packages. The published `0.1.0-preview.1` packages predate this API;
use a package release containing retention before compiling these options against NuGet.org.

The engine role is mutually exclusive with the durable engine role in one service collection.
There is no catch-all registration method, configuration-binder overload, hosted-service toggle,
or serializer replacement hook.

The separate shutdown amendment specifies seven engine-owned OrcaCore.Hosting
types, including IEphemeralWorkflowHostLifetime, automatically registered by this
same role. **Shutdown implementation and packages are pending**; doc 17 defines the
target surface. This is no additional option/registration, fingerprint or API in
published 0.1.0-preview.1. The authoring companion adds no hosting declaration.

## Authoring model

Start a definition through `Workflow.Ephemeral<TState>(DefinitionId, DefinitionVersion)`. The
staged builder requires one root initialization and one root terminal. `Build()` throws on invalid
structure; `TryBuild()` returns the same ordered diagnostics without mutating the authoring session.

The v1 ephemeral mode supports:

- named steps and ephemeral lambda steps;
- ordinary `Wait` and `Delay` boundaries;
- nested `If` and linear nested sequences/bodies;
- root `While`, root fixed `Parallel`, and root bounded `ForEach`;
- workflow deadlines plus eligible step retry, timeout, and transient-concurrency decorators;
- named-step transient-pool throttling and typed `End` output.

Fixed branch and item bodies receive isolated state. A successful join changes the parent only
through its explicit merge. `Parallel` results preserve authored branch order. `ForEach` admits
items deterministically by item index and applies the lower of the host execution-path ceiling and
the node-local admission limit.

Authoring handles are session-, epoch-, and scope-bound. After a root terminal freezes the
definition, or after a join advances its authoring epoch, stale or escaped handles are rejected
without changing the graph.

## Step context and state

Each attempt runs on a codec-detached copy of the last committed root state. Only the winning
attempt commits its replacement. `StepContext<TState>` exposes the attempt-local state and permits
explicit replacement through `ReplaceState`.

When a wait resumes, `StepContext.ResumedEvent` is a detached `EventEnvelope` only for the first resumed
step. Its metadata is read-only and its payload is obtained through `GetPayload<TPayload>()`. Later
steps see no resumed event. `StepContext.ForEachItem` exposes the stable authored item index inside
an item body and is `null` outside that scope.

Application state, inputs, event payloads, and typed outputs use the fixed certified
`orcacore-json-v1` codec. Unsupported graphs fail before commit. The codec has no application or DI
replacement seam.

## Registration, start, and inspection

Register a built definition through `IWorkflowDefinitionRegistry`. Registration returns a closed
result: registered, host-incompatible, or fingerprint conflict. Use `GetHandleOrThrow()` only as a
cast-free success projection after inspecting or intentionally accepting that result.

Typed definition handles start or reopen instances without exposing an engine implementation.
Start idempotency binds the key to the definition fingerprint and deterministic input fingerprint.
An instance handle provides:

- detached snapshot and typed root-state queries;
- nonblocking typed output inspection and notification-driven `WaitForOutputAsync`;
- cooperative cancellation request;
- immediate fenced termination.

Local cancellation of `WaitForOutputAsync` cancels only the caller's wait, not the workflow.

The shutdown contract adds a compatible-registration/start rejection only from
Closed, preserving static HostIncompatible failures. A queued start still before
Init rejects at Closed; initialization already admitted may complete its first step
and return Accepted during Closed. Acceptance/abort elects only at Stopping, so
awaiting pending starts during drain does not manufacture a host-stopping error
after the first step's effects. Authoritative original errors/cancellation remain.
These are implementation-pending semantics, not current binary behavior.

## Retained-instance lifetime

The null/omitted policy keeps accepted instances and their keys for the host lifetime and adds
no retention timer. An opted-in `EphemeralTerminalRetentionPolicy.Create(maxRetainedInstances,
maxAge, sweepInterval)` sets one host-wide settled-terminal count and maximum age, across all
definitions. Count must be at least one; age and interval must be positive, with interval no
greater than age or one day. Parameters fail in signature order. All positive intervals within
those limits are valid, even one tick; tiny intervals can keep sweeps busy and consume CPU, so
choose an operationally sensible cadence. Registration owns a copied policy value and includes
enabled state and all values in role agreement.

Only physically settled immutable terminals without admitted-operation pins are eligible.
Active/waiting/delayed/unsettled work remains protected. Age uses monotonic settlement time,
does not refresh on reads/replays, and is enforced by idle sweeps. Count pressure can evict sooner.
Retirement removes runtime payloads, facade/instance/start bindings, router deduplication and
obsolete timer/notification records together; it retains definitions and creates no unbounded
replacement tombstones. Protected/pinned work and caller-held copies can exceed the retained
count, and payload size/process memory are not capped by it. Retirement never frees a physical
slot early or proves application-owned external effects stopped.

A handle is identity, not a lifetime lease. A query/wait admitted before retirement captures a
detached result under a transient pin; local wait cancellation releases only its own pin.
Operations admitted after retirement throw `WorkflowInstanceNotFoundException` for the original
ID. Keeping a handle does not keep payloads alive; already detached results remain usable.

There is a real gap between `StartOrGetAsync` returning `Accepted` and a direct or
`start.WaitForOutputAsync(token)` call being admitted. Other completions can retire that already
completed job by count during the gap. **Not-found after `Accepted` does not mean the run never
started.** Its step may have produced an effect, and replaying the now-expired key starts a fresh
ID with `WasExisting = false` and may repeat that effect. Size `MaxRetainedInstances` above the
completions possible during the caller's start-to-observe gap; preserve independent effect
deduplication or choose durable coordination when longer guarantees are required. A larger
retained count reduces exposure but does not establish effect-safe retry.

The contract also corrects unreachable provisional-start orphans with retention off. Start-token
cancellation after runtime creation but before acceptance, or an exception after `Init`, fences
the unpublished unit to `Terminated` through its lane (preserving any prior terminal status),
returns the original error, publishes no key/handle, and eagerly releases owned data after
physical settlement. Acceptance winning the race returns `Accepted`; later cancellation of the
start token is not workflow cancellation. Internal abort telemetry is separate from a caller's
termination command. The same consumer experiment verifies idle expiry and physical-work protection.

Background sweep cleanup failures are contained and do not stop later sweeps. The existing
`orca.lifecycle.events` instrument records internal `cleanup-failed` observations with origin
`retention-sweep`; labels contain no instance IDs, start keys, payloads or exception messages.
Diagnostic listeners cannot propagate failures through the timer callback. This does not
hide cleanup exceptions from synchronous caller-triggered lifetime operations.

For the implementation-pending shutdown protocol, Closed keeps normal accepted
queries/waits/commands/ingress, retirement and key lifetime; it never queues host
cancellation. Pending initialized starts may publish Accepted during Closed. Stopping
alone seals new live operations and elects provisional abort, while retaining every
physical owner and pre-admitted pin. Completed freezes remaining metadata; detached
values survive, but a fresh terminal output query still rejects before decoding.
The ordinary start-to-observe retirement gap remains, so retain independent effect
deduplication and do not use not-found/host-stopping as safe retry instructions.

## Waits and external event ingress

Ephemeral `Wait` is an in-process suspension owned by the ephemeral engine. The application surface
does not expose a durable acknowledgement or replay contract for sending events into that
process-local runtime.

External self-routing ingress is a durable-host capability in `OrcaCore.Durable.Hosting`.
`IWorkflowEventIngress` accepts `WorkflowInboundEvent` through exactly four routes: direct instance,
correlation, definition fanout, and start-or-deliver. Durable acceptance persists ownership before a
matching wait is required; `Accepted` and `Duplicate` are the only outcomes that permit a broker
source to acknowledge. This durable boundary does not turn the ephemeral engine into a persistent
mailbox.

## Deliberately unavailable in v1

The future-capability registry is
[§13.4, “Future-capability registry”](specs/13-phasing-and-open-questions.md#134-future-capability-registry).
It records deferred capabilities—including `WhenFirst`, Saga, public child/external-job authoring,
nested structural fan-out and broad lifecycle/management operations—with
rationale and re-entry criteria. They are not callable v1 APIs. `WaitLong` and authored `Yield` are
removed rather than deferred and have no alias or placeholder.

## Verification

The consumer-facing contract is enforced by the developer-surface guards and fresh-package compile
fixtures. Runtime behavior is covered by the active acceptance, ephemeral-engine, and behavior-
scenario suites. Run commands from the repository root; the current gate request records the exact
configuration and expected counts for the target under review.

The superseded pre-v1 guide is preserved, unchanged, at
[`archive/plans/ephemeral-engine-developer-guide.md`](archive/plans/ephemeral-engine-developer-guide.md).

## Shutdown drain recipes (implementation pending)

These describe PR-041/MG-066–069 after source/package qualification. The seven
declarations, callbacks and owning tests are not implemented by canonical text.

1. **Generic Host ordered drainer.** Register the engine role, then an application
   IHostedService drainer. Set/retain HostOptions.ServicesStopConcurrently=false.
   Reverse stop order calls the drainer before the engine. ApplicationStopping only
   closes new starts/registrations; initialized pending starts and accepted work
   continue. Stop the application's submitters, join pending starts, and await the
   returned handles, preserving event delivery/dependencies needed by those workflows.
   Use the drainer StopAsync token, not the already-cancelled ApplicationStopping
   token, for local drain waits. Return on completion or budget expiry so the engine
   StopAsync can enter Stopping and cancel/join anything left. All phases share
   HostOptions.ShutdownTimeout; concurrent stops do not guarantee a drain window.
   Keep required ingress/dependency-service stops after drain. Never block an
   ApplicationStopping callback to perform this wait.
2. **Explicitly owned host.** Keep IHost in an application session/supervisor; start
   it, stop submitters, await pending initialized starts and accepted handles with
   an app drain budget, then call the engine lifetime StopAsync. ApplicationStopping
   may already mean Closed without cancellation. No concurrent engine/IHost stop may
   race the intended drain. Retain IHost on Incomplete, reobserve later, and only after
   Completed finish IHost.StopAsync/other hosted stops and dispose. Catch/reobserve
   an incomplete explicit IHost.StopAsync when using the ordered drainer too.
3. **Plain DI.** There is no close-only public engine call. Close the application's
   own submission gate and join every submitter/pending start, then await accepted
   handles while keeping ingress/dependencies alive. Engine admission remains Open,
   so all submitters must be controlled. On drain completion or expiry call StopAsync
   to close and cancel/join anything left. Incomplete retains the actual provider,
   scopes and external dependencies; reobserve rather than dispose.

After Stopping begins new live handle/snapshot/state/output/wait/command/ingress
operations reject, preserving original-ID not-found/mismatch precedence. Admitted
operations still finish normally and are joined. After Completed there is no new
engine work, DI/user code, timer, diagnostic or commit: only passive registered
identity, previously detached values and cached Completed remain available.

Incomplete has Reason/PendingWorkCount, not stuck-instance identities. **Before
calling stop**, inspect existing snapshots/state and stuck-step/lifecycle diagnostics
to identify and record suspect instances. Once Stopping rejects live queries use
those captured facts and existing diagnostics, not a new stop-time instance list.
The shutdown metrics use bounded fixed labels and contain no IDs.

Migration: the former no-op hosted stop now throws on Incomplete, possibly aggregated
by Generic Host. Run/RunAsync still disposes in finally even if stop throws; it cannot
retain dependencies for noncooperative incomplete work. Do not use unconditional
using/finally around that outcome. One explicit owner serializes disposal; raw
provider disposal only closes as unsupported abortive teardown, not a join or veto.
Source tasks 3.1–3.5 must prove these recipes with candidate packages before this
section becomes executable release guidance.
