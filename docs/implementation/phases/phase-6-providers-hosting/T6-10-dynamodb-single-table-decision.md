# T6-10: Resolve DynamoDB implementation deferral gate

**Difficulty**: Sonnet        **Depends on**: T6-09
**Spec**: PR-010, PR-020, PR-021        **AC**: none

## Goal
Resolve IOQ-10 before any DynamoDB implementation begins. The owner decision is to defer
DynamoDB implementation for this run and preserve compatible provider interfaces for a
future adapter, not to choose a concrete table design now.

## Read first
- `docs/implementation/00-stack-decisions.md`
- `docs/implementation/04-task-protocol.md`
- `docs/implementation/phases/phase-6-providers-hosting/README.md`
- `v3-gpt/src/OrcaCore.Abstractions/Providers/ProviderPorts.cs`
- `v3-gpt/src/OrcaCore.Abstractions/Providers/ProviderCommitContracts.cs`
- Spec: `docs/specs/10-provider-model-and-extensibility.md` PR-010, PR-020, PR-021

## Deliverables
- Update `docs/implementation/00-stack-decisions.md` with the IOQ-10 resolution
- Update Phase 6 task guidance to state DynamoDB implementation is unscheduled in this run
- Update `docs/implementation/phases/phase-6-providers-hosting/PROGRESS.md`

## Tests to write FIRST
No product tests. This is a registered open-question resolution and task-splitting task.

## Implementation notes
The compatibility decision must state that expected-version append, tail loading,
checkpoints, inbox, outbox, and projection metadata remain represented by the existing
provider ports and `ProviderCommitBatch`. Future DynamoDB work must choose a concrete table
design only when the plugin is reopened, then prove the same invariants through provider
certification. If the owner had not resolved IOQ-10, this task would record a blocker and
stop.

## Out of scope
Adding AWS packages, writing DynamoDB provider code, changing provider ports, or creating a
concrete DynamoDB table design.

## Definition of done
- [ ] IOQ-10 is resolved or a blocker is recorded in PROGRESS.md
- [ ] Phase 6 task guidance states DynamoDB implementation is deferred for this run
- [ ] No `v3-gpt/` code is changed
- [ ] PROGRESS.md updated; committed as "T6-10: resolve dynamodb implementation deferral"
