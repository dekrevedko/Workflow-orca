# Prototype evidence for the DAG public handle seam (2026-11-15)

A throwaway prototype of the proposed facade was built against the real durable bridge, scheduler, pumps and InMemory provider. It is evidence for the contract, not product source, and it is not part of this change's target.

- Worktree `X:/ocv303`, branch `prototype/dag-public-facade`, base `da1a4729ea2cdb9474eb219eb507760574f83dcb`, uncommitted.
- Evidence class: in-process, in-memory. No provider certification, no real process restart, no boundary guard run.

## Prototype files (SHA-256 at the time of the runs)

| File | SHA-256 |
|---|---|
| `src/OrcaCore.Dag/DagFacade.cs` (new) | `5b738bfb2178303e5e03fb4aa68fe23d00b264f129f24a868408d39a77e33af0` |
| `src/OrcaCore.Durable.Hosting/Dag/DurableDagFacade.cs` (new) | `75bcfff9b9c681eb7e20ab5b8e1129d49de73237bbd6ad63142b2e7dc92c5113` |
| `src/OrcaCore.Dag.Hosting/DagFacadeRegistry.cs` (new) | `1ca25707a4ba3afec68d482e7a1b1139cf0f1daae78a14c6b157beaa49048479` |
| `src/OrcaCore.Engine.Durable/Dag/DurableDagValues.cs` (modified) | `c62d492f696cd840dab5cc03408a60638d1c010a1ba44bcdaf53e7a53f8887ad` |
| `src/OrcaCore.Durable.Hosting/Dag/DurableDagRunInputCatalog.cs` (modified) | `39ed850f4fef68c6827ebe9838b61351a9b34078f81a31885f9d0a71c776fb03` |
| `src/OrcaCore.Dag.Hosting/DagCoordinator.cs` (modified) | `13fc1787074b68b1667f20203efd63cf1b7658f612c09d3f4128c3040b99e036` |
| `tests/OrcaCore.Hosting.Tests/PrototypeDagFacadeTests.cs` (new) | `29a3fbfb11e9377152c662ac904e9bb6cd67673576383b7fd2e9d592b7563ec6` |
| `tests/OrcaCore.Engine.Durable.Tests/PrototypeCodecRoundTripProbe.cs` (new) | `7c017db0c168b2f300bc5f8b1c97c4447c4552c3a5e68afddeb3e1676c366cfe` |

Two further files carry only DI wiring and one exposed property.

## Results

### Codec round-trip probe (`PrototypeCodecRoundTripProbe`, parent normalization unchanged for this probe's reading)

| Declared type shape | Stored | Re-encoded after decode | Verdict |
|---|---|---|---|
| positional record, init-only, public setter, get-only with matching constructor, private setter with matching constructor, computed member | `{"Value":17}` (computed adds `"Doubled":34`) | identical | stable |
| private setter, parameterless construction, no matching constructor | `{"Value":17}` | `{"Value":0}` | **lossy, accepted by the parent** |
| constructor parameter that binds to no member | — | — | rejected by the codec |

### Facade tests (`PrototypeDagFacadeTests`, 5 of 5 passed; independently rerun by the reviewer)

| Test | What it established |
|---|---|
| `CreateReplayReopen_AreConsistentUnderOneBinding` | Register is idempotent; start is `Accepted`; replay returns the same run id with `WasExisting`; changed input under one key is `Conflict`; reopen, terminal wait, mapped-input fingerprint, typed output read and not-found all work through the public facade with no reflection into the coordinator |
| `LossyRunInput_IsRejectedBeforeAnyRunExists` | With a decode and byte-identical re-encode check at submission, the lossy type fails with `NotSupportedException`, and no start key is bound |
| `StoredInputThatNoLongerRoundTrips_IsSkippedNotFailed_AndACompatibleHostFinishesIt` | On a second host over the same storage whose type reads the stored bytes differently under an equal key: reopen throws `DagRunInputContractMismatchException` with the run id; the role-independent child pump still joins the already-admitted child; the unmapped node stays `Ready` and the run `Running` across more than two discovery passes; a third, compatible host reopens and completes the run |
| `Waiter_HeldAcrossFaultAndReconciliation_ContinuesWithoutException` | A registered waiter does not throw when a fault and its reconciliation both commit before its recheck, and later returns `Succeeded` |
| `FaultedRun_ThrowsFromWaitAndCancel_KeepsPreFaultStatus_ThenRecovers` | A fault committed after registration wakes the waiter with `DagRunFaultedException`; an already-faulted run fails the first recheck; snapshot stays `Running`; cancel throws and commits nothing; after a committed cancellation and a second fault the snapshot is `CancellationRequested` and cancel returns `AlreadyRequested`; after reconciliation the run ends `Cancelled` |

### Controls

- **Recovery skip disabled:** the same redeploy test fails because the incompatible host maps the pending node with the misread value and the run ends `Succeeded`. Without the stored-input check the outcome is silent wrong input, not a mapping failure.
- **Regression:** with the stricter normalization, the existing hosting suite (58 including the five prototype tests) and the durable engine test project passed.

## Known deviations of the prototype from the RV-3 contract

- The prototype's stored-input check catches the integrity exception from corrupt storage and treats it as a mismatch. RV-3 keeps the existing integrity classification for corrupt storage.
- The fault-and-reconciliation race is arranged with timing delays. RV-3 requires an explicit barrier in the final regression test.
- The prototype applies the stricter normalization to every DAG value. RV-3 scopes the rule to the DAG run input.
- `DagRunDefinitionMismatchException` keeps its existing members (D2 not implemented).

## Not exercised

Registration `Conflict` and `RunInputConflict`; start `RunInputConflict`; a competing create between two hosts; PostgreSQL and SQL Server; status derivation across a real restart; the boundary guard with a seventeen-member allowlist.
