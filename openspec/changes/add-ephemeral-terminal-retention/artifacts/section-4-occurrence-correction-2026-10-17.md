# Transition ledger correction carried into Section 4

This note supersedes only row 209's disposition in the immutable
`task-2-1-occurrence-ledger-2026-10-16.tsv`. Its effective disposition is **Edit**,
not Retain. The original MG-030 line said “purge, and poison maintenance belong
to `IWorkflowProviderMaintenanceStore` under operator policy;”. The reviewed
transition changed it to “purge, and poison maintenance for durable records belong
to `IWorkflowProviderMaintenanceStore` under operator policy;”. The inserted
durable-record qualifier is a real text edit, already captured by that row's
`replaced-range` and target-text columns. Its reason is the durable scope of
MG-030, consistent with neighboring row 208.

The historical 499-row artifact remains byte-exact. Every other row's original
disposition is unchanged by this correction. Master integration and its known
Task 7.3/provenance/production-readiness conflicts remain in the final reviewed
consumer-and-integration target, using a merge commit rather than a squash.
