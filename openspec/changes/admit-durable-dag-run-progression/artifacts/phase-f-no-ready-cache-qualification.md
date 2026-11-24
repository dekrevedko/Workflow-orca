# Phase F DAG no-ready cache qualification

The implementation is commit `79586c9c53eddcd1ae1c2d73d86ba1b5c92caaeb`, based on `85879e23`. A scheduler that has successfully read a running DAG with no ready node now skips another checkpoint read at that same version, even with spare capacity. Every discovery pass still consults the provider’s recovery index. A successful remote join that makes a dependent ready advances the version.

## Lost-wake regression

The same test runs on the in-memory provider, PostgreSQL 17 and experimental SQL Server 2022. A seed scheduler admits the root. A fresh scheduler with capacity two observes that run with one authorized child and one pending dependent. A later-created marker passes through the same dispatch queue and makes each discovery pass observable. The target has one checkpoint read after the warm pass and still has one after the unchanged pass.

A separate workflow-only host executes the root and commits its join. The test claims the real ready notice for that run and join version, then acknowledges it without delivery before releasing the next discovery pass. The scheduler rereads the changed run and commits one dependent admission with input `42` derived from the remote output `41`. The test isolates the scheduler’s outbox **claims** to reproduce the lost wake. DAG discovery, checkpoints, appends, the worker’s outbox operations and the explicit acknowledgement use the selected storage provider. [Shared certification case](../../../../tests/OrcaCore.ProviderCertification/DagDiscoveryCacheCertification.cs)

| Check | Result |
| --- | --- |
| Original coordinator on all three providers | Expected one read after the unchanged pass, observed two |
| Cache enabled on all three providers | Changed version discovered and dependent admitted |
| Disposable build ignoring version changes, in-memory | Failed the changed-version read assertion before the later marker |
| PostgreSQL and SQL Server, focused real-database runs | Seven tests passed on each provider |
| Release solution build and exact local feed | Zero warnings and errors. Twelve packages packed |
| Core, Hosting, in-memory provider certification | 408, 126 and 135 tests passed, respectively |
| Infrastructure on the implementation and again after integrating master | 323 of 323 guards passed in each run |

The Hosting total counts the 126 tests executed by the ordinary lane. The runner also listed five explicit process-test entries. Each database selection separately exercised another run progressing past a blocked mapper, process restart, FIFO lease turns, competing store instances, facade binding and level-triggered faults. The original coordinator and the version-blind variation are negative controls. Independent review rebuilt the candidate, ran the new case on all three providers, reproduced the two negative controls on the in-memory provider and verified the refreshed package source digest and test inventory.

The production delta affects the idle visit in `DagCoordinator`. Local work completion still invalidates the cached decision, and hosts check the installed definition before skipping a discovery row. Worker retirement, governance storage and the broader DAG certification task remain separately owned. [Coordinator](../../../../src/OrcaCore.Dag.Hosting/DagCoordinator.cs)

The integration merge at `d76fb3c2` includes master `80a6a922`. All eleven files in the implementation delta match their reviewed blobs at `79586c9c`. The lost-wake regression passed again on both real databases after integration. Strict OpenSpec validation passed all 34 items.

## Reproduce on real storage

With the repository SDK and Docker Linux containers available, run these from the repository root.

```powershell
dotnet test tests/OrcaCore.Providers.PostgreSql.Tests -c Release --filter 'FullyQualifiedName~CachedNoReadyRunRecoversRemoteJoinAfterLostWake'
dotnet test tests/OrcaCore.Providers.SqlServer.Tests -c Release --filter 'FullyQualifiedName~CachedNoReadyRunRecoversRemoteJoinAfterLostWake'
```
