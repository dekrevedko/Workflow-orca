# Durable resource grant rollback

Can a worker that loses its workflow append release the ticket of the worker that won? DR-038 and DR-AC-036 require a pending or held lexical obligation to reserve its exact tickets through reconciliation. In the selected durable host, workflow commits and the governance partition have separate versions. See the [handoff contract](../../../openspec/specs/durable-runtime/spec.md#scenario-workflowgovernance-handoff-crashes).

The model starts after two independent workers have loaded the same pending obligation and workflow version. Both reach the reservation barrier. The first reserves one unit, and the second observes the same ticket. One workflow append wins. Its activation, the losing append and rollback, the rival's queue request, and legitimate scope exit can interleave. Worker and rival tickets have distinct identities. The model covers one occurrence, one pool of capacity one and one rival. It checks safety. It does not model process death, cancellation, orphan reclamation or retry liveness.

* [The selected governance aggregate](../../../src/OrcaCore.Durable.Hosting/ResourceLeases/SerializedResourceGovernanceAggregate.cs) reuses an existing exact holder grant and admits a queued rival on release.
* [The driver](../../../src/OrcaCore.Engine.Durable/Driver/DurableFiberDriverExecutor.cs) persists the pending obligation, acquires using its expected workflow version, then commits activation.
* [The commit pipeline](../../../src/OrcaCore.Engine.Durable/Execution/DurableCommitPipeline.cs) invokes [acquire compensation](../../../src/OrcaCore.Engine.Durable/Execution/DurableResourcePoolCommitEffects.cs) when the append fails. Compensation releases by instance and holder key.

Run `./scripts/verification/Invoke-TlaModelCheck.ps1 -ModelDirectory verification/tla/durable-resource-grant -ModuleName ResourceGrant` from the repository root. On the merged implementation, normal profile `SourceSafety` fails `ActiveGrantRetained`; a nonzero runner exit preserves that finding. `SourceTransfer` produces a witness with A active, B rolled back and the rival granted. `Reference` completes all 51 distinct states. Its witnesses exercise both retention after conflict and transfer on legitimate exit. In the reference, a losing append preserves a reservation shared with the committed winner. The model does not decide how to reclaim an orphaned reservation.

## SQL provider reproduction

The [shared certification test](../../../tests/OrcaCore.ProviderCertification/DurableResourceGrantRaceCertification.cs) drives the same compiled lexical lease on two independent runtimes connected to the same workflow store and governance partition. It pauses both after reservation and checks that the obligation, protection token, expected workflow version, and ticket facts agree. A rival queues for the only slot. A resumes and pauses again after its activation commit. B then loses its workflow append. The test stops B after that conflict and reads the stored workflow history, checkpoint, and pool snapshot before allowing A to exit.

On revision `74cd0d054df37d623bdaa6b6901a1701daf827b2`, the Release solution built with zero warnings and errors. Both providers ran both theory cases locally against real containers.

| Provider | B loses its workflow append | Control without B |
|---|---|---|
| PostgreSQL 17 | Ownership assertion failed. A was still held while the rival owned a new generation-2 ticket. | Passed. A retained generation 1 until its matching release and completed normally. |
| SQL Server 2022 | Ownership assertion failed. A was still held while the rival owned a new generation-2 ticket. | Passed. A retained generation 1 until its matching release and completed normally. |

In both failing rows, the workflow recorded one acquisition, zero releases and the original ticket in its held checkpoint. The provider had removed the rival from the queue and granted it the only slot. The tests observe admission while A is paused before its protected body. They make no claim about simultaneous external effects.

Run the provider probes after building Release:

```powershell
dotnet test tests/OrcaCore.Providers.PostgreSql.Tests -c Release --no-build --filter FullyQualifiedName~ResourceGrant_ActiveWinnerRetainsTicket
dotnet test tests/OrcaCore.Providers.SqlServer.Tests -c Release --no-build --filter FullyQualifiedName~ResourceGrant_ActiveWinnerRetainsTicket
```

The competing-append assertions express the required ownership behavior and remain failing regression tests on this revision. A repair must preserve a committed winner's grant during rollback and retain a recovery path for a genuinely abandoned reservation.
