# T2-03: Add provider fakes and certification skeleton

**Difficulty**: Sonnet        **Depends on**: T2-02
**Spec**: PR-020, PR-021, PR-024, EV-032, DU-020, DU-030        **AC**: AC-114, AC-305, AC-309, AC-310

## Goal
Create reusable provider fakes and the first abstract certification tests for durable port invariants.
Certification tests must be provider-neutral and inherited unchanged by concrete providers.

## Read first
- `src/OrcaCore.Abstractions/Providers/`
- `tests/OrcaCore.ProviderCertification/`
- `tests/OrcaCore.TestSupport/`
- `tests/OrcaCore.ProviderCertification/OrcaCore.ProviderCertification.csproj`
- Spec: `docs/specs/10-provider-model-and-extensibility.md` section 10.3

## Deliverables
- Failure-injectable fake stores in `tests/OrcaCore.TestSupport/Providers/`
- Abstract certification base classes in `tests/OrcaCore.ProviderCertification/`
- Certification traits for provider-sensitive ACs introduced in this task.

## Tests to write FIRST
In `tests/OrcaCore.ProviderCertification/EventStoreCertificationTests.cs`:
1. `[Trait("AC","AC-309")] ConcurrentAppend_SameExpectedVersion_OneWinnerOneConflict`
2. `[Trait("AC","AC-114")] CommitFailure_BeforeApply_LeavesWaitAndInboxEventAvailable`
3. `[Trait("AC","AC-305")] InboxDuplicate_AfterRecordedApplied_IsIgnored`
4. `[Trait("AC","AC-310")] OutboxRecords_AreNotVisibleWhenCommitFails`

## Implementation notes
Fakes live in TestSupport and are not production providers. Abstract tests may be skipped only by not inheriting them; the abstract base itself should compile and be executable through a test concrete fake.

## Out of scope
Production InMemory provider implementation, PostgreSQL, durable engine command pipeline.

## Definition of done
- [ ] Certification tests pass against fake stores
- [ ] `dotnet build OrcaCore.slnx` - zero warnings
- [ ] No provider-specific assumptions in certification base classes
- [ ] PROGRESS.md updated; committed as "T2-03: provider certification skeleton (PR-024)"
