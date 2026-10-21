# Issue #2 canonical-transition public contract — 2026-10-19

Contract checkpoint 3c8d33c / approval 4bd7c3e. F2 is applied for transition review;
seven declarations remain exact, with no implementation or executable-baseline credit.

Contract review material, not implemented or approved source. All seven new public
types are owned by assembly/package `OrcaCore.Engine.Ephemeral`, namespace
`OrcaCore.Hosting`. `OrcaCore` and durable assemblies have zero additions. The
declarations below are exhaustive, including non-public construction restrictions.
They specify classes, not records: no generated equality, Deconstruct, Clone,
init setters or record constructors are added to the public API.

```csharp
namespace OrcaCore.Hosting;

public interface IEphemeralWorkflowHostLifetime
{
    ValueTask<EphemeralHostShutdownResult> StopAsync(
        TimeSpan timeout, CancellationToken cancellationToken = default);
}

public abstract class EphemeralHostShutdownResult
{
    private protected EphemeralHostShutdownResult();

    public sealed class Completed : EphemeralHostShutdownResult
    {
        internal Completed();
    }

    public sealed class Incomplete : EphemeralHostShutdownResult
    {
        internal Incomplete(EphemeralHostShutdownIncompleteReason reason, long pendingWorkCount);
        public EphemeralHostShutdownIncompleteReason Reason { get; }
        public long PendingWorkCount { get; }
    }
}

public enum EphemeralHostShutdownIncompleteReason
{
    DeadlineElapsed = 0,
    ObservationCancelled = 1,
    ShutdownFaulted = 2
}

public sealed class EphemeralHostStoppingException : global::OrcaCore.OrcaCoreException
{
    internal EphemeralHostStoppingException();
}

public sealed class EphemeralHostShutdownIncompleteException : global::OrcaCore.OrcaCoreException
{
    internal EphemeralHostShutdownIncompleteException(EphemeralHostShutdownResult.Incomplete result);
    public EphemeralHostShutdownResult.Incomplete Result { get; }
}
```

`EphemeralHostStoppingException.Code` is exactly `WF-EPHEMERAL-HOST-STOPPING`;
`EphemeralHostShutdownIncompleteException.Code` is exactly
`WF-EPHEMERAL-SHUTDOWN-INCOMPLETE`. Neither adds a Message/Code override, public
constructor, identity collection, operation-name field, exception serialization
hook or factory. Result constructors are runtime-only; the root cannot be
subclassed outside its assembly. Normal inherited System.Object/Exception members
are not additional declarations. The enum has exactly the three named values.

## Registration and defaults

The existing `AddOrcaCoreEphemeralEngine(EphemeralEngineHostOptions)` registers one
owned singleton `IEphemeralWorkflowHostLifetime` per root provider, backed by that
provider's actual engine. It is the same instance when resolved from child scopes.
Same-role/equal-options registration is still idempotent. An application-supplied
descriptor for this reserved service before the first role registration is rejected
with `ArgumentException`, parameter `services`, before service-collection mutation;
it is not silently adopted. Later manual descriptor replacement is unsupported.
Separate root providers receive independent lifetimes.

There is no new registration method, builder member, host option, mode flag,
policy class, serializer, dependency or friend. The existing role fingerprint
remains byte-identical: shutdown has one fixed cooperative-cancel policy, and
`timeout` is an observer budget, not an option or workflow deadline. No implicit
shutdown occurs merely by resolving the service or cancelling an output wait.

Host states progress irreversibly Open -> Closed -> Stopping -> Completed. Closed
refuses new/replayed starts and registrations but leaves accepted work operational;
it neither queues shutdown cancellation nor seals accepted-instance producers.
Stopping alone requests cancellation of remaining work and joins it. No new option,
public Close method, drain overload or drain result is introduced.

On Generic Host `ApplicationStopping`, if available, every admission/initialization/
attempt gate observes its cancelled token and establishes Closed, even if another
application callback delays engine notification. The bounded internal callback only
closes admission; it does not queue shutdown cancellation or join. Accepted-instance
attempts check Stopping, not merely this cancelled token, and may continue in Closed.
Initialize the engine/coordinator before arming the subscription; late resolution
on an already-stopping host closes new-start/registration admission, without itself
starting cancellation. Neither this callback nor close waits on a semaphore/lane
or invokes user code. Only the engine's own hosted StopAsync or an admitted public
StopAsync enters Stopping, closing first if needed. The hosted token bounds its
observation. Plain-DI owners control submitters and call StopAsync explicitly;
provider disposal only closes admission as abortive teardown, never starts this join.

