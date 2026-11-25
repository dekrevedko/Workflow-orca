# Quarantined lease confirmation race

Two callers present distinct confirmation IDs, A and B, for the same quarantined token. The pool has one unit held by the original owner and one eligible FIFO waiter. A governance append releases that unit, grants the waiter, retains a release receipt for the token and binds the winning ID. Publishing the workflow's released tombstone requires a separate version-checked workflow append. Either caller can win governance.

At source revision `83b2a2f0`, a caller that initially reads an empty receipt and an open workflow can receive `AlreadyConfirmed` from governance and still publish its own ID. TLC found this interleaving in `SourceCrashAgreement`:

1. B reads no release evidence and the quarantined workflow at version zero. A then makes the same reads.
2. A commits governance. The receipt and binding both record A, and the waiter holds the released unit. A dies before the workflow append.
3. B resumes at the provider call and receives `AlreadyConfirmed`. The workflow still has version zero, so B's workflow append succeeds with `AcceptedConfirmationId = B`. Its public result is also `AlreadyConfirmed`.

The counterexample has nine states including initialization. A retry with A can subsequently return `AlreadyConfirmed` while leaving B's tombstone intact. In the fresh schedule, B starts after A dies, reads the retained receipt and publishes A. Capacity and ID agreement are checked separately.

## Source mapping and bounds

| Model actions | Implementation and assumptions |
| --- | --- |
| `ReadEvidence`, `ReadWorkflow` | `ConfirmProtectedWorkStoppedAsync` reads governance evidence and workflow lease records separately. Its cross-token conflict checks cannot match within this single-token scope. [DurableResourceLeaseRecovery.cs](../../../src/OrcaCore.Engine.Durable/Driver/DurableResourceLeaseRecovery.cs) |
| `WinGovernance`, `AlreadyConfirmed` | Hosted `ConfirmAndReleaseAsync` checks prior binding/evidence before attempting the governance append. The successful transaction retains a binding and receipt, removes the holder and grants the eligible waiter. The model starts with the waiter admitted and folds internal retries into the serialized outcome. [SerializedResourceGovernanceAggregate.cs](../../../src/OrcaCore.Durable.Hosting/ResourceLeases/SerializedResourceGovernanceAggregate.cs) |
| `Append`, `RestartA` | The workflow append uses the version captured by the caller. A conflict restarts the outer reads. A retry of a killed call preserves the request ID and obtains fresh local state. In source mode, both successful provider statuses lead to publication of the request ID. In reference mode, `ReadWinner` loads the retained receipt after a provider duplicate. [DurableResourceLeaseRecovery.cs](../../../src/OrcaCore.Engine.Durable/Driver/DurableResourceLeaseRecovery.cs) |
| `RepairRead`, `RepairAppend` | `ReconcileReleaseGapsAsync` selects nonreleased obligations and publishes retained evidence with a workflow version check. The model has one possible open version and one closing append. A failed repair CAS therefore means another writer already closed that workflow. [DurableResourceLeaseRecovery.cs](../../../src/OrcaCore.Engine.Durable/Driver/DurableResourceLeaseRecovery.cs) |

The model uses distinct variables for the token's receipt, set of bound IDs, workflow ID, release and grant counts, current owners' unit counts and queue membership. Reads, crash, retry and recovery are optional transitions. Schedule markers observe caller ordering without constraining either winner. `GapCloses` assumes a continuously enabled repair action eventually runs. Caller scheduling has no fairness assumption. The finite scope excludes purge, reuse across tokens, normal lexical release, waiter cancellation, unrelated stream writes and transaction implementation. The modeled `KillA` discards local state. Evidence for killed operating-system processes and SQL durability comes from separately executed integration tests.

The trusted recovery seam's result and retention requirements are documented in [management operations](../../../docs/specs/09-requirements-management-operations.md) and the [capability matrix](../../../docs/specs/17-selected-mode-capability-matrix.md).

## TLC profiles

| Profile | Expected checker outcome and meaning |
| --- | --- |
| `Reference` | **Pass.** All safety properties hold, including tombstone/receipt/binding agreement. Every committed receipt eventually has a matching workflow tombstone under fair repair. |
| `SourceAccounting` | **Pass.** Capacity is conserved, the grant and release happen at most once, binding agrees with receipt, and responses meet the configured status checks. Tombstone identity is examined by the next profile. |
| `SourceCrashAgreement` | **Expected source counterexample.** `CrashedStaleAgreement` fails for the nine-state sequence above. The runner classifies this trace as a witness. It demonstrates a defect in the baseline's modeled publication rule. |
| `SourcePersistentSplit` | A reachability witness shows that same-ID retry A can return `AlreadyConfirmed` after the split. |
| `ReferenceStale`, `SourceFresh` | Reachability witnesses exercise stale B with a winner reread and fresh B with retained evidence. Each publishes A and returns `AlreadyConfirmed`. |
| `SourceRetry`, `SourceRecovery` | Reachability witnesses show that either A's same-ID retry or the independent recovery scan can publish A after its crash. |
| `ReferenceCasRetry`, `SourceBWinner` | Reachability witnesses exercise a real workflow version conflict and retry, and B winning the initial serialized release. |
| `DoubleGrant`, `DropUnit` | Injected duplicate grant and lost capacity violate `ExactlyOneTransfer` and `CapacityConserved`, respectively. |
| `DisabledRecovery` | Disabling the recovery worker allows a temporal counterexample to `GapCloses` when callers stop taking steps. |

Witness profiles check the negation of the requested observation, so a named invariant violation confirms reachability. The reference and source accounting profiles explored 1,163 and 978 distinct states respectively with empty queues. The other eleven profiles produced their designated counterexamples.

Run all profiles from the repository root:

```powershell
./scripts/verification/Invoke-TlaModelCheck.ps1 -ModelDirectory verification/tla/lease-confirmation-race -ModuleName LeaseConfirmationRace -CheckLivenessAtEnd
```

The runner records each outcome, source hashes, checker arguments and TLC traces under the reported evidence directory.
