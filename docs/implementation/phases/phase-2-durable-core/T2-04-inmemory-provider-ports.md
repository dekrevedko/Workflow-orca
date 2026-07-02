# T2-04: Implement in-memory durable ports

**Difficulty**: Haiku        **Depends on**: T2-03
**Spec**: PR-030, PR-020, PR-021, PR-023        **AC**: AC-114, AC-305, AC-309, AC-310

## Goal
Implement all durable provider ports in `OrcaCore.Providers.InMemory` as the executable reference provider.
The implementation must pass the provider certification suite unchanged.

## Read first
- `v3-gpt/src/OrcaCore.Providers.InMemory/`
- `v3-gpt/src/OrcaCore.Abstractions/Providers/`
- `v3-gpt/tests/OrcaCore.ProviderCertification/`
- `v3-gpt/tests/OrcaCore.TestSupport/Providers/`
- Spec: `docs/specs/10-provider-model-and-extensibility.md` sections 10.3 and 10.4

## Deliverables
- InMemory implementations of event store, inbox, outbox, projection, timer, dispatcher, and serializer ports
- Concrete certification test fixture for the InMemory provider
- Internal synchronization preserving expected-version behavior.

## Tests to write FIRST
In `v3-gpt/tests/OrcaCore.ProviderCertification/InMemoryProviderCertificationTests.cs`:
1. `InMemoryEventStore_PassesEventStoreCertification`
2. `InMemoryInboxStore_PassesInboxCertification`
3. `InMemoryOutboxStore_PassesOutboxCertification`
4. `InMemoryProjectionStore_PassesProjectionCertification`

## Implementation notes
Providers.InMemory may reference only Abstractions. Keep locking simple and deterministic.

## Out of scope
Durable engine aggregate, PostgreSQL, background pumps, timers firing.

## Definition of done
- [ ] Certification suite passes for InMemory provider
- [ ] `dotnet build v3-gpt/OrcaCore.slnx` - zero warnings
- [ ] Providers.InMemory references only Abstractions
- [ ] PROGRESS.md updated; committed as "T2-04: in-memory durable ports (PR-030)"
