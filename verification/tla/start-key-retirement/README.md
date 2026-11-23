# Ephemeral start-key retirement

A caller that successfully pins the old binding gets its decision from that binding. A compatible request returns the original instance with `WasExisting = true`. An incompatible request returns the typed conflict with the original and attempted fingerprints. While the pin is held, a sweep skips that instance.

Releasing the last pin can retire an expired instance before the caller receives its result. Another caller can then create the replacement and receive `WasExisting = false`. The first caller still receives its captured decision. A later snapshot through its old handle reports `WorkflowInstanceNotFoundException` with the old ID. A caller still computing its input fingerprint at retirement makes its decision against whatever is bound when it reaches the gate. If the key is free it can reserve the start itself. If another start is in flight it waits and checks again. [Canonical requirement](../../../openspec/specs/management-and-querying/spec.md#requirement-ephemeral-start-idempotency-expires-with-atomic-retirement) and [start facade](../../../src/OrcaCore.Engine.Ephemeral/Facade/EphemeralWorkflowFacade.cs)

## Model and implementation

| Boundary | What the model checks against the runtime |
|---|---|
| Inspect and pin | One gate protects lookup, lifetime pin acquisition and fingerprint comparison. The model saves the inspected generation. A conflict exposes the existing and attempted binding fields. Only an accepted handle exposes an instance ID. |
| Release and enforce | Dropping a pin and checking eligibility are separate. Another caller can pin in between. The age sweep and release enforcement both require a settled, unpinned old instance. The operation that selects retirement runs its cleanup before returning. |
| Select and clean up | Selection seals the old lifetime under the gate. Physical removal runs outside the gate. A competing start can encounter the old dictionary entry, refuse the pin, remove the stale mapping and publish a replacement. Delayed cleanup compares instance identity before removing a key. |
| Return and use a handle | A replay returns the decision saved at inspection, even if the binding changed before return. Ordinary handle access resolves the captured instance ID. |

The finite model starts with one settled old instance, one key and two single-attempt callers. Each caller can supply either fingerprint class. It explores one age transition and one sweep. It retains the replacement through the rest of the run. Reservation, publish and reinspection follow the [existing start model](../start-idempotency/README.md). This model combines successful replacement publication and reservation release. The existing model covers abort, cancellation and the interval between publication and release. Monotonic age is abstracted to an eligibility flag. Count-based eviction, repeated generations, scheduling progress and external effects are outside this bound. [Model](StartKeyRetirement.tla), [instance lifetime](../../../src/OrcaCore.Engine.Ephemeral/Execution/EphemeralInstanceLifetime.cs) and [retention coordinator](../../../src/OrcaCore.Engine.Ephemeral/Execution/EphemeralRetentionCoordinator.cs)

TLC exhausted **1,472 distinct states**. It checked protection while pinned, binding visibility, selection on an eligible enforcement, a single accepted replacement, result identity and old-handle access. Four fault controls expose retirement through a pin, re-reading the key on return, erasing the replacement during old cleanup, and following the key from an old handle. Five witness traces reach the old handle returned after reuse, the original conflict returned after reuse, cleanup after publication, and replay or conflict against the replacement.

## Runtime evidence

| Deterministic schedule | Observed result |
|---|---|
| Hold an admitted snapshot pin and run the real age sweep | The sweep retires an unpinned peer. Compatible replay still returns the pinned instance and changed input still conflicts. Releasing the query pin retires the old instance and permits a fresh start. |
| Start a real replay after age expiry, between sweep ticks | Final replay-pin release selects retirement. A barrier pauses physical cleanup before the old facade entry is removed. A competitor publishes the new instance while the replay call is still open. Both the accepted and conflict cases return the original decision. After cleanup, the new binding remains usable and the workflow body has run exactly twice across both generations. |
| Pause a call during fingerprinting and sweep before it reaches the key | A competitor creates the replacement. The delayed caller returns that instance for matching input or returns the replacement's fingerprint in a conflict. The body runs once per generation. |

The first schedule holds the lifetime pin through the query observation seam. The second exercises the actual short-lived start replay pin. TLC also explores a sweep inside that replay-pin interval. Existing tests exercise count-pressure replacement with competing starts, ordinary stale-handle operations, and monotonic age without refresh on replay. All 35 selected C# cases passed in Release, including the five new cases. [New test schedules](../../../tests/OrcaCore.Engine.Ephemeral.Tests/Retention/RetentionInterleavingTests.StartKey.cs), [competing replacement starts](../../../tests/OrcaCore.Engine.Ephemeral.Tests/Retention/TerminalRetentionTests.cs) and [retention interleavings](../../../tests/OrcaCore.Engine.Ephemeral.Tests/Retention/RetentionInterleavingTests.cs)

Run from the repository root with the private JRE.

```powershell
./scripts/verification/Invoke-TlaModelCheck.ps1 -ModelDirectory verification/tla/start-key-retirement -ModuleName StartKeyRetirement
dotnet test tests/OrcaCore.Engine.Ephemeral.Tests/OrcaCore.Engine.Ephemeral.Tests.csproj -c Release --filter "FullyQualifiedName~RetentionInterleavingTests|FullyQualifiedName~TerminalRetentionTests|FullyQualifiedName~RetentionSweepTests|FullyQualifiedName~ConcurrentStartTests|FullyQualifiedName~StartKeyConflictAndHostModeMismatchUseClosedResults"
```
