## ADDED Requirements

### Requirement: Position-only workflow commits have an explicit protocol fact
`OrcaCore.Runtime.Protocol` SHALL declare `public sealed record WorkflowExecutionPositionChangedEvent : WorkflowEvent;` in namespace `OrcaCore.Abstractions.Durable`. The record SHALL add no payload members beyond its inherited workflow-event identity, instance, command, causation, occurrence-time and optional `ParentInstanceId`/`RootInstanceId` lineage members. `WorkflowEventCodec` SHALL serialize and deserialize it using discriminator `WorkflowExecutionPositionChangedEvent` and the fixed `orcacore-json-v1` representation. This SHALL be the separately admitted twenty-second workflow codec entry. The previously admitted 21 workflow mappings and their bytes SHALL remain unchanged, as SHALL separate DAG record/checkpoint framing.

Providers SHALL atomically append this fact and its checkpoint using the existing expected-version batch. The fact is a concurrency fence, not a replacement envelope or permission to reconstruct a new position from the previous checkpoint. Matched writer/reader protocol and provider binaries are required; unknown discriminators SHALL fail explicitly. No mixed-version rolling upgrade or persisted-development-data migration is promised by this admission.

#### Scenario: A position fact survives provider replacement
- **WHEN** one provider instance commits a position fact and checkpoint and another instance reads the same durable store
- **THEN** the fact retains all inherited fields, the checkpoint and lineage match the committed position, and a stale expected-version append is rejected

#### Scenario: Existing workflow and DAG records are read
- **WHEN** records using the prior workflow mappings or separate DAG framing are decoded by the current binaries
- **THEN** their existing identities and representations are unchanged by the additive workflow position fact
