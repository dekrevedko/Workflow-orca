## Context

Source checkpoints through `2fe5b9c6` repair the four Phase A findings under existing guarantees. Normative admission and real-provider qualification remain separate obligations. The repository is greenfield with no supported persisted development data. The owner will merge the DAG changes separately; this branch must incorporate that committed result before final baseline capture and certification.

## Goals / Non-Goals

Goals: exact declaration of the workflow position fact and compiler format 2 encoding, coherent existing semantics, current baselines, and real-store proof of version fencing and publication races. Preserve all existing workflow event mappings and DAG framing. Keep source review, normative approval and provider qualification distinguishable.

Non-goals: new atomic-read provider API, sequence-bearing tails, mixed-version rollout compatibility, deployed-database migration, facade activation, shared value-codec fidelity, distributed output observation, SQL Server storage redesign or promotion.

## Decisions

1. Add specialized requirements to existing capabilities. Existing crash safety, atomic scope commits, deadline scopes and role-idempotency requirements remain correct. The new headings have one owner and do not replace an existing DAG/facade requirement. The original 21 codec entries remain untouched; the workflow-only position entry is additive.
2. A checkpoint-position change must advance the optimistic stream version. Yield and fiber failure include the marker in their existing batch. An atomic read alone cannot repair same-version checkpoint replacement. The loader returns the same captured checkpoint used for aggregate reconstruction. It classifies the whole tail, honors terminal/cancel/park state before execution, retries changing contention, and quarantines repeated identical unreconciled positions. Quarantine does not repair or rewrite the checkpoint.
3. Retain the provider's ordered complete append-only tail guarantee. Current retention deletes whole instances atomically; the A.5 audit found no supported suffix trimming. Counting returned events would not validate gaps. A sequence API is deferred until a supported retention/read model requires it.
4. Complete format 2's structural encoding before release: invariant UTF-16 code-unit length and uppercase hexadecimal code units, explicit null/outcome tags, invariant numbers and ordinal schema ordering. Keep compiler format, authored version and definition identity as separate binding values excluded from fingerprint inputs. Opaque delegates stay opaque; users must version changed code.
5. Existing deadline election compares the runtime observation clock with the absolute deadline and retains an elected winner through delayed publication/physical settlement. Host profiles compare normalized typed fields, never separator-joined strings. New quality obligations name these behaviors without duplicating their existing owning requirements.
6. Import the merged DAG commit, not its working-tree edits. Verify the PostgreSQL lineage insert/load/update/null-clearing fix after conflict resolution. Shared provider tests must serialize the marker, preserve checkpoint lineage, and run position-only fencing and publication interleavings with independently constructed stores/runtimes. Run complete PostgreSQL and SQL Server projects as well as focused cases.

## Risks / Trade-offs

- An extra event is written per yield/fiber failure. The recorded 10,000-iteration loop adds 10,000 events and about 3.35 MB JSON without extra commit boundaries. This is event volume, not a physical I/O or throughput qualification.
- The new discriminator requires matched protocol/provider versions. Unknown discriminators fail explicitly; old readers are not a supported rolling-upgrade path.
- PostgreSQL's reviewed lineage change modifies initial migration bytes. Existing migration-journal hashes reject changed applied migrations. Qualification uses fresh/reset databases; no upgrade compatibility is claimed.
- SQL Server real-store correctness does not fix its document/history scaling. Experimental scope stays as selected by the remediation plan; support-document publication belongs to its separate change.
- Whole-document approval pins and API snapshots must preserve historical evidence. New exact projections and the bounded amendment record bind current additions without weakening historical guards.

## Contract and evidence route

Review the four additive deltas and numbered-declaration text before synchronizing canonical specs. Record this late admission honestly; the source checkpoints already exist. Use one bounded machine-readable post-gate extension for this change rather than adding entries to the legacy reshape-only record. Record exact delta hashes, numbered before/after projections, approval evidence, tasks and source/provider validation. A pending delta remains pending in provenance until synchronization; structural CLI validation alone is not semantic approval.

The complete major steps are contract admission, integrated baselines/provider qualification, and final scoped review/checkpoint. Preserve the user's concurrent DAG work and its remaining activation/certification obligations.
