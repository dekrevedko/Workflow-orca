# T1-10: Add correlation index and routing modes

**Difficulty**: Sonnet        **Depends on**: T1-08
**Spec**: EV-010, EV-011, EV-012        **AC**: AC-106, AC-107, AC-108

## Goal
Add the public routing entry points for external events. Callers can deliver directly to one
instance, resolve a unique active wait by `(EventName, CorrelationId)`, or fan out to all
instances of one definition. The correlation index is a multi-map and uniqueness is enforced
at routing time, not wait registration time.

## Read first
- `src/OrcaCore.Abstractions/Events/EventEnvelope.cs`
- `src/OrcaCore.Engine.Ephemeral/EphemeralWorkflowEngine.cs`
- `src/OrcaCore.Engine.Ephemeral/Execution/IInstanceRegistry.cs`
- `src/OrcaCore.Engine.Ephemeral/Execution/WorkflowInstance.cs`
- Spec: `docs/specs/05-requirements-events-waits-timers.md` section 5.2

## Deliverables
- Internal correlation index maintained on wait registration, match, cancellation, and
  terminal cleanup.
- Public engine methods for instance-targeted, correlation-targeted, and definition-targeted
  fanout delivery.
- Routing result/report types that distinguish delivered, buffered, no active wait, and
  ambiguous correlation outcomes.

## Tests to write FIRST
In `tests/OrcaCore.Engine.Ephemeral.Tests/Execution/RoutingTests.cs`:
1. `RaiseByCorrelationAsync_OneActiveWait_ResumesThatInstance`
2. `RaiseByCorrelationAsync_ZeroActiveWaits_ReturnsNoActiveWait`
3. `RaiseByCorrelationAsync_MultipleActiveWaits_ReturnsAmbiguousWithoutDelivery`
4. `RaiseByDefinitionAsync_MultipleDefinitions_DeliversOnlyTargetDefinition`
5. `WaitRegistration_DuplicateCorrelationAcrossInstances_Succeeds`
6. `WaitMatch_RemovesWaitFromCorrelationIndex`

In `tests/OrcaCore.Acceptance.Tests/RoutingAcceptanceTests.cs`:
7. `[Trait("AC","AC-106")] CorrelationTargetedEvent_ResumesExactlyOne`
8. `[Trait("AC","AC-107")] CorrelationTargetedEvent_RejectsMissingOrAmbiguous`
9. `[Trait("AC","AC-108")] DefinitionFanout_IsScopedToTargetDefinition`

Use AwesomeAssertions for assertions.

## Implementation notes
Routing intent is selected by the API method, not by a flag on `EventEnvelope`. Keep the
index internal and derived from active runtime state. Do not add engine-wide fanout.

## Out of scope
Durable projections, external subscriptions, provider-backed indexes, pause-window buffering.

## Definition of done
- [ ] All new tests green; full affected suites green
- [ ] `dotnet build OrcaCore.slnx` - zero warnings
- [ ] AC-106 through AC-108 are green in `OrcaCore.Acceptance.Tests`
- [ ] PROGRESS.md updated; committed as "T1-10: correlation routing (AC-106-108)"