## Public StopAsync parameter and race contract

1. Validate `timeout` before any effect: it must be `Timeout.InfiniteTimeSpan`, or
   between `TimeSpan.Zero` and `TimeSpan.FromMilliseconds(4_294_967_294)` inclusive,
   using exact ticks for the boundary. Other values throw
   `ArgumentOutOfRangeException(nameof(timeout))`. Zero closes, enters Stopping and makes
   an immediate observation; infinite is an explicit unbounded observation.
2. An already-cancelled token throws `OperationCanceledException` carrying that
   token, without initiating shutdown. This also applies to an already settled
   lifetime. Then check root disposal (`ObjectDisposedException`). If Completed
   already holds, return its cached detached result without reading TimeProvider
   (which may be application code) or creating observation resources.
3. An admitted invocation closes admission if needed and enters Stopping, or joins
   that already irreversible operation. Closed alone has not started it. Its finite
   budget starts at invocation admission on the registered
   TimeProvider's monotonic clock, including scheduling and cancellation work.
4. For an admitted observer, decide under one observation boundary: an accounting
   fault wins; otherwise a proven stable join wins; otherwise observer cancellation
   wins over an expired budget. These produce respectively `Incomplete` with
   `ShutdownFaulted`, `Completed`, `Incomplete` with `ObservationCancelled`, or
   `Incomplete` with `DeadlineElapsed`. Cancellation after admission is a result,
   not an OperationCanceledException. No result claims a hard real-time guarantee.
5. `PendingWorkCount` is a detached, nonnegative count of outstanding host-owned
   quiescence obligations at that decision, not unique steps, physical threads,
   bytes, or retained instances. Handoff/cleanup/callback owners count separately.
   A lost accounting proof retains a fault obligation until the root ends, so a
   faulted result remains Incomplete even if physical bodies appear finished.
   Incomplete never has zero pending obligations. Completed has no outstanding
   obligations, no further producer and no result-owned reference to runtime data.

## Hosted observation and errors

The engine's internal hosted-stop entry closes admission and enters Stopping even
if its supplied token is already cancelled: this token bounds observation, not the
authority to start cancellation/join. It does not route through the public pre-cancelled no-effect path.
It returns normally only for Completed and otherwise throws
`EphemeralHostShutdownIncompleteException` containing the detached result. Generic
Host may aggregate that exception. The shared operation continues independently.
The hook never synchronously executes application cancellation handlers. Ordinary
Run/RunAsync now surfaces an incomplete hosted stop as an exception (possibly
aggregated) instead of the former no-op stop's clean return; its disposal finally
still runs. The migration guide must describe both effects.

## Admission outcomes and preservation of prior contracts

- All caller argument checks and already-cancelled operation tokens retain their
  existing precedence. A start then acquires host admission **before** input
  fingerprint serialization, the facade start semaphore, Init or definition work.
- After closure, all `StartOrGetAsync` calls, including retained-key replay and
  changed-input conflict attempts, throw `EphemeralHostStoppingException`. Closure
  does not expire a key, remove a retained result or mean an accepted start never ran.
- Registration retains its existing mode/static-capability compatibility checks
  first; their HostIncompatible outcomes remain unchanged. An otherwise compatible
  registration after closure throws the same stopping exception before mutation
  or fingerprint-conflict/idempotent-success publication. Static compatibility is
  metadata-only and invokes no step/DI/user callback. Obtaining an already registered
  definition handle remains an identity lookup.
- Instance operations preserve original-ID not-found/retirement and existing
  definition/state-type mismatch precedence using metadata-only checks. Closed
  alone adds no instance-operation rejection. Stopping/Completed then reject each
  otherwise valid live operation listed below as host-stopping, before codecs,
  user callbacks, DI use, pins, lane work, mutation or commits.
