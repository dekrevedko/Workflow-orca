# T5-06: Add durable saga audit and operator recovery

**Difficulty**: Sonnet        **Depends on**: T5-04, T5-05
**Spec**: SG-020, SG-021, SG-022        **AC**: AC-406, AC-407, AC-408

## Goal
Expose durable saga audit snapshots and operator recovery commands. Operators must be able
to inspect forward actions, compensation actions, order, outcomes, and recorded recovery
interventions without replaying volatile state.

## Read first
- `v3-gpt/src/OrcaCore.Engine.Durable/Management/DurableManagement.cs`
- `v3-gpt/src/OrcaCore.Engine.Durable/Management/WorkflowInstanceQueryModel.cs`
- `v3-gpt/src/OrcaCore.Abstractions/Providers/ProviderCommitContracts.cs`
- `v3-gpt/src/OrcaCore.Providers.InMemory/InMemoryWorkflowProvider.cs`
- `v3-gpt/src/OrcaCore.Providers.PostgreSql/PostgreSqlWorkflowStore.cs`
- Spec: `docs/specs/07-requirements-saga.md` section 7.3
- Spec: `docs/specs/12-acceptance-criteria.md` AC-406 through AC-408

## Deliverables
- Saga audit snapshot contracts under `v3-gpt/src/OrcaCore.Abstractions/Instances/`
- Projection writes for saga compensation state
- InMemory and PostgreSQL projection support
- Management query API for saga audit and operator recovery
- Tests in `v3-gpt/tests/OrcaCore.Engine.Durable.Tests/Sagas/SagaAuditTests.cs`
- Provider coverage in `v3-gpt/tests/OrcaCore.Providers.PostgreSql.Tests/`

## Tests to write FIRST
In `v3-gpt/tests/OrcaCore.Engine.Durable.Tests/Sagas/SagaAuditTests.cs`:
1. `SagaAudit_AfterCompensation_IncludesForwardCompensationOrderAndOutcome` - audit snapshot is complete. Trait AC-407.
2. `ManualRecovery_OnCompensationFailed_RecordsOperatorIntervention` - allowed intervention is queryable. Trait AC-408.
3. `SagaRestart_MidCompensation_DoesNotDuplicateActions` - rehydration resumes from durable facts. Trait AC-406.

## Implementation notes
This task touches provider projection contracts after Phase 2, so update provider
certification or provider-specific tests as needed. Keep audit data append/query oriented;
do not require providers to keep complete history forever beyond the retention-policy
decision.

## Out of scope
New third-party dependencies, UI, and RabbitMQ dispatcher behavior.

## Definition of done
- [ ] New tests are red before implementation and green after
- [ ] `dotnet test v3-gpt/OrcaCore.slnx --filter "AC=AC-406|AC=AC-407|AC=AC-408"` passes
- [ ] `dotnet build v3-gpt/OrcaCore.slnx` - zero warnings
- [ ] Provider projection/certification impact documented in PROGRESS.md
- [ ] PROGRESS.md updated; committed as "T5-06: durable saga audit and recovery (SG-020, SG-021, SG-022)"
