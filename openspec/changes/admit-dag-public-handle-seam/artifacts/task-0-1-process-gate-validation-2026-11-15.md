# Task 0.1 process-gate validation

Date: 2026-11-15. Parent: `da1a4729ea2cdb9474eb219eb507760574f83dcb`.
Scope: process preparation only, pending independent review. Task checkboxes remain open.

## Checks

- Guards project Debug build with warnings as errors: zero warnings, zero errors.
- ProviderCertification project Debug build with warnings as errors: zero warnings, zero errors. This supplies the compiled evidence required by the eleven Section7Scenario 3.11d guards; it is not a provider test run.
- `CanonicalSynchronizationGate_EnumeratesCapabilitiesDeltasAndRequirementOwners` and `Task53_ReshapeSolelyOwnsFriendTopologyAndDurableGovernanceAggregate`: 2 passed, 0 failed.
- Strict OpenSpec validation for this change: structural pass only.
- Complete `Disposition=Infrastructure` guard lane: 319 passed, 0 failed, 0 skipped (2 minutes 59 seconds). The twelve exact local packages were packed before this run.
- Full solution Release build with warnings as errors: zero warnings, zero errors.

The provenance record is measured by the compiled guard's own parser and classifier: 223 rows, 59,416 bytes, SHA-256 `ca4411bd09c9770fdb8d123c9e85f5a35bfd787f9568bf10b41e23721a7411de`. There are 198 synchronized operations, 17 existing superseded operations, 5 pending modifications and 3 new-capability requirements outside canonical. Semantic approval eligibility is false. Active capability directories: 39, 3,184 bytes, SHA-256 `862963abaa1e185edfdc378405b03362d055fc46a5741cb1a17d031c31611853`.

The full-lane preparation additionally required exact extension projections in `EphemeralShutdownTransitionGuards.cs`, `NativeSupportTransitionGuards.cs` and `EphemeralFailureSettlementTransitionGuards.cs`. Each validates the five-row Proposed extension before comparing every earlier registry value. The initial lane exposed these stale schema/whole-registry assertions and missing local package prerequisites; all are resolved in the green run.

## Negative probes

A temporary console harness loads the compiled Guards assembly. It reads live canonical and active requirements using the guard's private parsers, deserializes the registry into the guard's own checkpoint type, then invokes the named private validator. This isolates each check from earlier failures in the encompassing test. No product internals are invoked. The baseline and restored runs of all three validators pass. Mutated files are restored byte-for-byte in `finally`; the extra-owner change is removed after each probe.

| Probe | Validator | Observed result |
|---|---|---|
| Add a valid unnamed fifth owner of the friends heading | `ValidateActivePostGateOwners` | Red, exact owner-chain equality |
| Add a valid unnamed fourth owner of completion-waits | `ValidateProposedEphemeralFailureSettlementSuccessors` | Red, exact owner-chain equality |
| Add an unnamed third owner to another settlement heading | `ValidateProposedEphemeralFailureSettlementSuccessors` | Red, unchanged two-owner assertion |
| Change one byte in a proposed block (`SHALL` to `SHALX`) | `ValidateProposedDagFacadeSeamSuccessors` | Red, proposed block hash |
| Change one byte in the immediate runtime-view predecessor | `ValidateProposedDagFacadeSeamSuccessors` | Red, predecessor block hash |
| Set one new row to ApprovedPending without synchronization | `ValidateProposedDagFacadeSeamSuccessors` | Red, source-pinned Proposed row equality |
| Check Task 1.3 | `ValidateProposedDagFacadeSeamSuccessors` | Red, required Open task state |
| Remove each of the five registry rows in turn, retaining its delta | `ValidateProposedDagFacadeSeamSuccessors` | Five red results, exact five-row admission fails before duplicate ownership can be waived |

All eight planned negative cases passed, including five individual row removals (twelve mutations). A positive control temporarily checks only Tasks 0.1 and 0.2: the facade validator stays green. It then restores both checkboxes to open. This proves the process-stage pins permit later checkbox activation; it grants no approval.

## Reproduction and limits

```powershell
dotnet build tests/OrcaCore.ProviderCertification/OrcaCore.ProviderCertification.csproj --no-restore -warnaserror
dotnet build tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj --no-restore -warnaserror
dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj --no-build --filter 'Disposition=Infrastructure'
openspec validate admit-dag-public-handle-seam --strict
```

No product source, canonical spec, predecessor block, existing Complete evidence, signature proof, compiled friend allowlist or expected-red disposition is changed. No product/provider runtime suite or prototype test was rerun. The unchanged RV-4 contract and its earlier runtime evidence retain their stated limits. The negative probes exercise the proposed-stage guards only; the future atomic canonical/ApprovedPending transition remains Task 1.3/1.4.
