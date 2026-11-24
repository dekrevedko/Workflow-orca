## MODIFIED Requirements

### Requirement: Durable resource governance is one serialized provider aggregate
V1 SHALL maintain one resource-governance aggregate per configured `ResourceGovernancePartitionId`. The aggregate SHALL own every immutable pool creation definition, FIFO queued atomic multi-pool request, reservation/ticket, review mark, idempotent resize operation, confirmation binding, and tombstone in that partition. `DurableResourcePoolDefinition.Capacity` SHALL be immutable creation capacity, while `DurableResourcePoolSnapshot.ConfiguredCapacity` SHALL be current capacity after committed resizes. Startup SHALL atomically create definitions; later hosts SHALL present identical creation name/capacity/review values or fail even when current capacity differs, and startup SHALL NOT reset current capacity or resize debt. `ReviewAfter` SHALL mark for reconciliation only. `GetAsync` and `ResizeAsync` SHALL throw `ResourcePoolNotConfiguredException` for an unknown pool before mutation. `ResizeAsync` SHALL reject capacity less than one with `ArgumentOutOfRangeException` before operation-ID binding or aggregate mutation; otherwise it SHALL be idempotent by `ResourcePoolOperationId`, return the closed `DurableResourcePoolResizeResult.Applied` or `.Conflict` outcome, and expose no force-release path. Exact replay of one operation ID/pool/capacity tuple SHALL return the originally recorded `Applied`; reuse with another pool or capacity SHALL return `Conflict` containing recorded and attempted mutation facts and SHALL perform no mutation.

The engine SHALL use `IDurableResourceGovernanceStore.LoadLatestAsync(partitionId, cancellationToken)` for partition initialization, current-state reads, mutations and conflict retries. The provider SHALL return one committed view of the requested partition, its authoritative version and its latest record. `ResourceGovernanceHead.Create(partitionId, version, record)` SHALL require a non-null partition, a nonnegative version, no record at zero and one record at a positive version with matching sequence. The engine SHALL reject a head for another partition. It SHALL decode only the selected v1 `state-committed` full state and validate its current ownership and accounting before serving it or appending at the returned version. A damaged selected record or a missing/mismatched latest record relative to the available head version SHALL fail closed. Recovery SHALL NOT fall back to an older record. Only a version-zero head may initialize a partition; an invalid empty state at a positive version SHALL fail without appending creation facts. A read SHALL return detached record bytes; the v1 state envelope remains readable.

Experimental SQL Server SHALL store a per-partition version on new writes. Only a legacy partition document with an absent version property MAY use its record count as the available head. Its latest sequence, format, checksum and state SHALL still be validated. A present null, non-integral, negative or mismatched version SHALL be rejected. A coherent suffix removal from a legacy document can be indistinguishable from a legitimately shorter legacy partition. No provider can identify replacement by an older internally consistent whole document using that document alone. Reading a legacy document SHALL NOT save or migrate it.

Explicit full-history inspection SHALL continue through `LoadAsync`. `ResourceGovernanceRecord.FromPersisted` SHALL validate one positive sequence, supported protocol format, checksum, and a defensive payload-byte copy. `ResourceGovernanceStream.Create(long version, IReadOnlyList<ResourceGovernanceRecord> records)` SHALL reject a negative version, null collection, null entry, gap, duplicate, reordering, or version mismatch; defensively copy the collection; require version zero exactly when empty; and require the complete v1 history sequence `1..Version`. `AppendAsync` SHALL reject/certify an empty or nonconsecutive batch, defensively copy it, require sequence `expectedVersion + 1` through `expectedVersion + count`, and commit the whole ordered batch or return expected-version `Conflict` without a partial append.

The runtime SHALL expose exactly four internal post-commit certification barriers, never application or provider-storage API: `WorkflowPendingObligationCommitted`, `GovernanceReservationCommitted`, `WorkflowActivationCommitted`, and `GovernanceOwnershipConfirmed`. Their enum/fact/gate types SHALL remain internal in `OrcaCore.Engine.Durable.ResourceGovernance`; `OrcaCore.ProviderCertification` SHALL consume them through the sole cross-package test friend edge in the exact repository friend graph. Each barrier fact SHALL be immutable and carry partition, lease obligation, instance, generation, fiber occurrence, scope-entry occurrence, protection token, workflow and governance stream versions, plus the exact ticket ID, pool, units, and provider generation for every ticket then known. The barrier SHALL signal and block after the named durable commit and before the next protocol command so the deterministic certification fixture can stop the host at that boundary. Re-entry MAY report the same occurrence/barrier again but SHALL preserve equal facts and SHALL NOT create a new obligation or ticket.

#### Scenario: Atomic request crosses pools
- **WHEN** one request needs multiple pools in the configured partition
- **THEN** one expected-version governance transition grants every requirement or none and preserves fixed FIFO committed-request order

#### Scenario: Workflow/governance handoff crashes
- **WHEN** certification stops a host at any one of the four named post-commit barriers
- **THEN** recovery completes or compensates the exact occurrence while all uncertain tickets remain reserved and workflow/governance ownership agrees on obligation, instance, generation, fiber, scope entry, token, ticket, pool, units, and provider generation

#### Scenario: Provider persists governance
- **WHEN** a durable provider implements `IDurableResourceGovernanceStore`
- **THEN** `LoadLatestAsync` returns the head for ordinary recovery, explicit `LoadAsync` creates one defensively copied full stream through `ResourceGovernanceStream.Create`, and expected-version ordered-batch `AppendAsync` preserves per-record checksum/format validation, exact sequence continuity, all-or-conflict append, and no partial batch

#### Scenario: Persisted governance stream is malformed
- **WHEN** a provider returns a null/mutable record collection, version mismatch, sequence gap, duplicate, reordering, unsupported format, bad checksum, or payload later mutated by its source buffer
- **THEN** explicit history validation rejects the malformed stream or the accepted detached record remains unchanged by source mutation

#### Scenario: Resize operation ID is reused with changed intent
- **WHEN** one `ResourcePoolOperationId` was recorded for a pool/capacity and is submitted with another pool or capacity
- **THEN** `ResizeAsync` returns `DurableResourcePoolResizeResult.Conflict` with recorded and attempted facts, changes no pool state, and grants no waiter

#### Scenario: Host restarts after a persisted resize
- **WHEN** a pool was created at capacity N, resized to current capacity M with possible debt, and a later host supplies the original creation definition N
- **THEN** startup succeeds and retains M and debt; supplying M or any other value as creation capacity fails even when it equals current capacity

#### Scenario: Pool management input is invalid
- **WHEN** `GetAsync`/`ResizeAsync` names an unconfigured pool or `ResizeAsync` receives zero or negative capacity
- **THEN** the typed unknown-pool or range exception occurs before operation-ID binding, aggregate mutation, queue change, or provider append

#### Scenario: Provider only has per-workflow streams
- **WHEN** a provider cannot serialize the configured cross-workflow pool partition
- **THEN** startup/certification rejects it as incapable of v1 durable leasing

#### Scenario: Recover the current partition state
- **WHEN** a nonempty partition is reopened or a pending command retries after an append conflict
- **THEN** the engine reloads the authoritative head, restores its complete current state including retained identity bindings and bases its next append on that head's version

#### Scenario: Latest record cannot establish current ownership
- **WHEN** the head has the wrong partition, a positive version lacks its record, the latest sequence disagrees with the head, or the selected record has an invalid format, checksum, state envelope or current accounting
- **THEN** initialization or the current operation fails before returning state, granting tickets, appending a transition or treating the partition as empty
