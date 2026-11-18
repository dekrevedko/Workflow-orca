# Ephemeral start idempotency

The open-host fragment checks independence of different start keys, rechecking admission after reservation release, and compatible replay during retention. See the [management requirements](../../../docs/specs/09-requirements-management-operations.md) and [canonical retained-key requirement](../../../openspec/specs/management-and-querying/spec.md#requirement-ephemeral-start-idempotency-expires-with-atomic-retirement).

Four callers each make one start attempt against an open host. Callers 1 and 2 have matching requests. Caller 3 uses their key with a different fingerprint. Caller 4 uses another key. Each caller's number also stands for its prospective instance ID. The model covers the lifetime of a retained start binding. Retirement, restart, host shutdown, fingerprint computation and provisional cleanup internals are outside this bound.

| Model action | Corresponding step in the ephemeral facade |
|---|---|
| Inspect | Under the facade gate, compare an existing binding, observe the current reservation's task, or create a reservation. The fingerprint stands for equality of definition, version, definition fingerprint and input fingerprint together. |
| Publish | After provisional execution, accept the instance and insert its bindings under the gate. Callers arriving before reservation release can already see the binding. |
| Abort / Release | On failure, finish provisional cleanup, then remove the reservation and signal its task. Wake can run in any subsequent turn. |
| Wake / Cancel | A waiting caller observes completion of the specific reservation it saw, then competes again under the gate. Its own cancellation can end the request. |

The implementation is in [EphemeralWorkflowFacade.cs](../../../src/OrcaCore.Engine.Ephemeral/Facade/EphemeralWorkflowFacade.cs), method `StartCoreAsync`. The abstraction combines removal and signaling into one release action. It allows arbitrary delay before a waiter rechecks the key. It assumes cleanup and publication can finish, and retains each published binding throughout the run.

`Safety` checks that each provisional owner holds its key, each published instance keeps its binding, and replay returns the matching instance or reports a fingerprint conflict. `Settlement` additionally checks that all calls finish under weak fairness of continuously enabled internal steps per caller. External cancellation is optional. Injecting a missing reservation or ignoring the fingerprint must produce the respective ownership or identity failure.

Three witnesses exercise completion on a different key while a start is still running, takeover with a different fingerprint after the owner aborts, and replay after waiting. A control serializes reservation acquisition across keys and checks that the unrelated-completion witness disappears.

The source schedules include same-key replay, independent completion, a pending caller succeeding after its owner cancels, cancellation during fingerprinting, and a closed conflict result. The abort test checks the replacement and replay outcomes. Only the model observes the internal reservation at each transition. See [ConcurrentStartTests.cs](../../../tests/OrcaCore.Engine.Ephemeral.Tests/Facade/ConcurrentStartTests.cs) and [EphemeralWorkflowFacadeTests.cs](../../../tests/OrcaCore.Engine.Ephemeral.Tests/Facade/EphemeralWorkflowFacadeTests.cs).

```powershell
./scripts/verification/Invoke-TlaModelCheck.ps1 -ModelDirectory verification/tla/start-idempotency -ModuleName StartIdempotency
dotnet test tests/OrcaCore.Engine.Ephemeral.Tests/OrcaCore.Engine.Ephemeral.Tests.csproj -c Release --filter "FullyQualifiedName~ConcurrentStartTests|FullyQualifiedName~StartKeyConflictAndHostModeMismatchUseClosedResults"
```

The reviewed eight-profile run completed both normal searches at 7,610 distinct states each with zero queued. Both mutations and all three witnesses produced their expected counterexamples. The serializing control passed, and all eight selected C# cases passed locally on Windows x64.
