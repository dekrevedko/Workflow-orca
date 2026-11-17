## Why

Reshape Task 8.6 must ship the public DAG facade that doc 17 §17.2.6 already specifies: `IDagDefinitionRegistry`, `DagDefinitionHandle<TRunInput>`, `DagRunHandle`, snapshots, result unions and statuses. The run runtime behind it exists (create/reopen, read, terminal wait, cancellation on the durable bridge), but no application can reach it: tests reflect into `DagCoordinator.CreateOrReopenAsync`, plans are installed only as a side effect of create, and cancellation has no production caller.

The facade cannot be written under the current contracts. Both handles are public sealed types in `OrcaCore.Dag` with internal constructors, and their behavior must be supplied by `OrcaCore.Dag.Hosting`. That assembly may reference only three internal DAG types and fifteen exact members (the runtime view), none of which can construct a handle or connect one to the bridge. The canonical friend requirement says any further seam needs its own approved amendment. This is that amendment.

## What Changes

- Propose a **facade-handle grant** on the existing `OrcaCore.Dag -> OrcaCore.Dag.Hosting` friend: exactly two internal constructors, one on `DagDefinitionHandle<TRunInput>` and one on `DagRunHandle`. Every parameter is a public type or a delegate over public types. No new internal type, friend assembly, package edge or test friend. The exact allowlist becomes three non-public types and seventeen non-public members.
- Keep every typed DAG exception, foreign-plan node rejection, declared-output type checking and result-union construction inside `OrcaCore.Dag`. The hosting assembly signals outcomes only through delegate results and never constructs a DAG exception.
- Keep `DefinitionRegistrationConflict`, `StartIdempotencyConflict`, `WorkflowFailure` and `PayloadFingerprint` creation in `OrcaCore.Engine.Durable`. They cross the existing `OrcaCore.Durable.Hosting -> OrcaCore.Dag.Hosting` bridge as opaque public values. `OrcaCore.Dag.Hosting` projects public snapshots and statuses from committed run facts. The six-signature authoring allowlist is not widened and no tenth friend is added.
- **Typed run-input binding.** A DAG definition binding is identity, version, structural fingerprint **and** declared run-input contract, because the fingerprint deliberately excludes `TRunInput`. Registration, start replay, reopen and recovery all compare the four parts, with a typed outcome when only the run-input contract differs.
- **Integrity-fault outcome.** A run in the internal `IntegrityFault` state stays nonterminal, keeps its pre-fault public status in snapshots, and surfaces to waiters and to cancellation as a typed public failure.
- **BREAKING, proposed (doc 17 §17.2.6, pre-release surface):**
  - replace the listed internal constructors `DagDefinitionHandle()` and `DagRunHandle(DagRunId)` with the delegate constructors (D3);
  - add `DagRunFaultedException` (`DAG-RUN-FAULTED`) (D1);
  - extend `DagRunDefinitionMismatchException` with expected/actual version and fingerprint (D2);
  - add `DagRunInputContractMismatchException` (`DAG-RUN-INPUT-CONTRACT-MISMATCH`) and the closed `RunInputConflict` variants on `DagRegistrationResult<TRunInput>` and `DagStartResult` (D4).
- Record the source obligations Task 8.6 inherits: `Register` is the plan-install point for restart recovery; start replay resolves the authoritative start-key owner; node failures use the canonical DAG authored location.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `developer-facing-surface`: modify exactly "Implementation package boundaries use exact internal friends" (facade-handle grant, ownership split, four-part binding rules), "Closed registration and start results retain a cast-free success path" (the two closed `RunInputConflict` variants) and "Completion waits are notification-driven and race-free" (typed fault outcome for the DAG wait).
- `repository-foundation`: modify exactly "Dependency direction remains one-way" (the grant adds no friend, edge or type reference; three types and seventeen members).
- `management-and-querying`: modify exactly "DAG terminal waiting is notification-driven" (typed fault outcome, pre-fault status derivation, cancellation on a faulted run).

## Impact

