# Phase A correctness evidence

This record covers source checkpoint `c27b0b4e` and its review follow-up. It is evidence for a still-open implementation phase, not provider qualification or approval to merge. The governing plan is `architecture-remediation-plan-2026-11-16.md` on the planning branch at `51753582`.

## Reviewed behavior

The original checkpoint repairs coherent durable loading and version advancement, ephemeral deadline election, outcome fingerprint identity, and host-registration profile equality. An independent source reviewer reproduced and helped close two additional cancellation/backoff races and a timer-double defect. The owner subsequently built the checkpoint fresh and reported all 45 new tests passing. That is owner-reported evidence, distinct from the author runs below.

The follow-up completes length framing of fingerprint fields while format 2 is still unreleased. Event names, branch ids, pool keys, type/schema identities and partitioner fields cannot inject structural delimiters. UTF-16 code units are encoded losslessly as hexadecimal, including accepted unpaired surrogates; numeric formatting and schema ordering are invariant. It also parks a stable unreconciled checkpoint/tail with the offending event type in the diagnostic; genuinely advancing streams retain their bounded conflict/retry path. Ephemeral waits observe either the attempt decision or physical-task failure, so a wrapper failure before result observation does not require a timer callback. These repairs preserve the existing clock-based winner election, including the interval between electing timeout and publishing its decision after the cancellation callback.

Independent follow-up review reproduced two further edge cases: UTF-8 replacement fallback collapsed distinct malformed names, and a physical wrapper failure could overtake an already-elected timeout before decision publication. Both received failing regression tests before their fixes. The reviewer found no actionable issue in stable-tail parking or the retention/event-volume evidence.

## A.5: retention and tail continuity

The accepted plan makes additional continuity checks conditional on supported retention removing events needed after a captured checkpoint. The source audit found no such trimming operation:

| Provider | Retention implementation | Observed behavior |
|---|---|---|
| InMemory | `src/OrcaCore.Providers.InMemory/InMemoryWorkflowProvider.cs`, `PurgeForMaintenanceAsync` / `DeleteInstanceData` | Under the provider lock, checks maintenance blockers and removes the whole instance's checkpoint and stream together. |
| PostgreSQL | `src/OrcaCore.Providers.PostgreSql/PostgreSqlWorkflowStore.Retention.cs`, `InstanceDataDeleteStatements` / `DeleteInstanceDataAsync` | Purges the checkpoint, events and stream head in one transaction under maintenance table locks. |
| SQL Server | `src/OrcaCore.Providers.SqlServer/Internal/SqlServerWorkflowStateEngine.cs`, `PurgeForMaintenanceAsync` / `DeleteInstanceData` | Removes the complete instance state, including checkpoint and stream, through the adapter's transactional state engine. |

Checkpoint advancement and ContinueAsNew retain the append-only history. A concurrent whole-instance purge does not leave a retained checkpoint with a shortened suffix. A stale in-flight advancement still faces expected-version append checking against the authoritative stream; removing a nonempty stream does not let its old version commit into an empty one.

The existing `LoadTailAsync` result has no stored sequence numbers. Counting its returned events would not validate continuity. For this phase, the audit supports relying on the existing ordered, complete, append-only provider contract. No sequence-bearing provider API or generic storage-corruption gate is added. Revisit this decision before introducing partial history trimming, replica reads without suitable consistency, or non-atomic checkpoint/event retention. This is a source-audit conclusion; real-provider correctness certification remains open.

## Yield event accounting

The measured workflow initializes a mutable counter, executes one named increment step inside `While` until the iteration limit, and ends. It uses the in-memory provider, command budgets 1 and 256, and a one-minute segment-duration ceiling. The experiment counts append batches and persisted events, and UTF-8 encodes each position event through `WorkflowEventCodec` to count its JSON bytes.

| Iterations | Command budget | Baseline commits | Candidate commits | Baseline events | Candidate events | Added position events | Added event JSON bytes |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 100 | 1 | 202 | 202 | 103 | 203 | 100 | 33,491 |
| 100 | 256 | 202 | 202 | 103 | 203 | 100 | 33,489 |
| 10,000 | 1 | 20,002 | 20,002 | 10,003 | 20,003 | 10,000 | 3,348,895 |
| 10,000 | 256 | 20,002 | 20,002 | 10,003 | 20,003 | 10,000 | 3,348,945 |

The baseline is `ba2b057d`. The 100-iteration measurements were collected during the implementation that became `c27b0b4e`; the 10,000-iteration measurements use that exact source commit in a disposable checkout. The added fact is included in an already-existing yield commit: one extra event per iteration in this workload, zero extra commit boundaries. JSON length varies slightly with timestamp formatting. These are event-volume measurements, not latency, throughput, WAL, index, database-row, or whole-store SQL Server write measurements. Physical provider costs remain to be measured when qualifying those providers.

Reproduction starts from `DurableCoherentLoadTests.LongLoopCountsPositionFactsWithoutAddingCommitBoundaries` in the candidate. For 10,000 iterations, change only the iteration limit and increase the test's maximum drive-turn guard proportionally in a disposable checkout. The baseline probe uses the same loop and counting store, omits candidate-only assertions that every position checkpoint has an event, and identifies position events by their CLR name so it compiles against the older protocol. The initial large-loop run hit the original 1,000-turn test guard at budget 1 in both versions; after scaling that harness guard, both budgets passed in both versions. Raw TRX files and disposable probes are held locally and are not committed.

## Qualification boundaries

The original source checkpoint built with zero warnings/errors. Author full-suite evidence after its review remediation: Core 396, Durable 127, Ephemeral 426, Hosting 68, Acceptance 37 and in-memory ProviderCertification 116 passed. Independent remediation checks passed 14 durable and 151 deadline/ownership cases, including the original adversarial cancellation and backoff probes. The newer follow-up has separate focused red/green evidence; these original counts do not certify changed follow-up source.

The reported 311/319 Infrastructure run had eight qualification failures: four API/source/inventory snapshots, two absent package-feed checks, and two historical DAG slice-scope guards. The follow-up built all twelve exact-version packages and reran the two feed checks successfully after placing the feed at their required repository-relative location. The six snapshot/scope failures remain; this targeted rerun is not a new full-lane pass. These are not product-runtime failures and are not relabeled as intentional ExpectedRed tests. The DAG guard repairs belong to the concurrent DAG track. Phase A still must admit the new protocol fact in normative declarations, refresh its exact affected snapshots and package artifacts after final integration, integrate the landed facade predecessor's PostgreSQL lineage changes, and run affected real PostgreSQL and retained experimental SQL Server certification. B and C retain their facade predecessor dependency. No source checkpoint or green unit suite closes those obligations.
