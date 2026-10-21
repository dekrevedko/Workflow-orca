## ADDED Requirements

### Requirement: Ephemeral host shutdown has one supported observation contract
`OrcaCore.Engine.Ephemeral` SHALL own exactly the following new public declarations
in namespace `OrcaCore.Hosting`, with the shown construction restrictions and no
additional members, record-generated surface, public constructor or extension:

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

The inherited stable codes SHALL be `WF-EPHEMERAL-HOST-STOPPING` and
`WF-EPHEMERAL-SHUTDOWN-INCOMPLETE` respectively. The existing ephemeral role entry
SHALL register one reserved owned singleton lifetime per root provider, the same
when resolved from a child scope. Equal existing role/options registration SHALL
remain idempotent; a pre-existing application descriptor for the reserved lifetime
before first role registration SHALL fail with ArgumentException, parameter services,
before collection mutation. There SHALL be no new role entry, option, shutdown mode,
fingerprint value, hosted-service toggle, dependency, serializer hook or friend.

StopAsync SHALL validate timeout first: exactly InfiniteTimeSpan or an inclusive
zero through 4,294,967,294 milliseconds, compared in exact ticks; other values SHALL
throw ArgumentOutOfRangeException for timeout before any effect. An already-cancelled
token SHALL then throw OperationCanceledException with that token without initiating
shutdown; root disposal SHALL next throw ObjectDisposedException. If Completed
already holds after those checks, the call SHALL return its detached cached result
without reading TimeProvider or creating observation resources. Otherwise zero SHALL close,
enter Stopping and observe immediately; infinite SHALL explicitly permit an unbounded observation.
Finite observation time SHALL begin on admission using the registered monotonic
TimeProvider and include queued cancellation/join work. Once admitted, observer
cancellation SHALL be Incomplete(ObservationCancelled), not an exception. Decision
precedence SHALL be accounting fault, stable completion, observer cancellation,
then elapsed budget. The two latter incomplete causes SHALL NOT abort the shared stop.

Host state SHALL progress irreversibly Open -> Closed -> Stopping -> Completed.
Closed SHALL refuse new/replayed starts and compatible registrations but SHALL NOT
queue shutdown cancellation, seal accepted-instance producers or reject otherwise
valid accepted-instance operations. Only an admitted public StopAsync or the engine's
own hosted StopAsync SHALL enter Stopping, closing first if needed, to cancel and
join remaining work. The internal hosted stop SHALL do so even with an already-
cancelled host token, return normally only on Completed and otherwise throw the
typed incomplete exception with its detached result. The host token SHALL bound
observation only; this path SHALL NOT use the public pre-cancelled no-effect branch.

If IHostApplicationLifetime is present, every admission/initialization/attempt gate
SHALL read ApplicationStopping directly and establish Closed from its cancelled
state, even if another handler blocks engine notification. Accepted-instance gates
SHALL still permit ordinary operation and attempts in Closed, checking Stopping
separately. The engine/coordinator SHALL initialize before subscribing. Its bounded
callback SHALL only close, without queuing cancellation/join, invoking application
code or waiting on a lane. Late resolution SHALL close new-start/registration
admission without itself cancelling accepted work. Ordinary output-wait cancellation
SHALL NOT initiate host shutdown. No new close-only API, option or drain result
SHALL be added.

#### Scenario: A valid public stop token was cancelled before invocation
- **WHEN** the public method receives a valid timeout and an already-cancelled token
- **THEN** it throws caller cancellation without closing an open host or scheduling cancellation work

#### Scenario: A host budget is already exhausted on hosted stop entry
- **WHEN** the internal hosted service receives an already-cancelled observation token
- **THEN** the hook closes admission and enters Stopping to cancel/join remaining work, returning Completed only if stable settlement is proved and otherwise throwing the detached typed incomplete result

#### Scenario: Exact API appears only with implementing source
- **WHEN** the contract/canonical gates are prepared before source implementation
- **THEN** these proposed declarations have no compiled/public-baseline credit; source task 3.3 adds their API, compile and package baselines together with source

#### Scenario: Another application stopping callback blocks notification
- **WHEN** the host stopping token is cancelled but another registered callback blocks before the engine callback runs
- **THEN** gates establish Closed from the token and reject new starts/registrations without waiting for notification, while accepted-instance operations/attempts continue and no shutdown cancellation is queued until a stop entry runs

### Requirement: Ephemeral host shutdown preserves dependency ownership
One application-owned session/supervisor SHALL serialize provider/host disposal and
retain the root, relevant scopes and external step dependencies until Completed.
After Completed, the owner SHALL finish other hosted-service stopping before disposal;
no engine-owned step, callback, turn, serializer/notification or continuation SHALL
then access those dependencies. Incomplete SHALL NOT authorize disposal or capacity
reuse. A later finite/infinite observation SHALL be supported while ownership is kept.

