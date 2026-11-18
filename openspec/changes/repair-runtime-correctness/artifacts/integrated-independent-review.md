# Integrated Phase A independent review

Verdict: ACCEPT

Scope: Frozen Phase A source/provider integration, normative synchronization, declaration/API guard updates, and integration of merged DAG PR #27. This is source/provider acceptance for the exact target below; it does not itself close Phase A task 2.2.

## Exact reviewed target

- First parent: `2bb31ee317f71bfc60bbbb06780c2bcdcb6a0264`.
- Incoming merged DAG parent: `a822b215fe1dc1d0fea4f498ab0ea0a29d44203d`.
- Integrated tree: `b56068a8cd3bdb1306b3c118bb42bb2978e7390d`.
- Disposable rehearsal commit containing both parents: `8907d2cd9ae6463d0a873b46e6b1eecac4407ca0`.
- Manifest: `integrated-review-target.json`, SHA-256 `b882b0c1ca8ba9b41a174702bc1a7714c4028b0c33f32574c6f6fb343feca42e`.
- All 177 manifest file hashes matched the rehearsal working files on final verification; the rehearsal HEAD/tree matched and its Git status was clean.

## Assessment

No actionable defect was found in the reviewed integration. The provider additions exercise the intended runtime behavior against real PostgreSQL and experimental SQL Server stores. They do not substitute an in-memory persistence implementation for the race under test.

`tests/OrcaCore.ProviderCertification/RuntimePositionCertification.cs:18` checks both position-only command variants against independently constructed store clients. It checks version advancement, the winning payload, parent/root lineage preservation, rejection of the stale competitor, and a single committed execution-position marker in the tail. The publication regression at line 82 schedules a second runtime before the first runtime's tail read or after that tail has been captured. The wrapper delegates actual checkpoint/tail storage operations to the provider. The explicit interleaving count, single outbox publication, single active timer, and coherent final load prevent a passing test caused by never entering the intended race. These two schedules cover the reload path and the stale expected-version append path.

The shared event-store suite checks the marker's serialized lineage and full checkpoint equality, including replacing non-null parent/root lineage and then clearing it to null. Provider entry points construct separate store objects connected to the same database/schema. This is useful real-storage, competing-runtime certification within one process; it is not a separate-process crash or fault-injection campaign.

The PostgreSQL migration, store implementation, and local event-store test were compared with the actual merged DAG parent and are unchanged from it. The overlapping durable DI integration retains both the incoming DAG facade registration and Phase A's structural profile comparison. No selective copying from a dirty donor tree was used in this target.

The normative changes consistently describe the execution-position marker, coherent checkpoint/tail loading, expected-version fencing, bounded retry, and stable unreconciled-state parking. They explicitly retain matched-binary and fresh/reset-store qualification. The new declaration guard permits only the exact admitted additions and registry pointer; historical DAG API proof is anchored to immutable approved history, while the exhaustive current public API comparison still checks live assemblies. The Protocol baseline adds the intended marker record; the Ephemeral baseline adjustment is emitted nullable metadata rather than a new method contract. The culture regression establishes that the previous culture-dependent ordering differs before asserting the new invariant fingerprint.

The AC-327 long-loop waiver points to the existing portable 10,000-iteration experiment; it does not present smaller unit loop cases as a fresh large-loop benchmark. No throughput, WAL cost, or production capacity conclusion follows from these correctness checks.

## Independently executed checks

All .NET commands used the exact rehearsal tree, Release configuration, and `--no-build`. Every listed test ran; none was skipped. Raw TRX files are retained in the adjacent local `independent-results/` directory.

| Check | Passed | Failed | Skipped | Evidence |
| --- | ---: | ---: | ---: | --- |
| PostgreSQL position fencing, checkpoint/tail interleavings, marker codec, checkpoint lineage | 7 | 0 | 0 | `independent-pg.trx` |
| Experimental SQL Server equivalent provider probes | 6 | 0 | 0 | `independent-sql.trx` |
| Durable coherent-load regressions | 17 | 0 | 0 | `independent-coherent-load.trx` |
| Schema culture ordering and workflow event codec | 49 | 0 | 0 | `independent-schema-codec.trx` |
| Runtime declarations, current API, historical DAG and canonical synchronization guards | 13 | 0 | 0 | `independent-guards.trx` |
| OpenSpec strict validation of all changes/specifications | 29 items | 0 | 0 | `independent-openspec.log` |

Reusable .NET filters:

- PostgreSQL: `FullyQualifiedName~PositionOnlyCommitRejectsASecondHostAtTheOldVersion|FullyQualifiedName~InterleavedCheckpointLoadMustNotCommitPublishTwice|FullyQualifiedName~AppendAsync_AllWorkflowEventTypes_RoundTripsFromTail|FullyQualifiedName~CheckpointUpsert_RoundTripsFullRuntimeState|FullyQualifiedName~Checkpoint_SaveAndLoad_RoundTripsPayloadAndVersion`.
- SQL Server: the same filter excluding `Checkpoint_SaveAndLoad_RoundTripsPayloadAndVersion`.
- Durable: `FullyQualifiedName~DurableCoherentLoadTests`.
- Core: `FullyQualifiedName~SchemaOrderingIsOrdinalAcrossCultures|FullyQualifiedName~WorkflowEventCodecTests`.
- Developer surface: `FullyQualifiedName~RuntimeCorrectnessDeclarationGuards|FullyQualifiedName~EveryTargetAssembly_MatchesTheApprovedExactPublicApiBaseline|FullyQualifiedName~BaselineComparator_RejectsBothAddedAndRemovedSignatures|FullyQualifiedName~BaselineInventoryAndCandidateCapture_FailClosed|FullyQualifiedName~CanonicalSynchronizationGate_EnumeratesCapabilitiesDeltasAndRequirementOwners|FullyQualifiedName~DagStreamIdentitySliceGuards`.
- OpenSpec: `openspec validate --all --strict --no-interactive`.

In addition, the final full-suite TRX counters produced by the integrating agent were read independently: PostgreSQL 127/127, SQL Server 98/98, Core 402/402, and Infrastructure v2 323/323; all reported zero failures and zero skipped tests. Those full suites were not rerun by this reviewer. The integrating agent also reported Durable 130, Ephemeral 430, and Hosting 94 passing, and a fresh zero-warning/error Release build and twelve-package feed. Earlier Infrastructure failures were superseded by the exact-target v2 result.

## Acceptance boundaries

This verdict applies only to the frozen tree and manifest above. Later edits require appropriate revalidation. It does not mark the final Phase A closeout task complete, approve later phases, or activate further DAG features.

PostgreSQL and SQL Server evidence covers matched runtime/provider binaries and fresh/reset test stores. It does not certify rolling mixed-version operation or upgrading an already-applied migration whose checksum has changed. SQL Server remains experimental; its whole-state document storage and scale characteristics are not resolved by this work. Distributed output observation, separate-process failure recovery, production performance, and broader provider codec fidelity remain outside this target unless covered by their own explicit work and evidence.

No source, index, or target HEAD was modified by this review. Only external review evidence was written.