- Already admitted operations settle under their normal result/error contract
  and participate in join, including operations admitted during Closed. An output
  wait keeps its pin until its normal terminal result or caller cancellation;
  beginning Stopping neither abandons it nor rewrites its normal outcome.
- A queued pre-Init start losing initialization admission to Closed runs no Init.
  A start whose initialization admission won before Closed proceeds normally and
  may publish Accepted during Closed. Closing neither aborts it nor converts its
  pending-start success to host-stopping. Acceptance and Stopping instead share the
  final election: acceptance winning publishes once, then remaining workflow work
  is cancelled separately; only a still-unaccepted start losing to Stopping is
  independently fenced through its lane, preserving immutable status and an already
  authoritative original error/caller cancellation, otherwise host-stopping. Both
  retention modes preserve physical capacity until actual settlement. Init/attempt
  admission winning its gate may begin/return later outside it and remains owned.
- Open and Closed preserve issue #1 retention, replay-key lifetime, mismatch and
  detached result semantics for accepted instances; start replay is still refused
  in Closed. Closure is never an effect-safe replay instruction.

## Exact operation boundary

These rows apply before owner disposal and are exhaustive for engine-facing operations. Ordinary argument/token
checks and the metadata-only original-ID/mismatch precedence above still apply.
Closed is admission closure; it is not producer sealing or completed shutdown.

| Operation | Closed, before Stopping | Stopping and after Completed | Reason for rejection from Stopping |
|---|---|---|---|
| New/replayed/conflicting StartOrGetAsync | Host-stopping | Host-stopping | No new workflow admission or input-codec execution |
| Compatible registration, including idempotent replay | Host-stopping | Host-stopping | No definition mutation or fingerprint work |
| GetInstanceAsync / live instance-handle acquisition | Normal original-ID lookup | Host-stopping | Acquiring a live runtime handle/pin can create work |
| Snapshot query | Normal accepted-instance semantics | Host-stopping | Mapping/inspection can invoke runtime work or codecs |
| State query | Normal accepted-instance semantics | Host-stopping | State extraction can use codecs/dependencies |
| Output query | Normal accepted-instance semantics | Host-stopping | Even a terminal output can require decoding |
| New output wait | Normal accepted-instance semantics and pin | Host-stopping | Creates a pin, waiter or decoding work |
| Cancellation/termination command | Normal accepted-instance semantics | Host-stopping | Can signal callbacks, enter a lane or commit |
| Event ingress to accepted instances | Normal accepted-instance semantics | Host-stopping | Can invoke handlers, resume attempts or commit |
| Already-initialized/accepted-instance delay/timer/retry/resource/yield continuation | Normal semantics, including new attempts | No new business admission; join already-admitted work and required fences | Producers must seal before stable zero |
| Retention sweeps/cleanup and admitted-operation handoffs | Normal policy and ownership | Seal/join before Completed; none after | No late cleanup callbacks or commits |
| Already registered definition-handle lookup on an already-resolved registry | Passive identity lookup | Passive identity lookup only | No registration, codec, user callback, DI resolution, timer, owned task or commit is allowed |
| Reading previously detached snapshots/state/output/results or handle identity | Caller-owned data | Caller-owned data | No engine operation occurs |
| Repeated StopAsync | Enters Stopping | Observes same operation; cached Completed after proof | Completed fast path must not start work, arm timers, register callbacks or emit diagnostics |

After Completed, *every* engine-facing path is limited to pure argument/token and
immutable identity/error checks, passive registered-handle identity lookup, and
return of the detached cached shutdown result. All live instance-handle acquisitions, queries and commands above
fail, even against terminal retained data; callers may read only values detached
before completion. No path may create owned work, access DI dependencies, invoke
user code, rearm timers, dispatch notifications/diagnostics or commit. Late sealed
trampolines only test the gate and return. Retained metadata is frozen until ordinary
owner disposal; no background retention resumes. Owner disposal may release already
joined internal storage and application-owned dependencies, without starting engine
work. Resolving new services or unrelated application work is not engine permission.

## DI ownership and supported disposal order

The library owns its engine/coordinator/timers, not the application's root provider,
host or arbitrary step dependency. The application has one serialized disposal
owner. It retains the root, every relevant scope and externally supplied dependency
until Completed, then awaits other hosted-service stops before disposing them.
No `using`/`await using`/unconditional finally may release that owner on Incomplete.
Keep it in an application-owned session/supervisor field; a transient local plus
discarded shutdown task is not an owner. Observe again with another budget or an
explicit infinite observation when the remaining work returns.