Supported drain guidance and consumer qualification SHALL cover these sequences:

- For Generic Host hosted draining, register the engine before an application
  IHostedService drainer and use HostOptions.ServicesStopConcurrently=false. Reverse
  registration stop order SHALL let that drainer await its own pending starts and
  accepted handles while Closed, retaining event producers/dependencies needed by
  those instances, before the engine's hosted StopAsync enters Stopping. Drain waits
  SHALL use the supplied StopAsync budget token, not the already-cancelled
  ApplicationStopping token. Local drain expiry SHALL return control to the engine
  without disposing dependencies or abandoning runtime ownership. Draining and the
  engine stop SHALL share HostOptions.ShutdownTimeout; a fresh engine budget SHALL
  NOT be promised. With concurrent hosted stops, ordering SHALL NOT be described as
  guaranteeing a drain window. Required dependency/event-service stops SHALL occur
  after drain. Blocking ApplicationStopping callbacks SHALL NOT implement drain.
- An explicit Generic Host owner SHALL be able to start and retain IHost, stop its
  submitters, await its pending starts/accepted handles first, then call the engine
  StopAsync, without another engine/IHost stop racing that drain. ApplicationStopping
  alone MAY already make the engine Closed without cancellation. A bounded drain
  expiry SHALL proceed to StopAsync. Incomplete SHALL retain the host for reobservation;
  only Completed SHALL permit finishing IHost.StopAsync and disposal. An explicit
  IHost.StopAsync with the ordered drainer and catch/reobserve SHALL also be supported.
- Plain-DI owners SHALL close their own submission gate and join their submitters/
  pending starts, await accepted handles with an application drain budget, then call
  the lifetime StopAsync to close/cancel/join anything left. The engine SHALL remain
  Open during that app-controlled drain: there is no plain-DI close-only API and
  the application SHALL control all submitters. It SHALL keep event delivery and
  actual dependencies alive, retain provider/scopes on Incomplete and reobserve
  before disposal. Provider Dispose/DisposeAsync SHALL NOT be presented as stop/join.

Incomplete-capable Generic Host applications SHALL own IHost explicitly and retain it
on incomplete hosted stop. Migration documentation SHALL state that the formerly
no-op hosted stop now surfaces Incomplete as an exception escaping Run/RunAsync
(possibly aggregated). Its unconditional disposal finally SHALL still be identified
as unsafe for incomplete-work ownership; throwing SHALL NOT imply it prevented disposal.

The engine SHALL NOT claim to intercept arbitrary external provider disposal, retain
already disposed services, create a hidden root provider, or block synchronous disposal
forever. Unjoined direct disposal SHALL be classified as unsupported abortive teardown:
enter only Closed and record a contained best-effort bounded diagnostic before
teardown, without queuing shutdown cancellation/join, claiming Completed or falsifying
physical ownership. Abortive disposal SHALL NOT be a supported drain mechanism. Applications SHALL serialize
concurrent disposal through the supported owner; arbitrary premature external disposal
is outside the successful-join guarantee.

#### Scenario: The owner disposes after successful physical join
- **WHEN** the supported owner observes Completed, finishes other hosted stops and disposes its root and scopes
- **THEN** disposable dependency sentinels observe no later engine-owned access

#### Scenario: Work ignores cancellation beyond the observation budget
- **WHEN** StopAsync returns Incomplete while physical work or an entered callback still uses a dependency
- **THEN** the owner keeps the actual provider/scopes/dependencies alive, physical capacity remains owned, and another observation can join later actual settlement

#### Scenario: Generic Host RunAsync reaches its disposal finally
- **WHEN** a caller uses ordinary RunAsync and an incomplete hosted stop throws
- **THEN** the new exception (possibly aggregated) and automatic disposal are documented as changed behavior and an unsupported incomplete-work ownership pattern; the exception does not prevent disposal

#### Scenario: An ordered Generic Host service drains before engine stop
- **WHEN** a drainer registered after the engine awaits its handles with sequential hosted stops
- **THEN** ApplicationStopping closes only new admission, accepted work can finish naturally and receive events/new waits, and cancellation begins only at the subsequent engine StopAsync within the same host budget

#### Scenario: Concurrent hosted stops race a drainer
- **WHEN** ServicesStopConcurrently permits the engine stop to enter before the drainer finishes
- **THEN** the documented recipe makes no drain-window guarantee, Stopping may cancel remaining work, and Incomplete still requires explicit retained host ownership

#### Scenario: A plain-DI application drains its own submissions
- **WHEN** the owner closes its application submission gate, joins pending starts and awaits accepted handles before calling StopAsync
- **THEN** the engine stays Open during that controlled drain, event delivery/dependencies stay alive, and the later explicit call closes and cancels/joins any remaining work before disposal