Eventual source affects `OrcaCore.Dag` (public facade types, two internal constructors, two new public exceptions, one extended exception, two new union variants), `OrcaCore.Dag.Hosting` (registry, handle wiring, snapshot projection), `OrcaCore.Durable.Hosting` and `OrcaCore.Engine.Durable` (run facts and the four opaque values behind the existing bridge, start-key owner resolution), the `DagRuntimeViewBoundaryGuards` allowlist, the durable-bridge reference fixture, API baselines and package-consumer fixtures.

This draft changes no product source, canonical requirement, numbered document, guard or fixture. It adds only this change directory and its review packets. Five headings gain this change as an additional owner, so the post-gate rules require a process-only gate (Task 0) before the contract is reviewed. That gate needs registry rows, a provenance refreeze **and stage-aware edits to the guards' hard-coded owner-chain check and to the settlement validator**; none of it is part of this draft.

## RV-1 remediation (2026-11-15)

The first pre-gate draft was rejected at projected tree `6fb21010c787cf9962b2970a4c70997d57ccace0`. Its request and manifest stay byte-exact in `docs/review/`; its five mutable files are preserved byte-exact under `artifacts/rejected-pre-gate-draft-2026-11-15/` and its two proof files keep their original names. The reviewer found the two-constructor seam viable and required four revisions, all made here:

1. The declared run-input contract is now part of registration, start replay, reopen and recovery (design §3).
2. D1 is defined against durable facts, with the pre-fault status derivation, the three wait cases and cancellation; the two canonical wait requirements are now modified and `DAG-RUN-FAULTED` enters the doc 17 catalog (design §5).
3. Node failures use `dag:$/dag-node:<eight-digit ordinal>` and a defined occurrence (design §6).
4. The gate tasks name the guard change, the re-pin, the atomic sync, the split closeout and the substrate checkpoint (tasks.md).

## RV-2 remediation (2026-11-15)

RV-1 was rejected at projected tree `bc970f9a9358768c294c11dcebfa7f441ecb5171` with the seam upheld again. Its request and manifest stay byte-exact; its six mutable files are preserved under `artifacts/rejected-rv-1-draft-2026-11-15/` and its proof files keep their names. Three contract issues were raised and are resolved here:

1. Start under another run-input contract is now a closed, inspectable `DagStartResult.RunInputConflict` that keeps the owner's run id, and the canonical closed-results heading is modified (design §3).
2. An equal run-input contract key no longer establishes compatibility with a stored run; the durable side also verifies the stored bytes against the installed type before reopen and mapping, and local `Type` equality is distinguished from durable key equality (design §3).
3. `DagRunFaultedException` is level-triggered on an authoritative recheck; a fault reconciled before the recheck is not reported (design §5).

The guard work in Task 0.1 and 1.4 now also covers the settlement validator.

## RV-3 revision after prototype (2026-11-15)

RV-2 (projected tree `5f1a184d4e6c1667729cf67134e199e4a9b038c5`) was returned with one blocking inconsistency: it could accept a new run and an exact-byte replay, then refuse to reopen that run. Its request and manifest stay byte-exact; its six mutable files are preserved under `artifacts/superseded-rv-2-draft-2026-11-15/`. A throwaway prototype against the real bridge (`artifacts/task-1-1-public-handle-seam-prototype-evidence-2026-11-15.md`) then settled the open points:

1. **Run-input round-trip rule, at submission and at reopen/recovery.** The submitted run input must decode and re-encode to identical bytes, or start throws `NotSupportedException` before any run is committed or key bound. The stored input is verified the same way before a handle is issued or a mapper evaluated. The rule covers the DAG run input only.
2. **Locally unavailable.** A well-formed stored payload that fails the check under the installed binding makes the run locally unavailable: that host skips new mapping and admission, while committed child work is still processed by any durable host. Corrupt storage keeps its existing integrity classification.
3. **Representation, not meaning.** The byte check guarantees the stored representation; a change in meaning still requires a new definition version.
4. **Faults.** The level-triggered rule and cancellation outcomes are unchanged; the final regression test uses an explicit barrier.

No signature changed, so the RV-2 proof remains the current proof. The current two-assembly proof (`artifacts/task-1-1-public-handle-seam-rv-2-proof-*.cs`) compiles with warnings as errors against the real `OrcaCore` and `OrcaCore.Dag`. It proves signature sufficiency only; its bridge is a stand-in.
