# T2-01: Add durable workflow event contracts

**Difficulty**: Haiku        **Depends on**: T2-00
**Spec**: DU-011, DU-012, DU-040        **AC**: none

## Goal
Introduce the closed durable command/event vocabulary and stream identity types used by the durable core.
The contracts describe engine facts only; business domain events remain outside OrcaCore.

## Read first
- `v3-gpt/src/OrcaCore.Abstractions/Ids`
- `v3-gpt/src/OrcaCore.Abstractions/Events/EventEnvelope.cs`
- `v3-gpt/src/OrcaCore.Abstractions/Instances/WorkflowInstanceSnapshot.cs`
- `v3-gpt/src/OrcaCore.Abstractions/Steps/StepResult.cs`
- Spec: `docs/specs/06-requirements-durable-execution.md` sections 6.2 and 6.5

## Deliverables
- Durable stream/version/causation IDs in `v3-gpt/src/OrcaCore.Abstractions/Ids/`
- Durable command records in `v3-gpt/src/OrcaCore.Abstractions/Durable/`
- Durable workflow event records in `v3-gpt/src/OrcaCore.Abstractions/Durable/`
- XML docs for every public contract.

## Tests to write FIRST
In `v3-gpt/tests/OrcaCore.Core.Tests/Durable/DurableContractTests.cs`:
1. `WorkflowEventCatalog_AllEventsCarryInstanceAndCausationMetadata`
2. `WorkflowCommandCatalog_AllCommandsCarryCommandIdAndInstanceIdentity`
3. `StreamVersion_NegativeValuesRejected`
4. `WorkflowStartedEvent_CarriesBoundDefinitionVersion`

## Implementation notes
Keep contracts in Abstractions with no provider dependency. Do not add event-store ports yet.

## Out of scope
Command decision logic, event serialization, append operations, checkpoints, inbox, outbox, projections.

## Definition of done
- [ ] All new tests green; full affected suites green
- [ ] `dotnet build v3-gpt/OrcaCore.slnx` - zero warnings
- [ ] Abstractions still references no implementation project
- [ ] PROGRESS.md updated; committed as "T2-01: durable contracts (DU-011, DU-012, DU-040)"