### Supported drain recipes

**Generic Host hosted drain:** register the engine role first, then an application
IHostedService drainer, so the host calls the drainer's StopAsync before the engine's
in reverse registration order. Set/retain HostOptions.ServicesStopConcurrently=false;
concurrent stops can enter engine Stopping while the drainer still awaits and do not
guarantee a drain window. On ApplicationStopping the engine is only Closed. The
drainer stops its submitters, owns pending start calls and accepted handles, and
awaits their natural completion (including required event delivery) in its StopAsync
using the supplied host token. Do not use the already-cancelled ApplicationStopping
token to cancel workflow bodies or these drain waits. The drainer returns when
finished or its budget expires; on expiry it releases only its local observation,
not the provider or runtime ownership, so the engine's subsequent StopAsync can
enter Stopping. All drains, other hosted stops and engine observation share the
single HostOptions.ShutdownTimeout budget; a drainer cannot promise the engine a
fresh timeout. Keep dependencies/event producers needed by draining work alive and
order their stops after the drainer. Do not block ApplicationStopping callbacks to
implement draining, and do not assume this protects dependencies from unrelated
hosted services that dispose them early. The explicit application owner still
retains IHost on Incomplete, catches hosted-stop errors and reobserves before disposal.

**Explicit Generic Host owner:** StartAsync on an explicitly retained IHost; quiesce
submitters and await the application's own pending starts/accepted handles first,
then call this lifetime's StopAsync. ApplicationStopping may already have made the
engine Closed, but no code may call IHost.StopAsync or the engine lifetime StopAsync
concurrently with that drain. ApplicationStopping alone does not cancel workflows.
Keep required event delivery alive. If the app's drain budget expires, proceed to
StopAsync for cancellation/join; retain IHost on Incomplete. After Completed finish
IHost.StopAsync and other stops, then dispose. Explicit IHost.StopAsync with the
ordered hosted drainer and catch/reobserve is the other supported pattern.

**Plain DI:** there is no public close-only call or Generic Host signal. The app
first closes its own submission gate and joins its submitters/pending starts, then
awaits its accepted handles with its own bounded drain budget, keeping ingress and
dependencies alive. Engine admission remains Open during that app-controlled drain;
this does not protect against uncontrolled callers, so the app must own every
submitter. Afterwards call the public StopAsync, which closes and enters Stopping
in the same invocation, to cancel/join anything left. On Incomplete retain the actual
provider/scopes and reobserve; only Completed permits disposal. No provider disposal
or token cancellation is a substitute for the explicit join. Serialize all disposal
through the same application owner.

`IHost.Run/RunAsync`, `WaitForShutdown` followed by unconditional disposal, host
process exit, and raw provider Dispose/DisposeAsync cannot preserve dependencies
on an incomplete stop. They are not qualified for noncooperative-work ownership.
Throwing from a hosted service cannot override RunAsync's disposal finally.
The library neither intercepts external provider disposal nor creates a hidden
child provider, prolongs an already disposed singleton, blocks Dispose forever,
or treats retention/managed reachability as dependency liveness. Direct disposal
without successful join is an unsupported abortive teardown: no Completed proof,
no safe-capacity claim, admission becomes Closed and a bounded best-effort diagnostic
is recorded before teardown; it does not enter Stopping or queue shutdown cancellation.
Existing physical ownership is not fabricated as released. Abortive teardown remains
unsupported for draining and may already destroy services/timers; Closed does not
turn raw disposal into the supported drain recipe.

## Count-only stop diagnosis

Incomplete intentionally reports PendingWorkCount, not stuck instance identities.
Before calling StopAsync (while Open or Closed), inspect the existing snapshots and
state and existing stuck-step/lifecycle diagnostics to identify and record suspect
instances. From Stopping, live queries are rejected; use already captured facts and
the existing diagnostics rather than expecting a new instance list or new stop-time
inspection API. Bounded host-shutdown metrics contain no IDs. This adds no telemetry
instrument, logging dependency or guarantee of an operator-accessible live query.
