# Task 7.6 forbidden-symbol qualified-owner audit — 2026-09-22

## Disposition

The 125-entry `ForbiddenPublicSymbols` catalog contained fifteen owner defects:

- ten ephemeral management types used `OrcaCore.Engine.Ephemeral.Management`, which existed only
  in removed `v3/` and `v3-cursor/` lineages, not in the promoted product lineage; their exact
  owner in the audited commits is `OrcaCore.Engine.Ephemeral`;
- `IWorkflowPayloadCodec` and `IWorkflowPayloadSerializer` named the later
  `OrcaCore.Provider.Abstractions` assembly even though their public historical owner was the
  pre-split `OrcaCore.Abstractions.csproj` project, whose current successor is the `OrcaCore`
  assembly named by the probe; and
- `WorkflowProjectionPressureMetrics`, `WorkflowProjectionStatistics`, and
  `WorkflowProjectionStatisticsGroup` never existed under the recorded provider namespace and are
  deleted rather than preserved as vacuous negatives.

The resulting catalog contains 122 exact identities. Its ordered SHA-256 is
`302b74d5ac5d52ddb2586002454f1f6771c37896beb643499b854ae85645c3a4`.

## Executable evidence

`RemovedDeferredAndWrongOwnerPublicSymbols_AreAbsentBeforeBaselineApproval` now reads immutable
source archives for commits `ac46d99543daf85c0fa3234272997ba40f47f96b` and
`666bc1e6ec57eb055f3fecbb8f74a64ebe2e1ea9`. Every retained identity must resolve to:

1. its named assembly's historical source root;
2. its exact namespace declaration;
3. its exact type declaration; and
4. its exact member declaration inside the named type's balanced body when the identity names a
   member; comments, strings, parameters, and sibling types cannot satisfy it.

Changing an assembly, namespace, type, or member to an impossible owner makes the regression fail.
The fresh-package marker fixture and production deletion ledger use the same corrected 122-entry
catalog, so compiler-negative, reflection-negative, and inventory evidence cannot diverge.
The 122-entry deletion inventory remains owned by reshape Task 7.17; harmonization Task 7.6
reviews its exact qualified-owner coordinates without taking over the original removal owner.
