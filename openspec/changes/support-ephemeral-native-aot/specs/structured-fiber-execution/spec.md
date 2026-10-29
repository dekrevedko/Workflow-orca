## ADDED Requirements

### Requirement: Ephemeral native execution preserves closed typed dispatch
The supported ephemeral native journey SHALL retain statically reachable closed operations from typed authoring/registration through bootstrap, definition installation/identity, start snapshots, step contexts, callbacks/selectors/merges, typed waits and public event-value boundaries, query/output, retry, branch/item snapshots and failure construction. It SHALL NOT depend on dynamic assembly loading, runtime-only generic instantiation, DynamicInvoke, compiled expression IL, property-name dispatch or nonpublic-constructor discovery. Any retained bounded reflection SHALL have individual metadata and analyzer proof. The approved StepExecutionThrottle.For<TStep> interface check SHALL retain its existing body and validation order, with only DynamicallyAccessedMembers(Interfaces) on its public generic parameter; exact-step identity and physical throttle ownership SHALL remain unchanged. Both reference-type and value-type closures SHALL execute without assuming runtime generic code generation. Replacing dispatch SHALL preserve all existing ordering, commit authority, physical ownership, cancellation, retention and shutdown semantics.

#### Scenario: A typed wait parks a value-state workflow
- **WHEN** a native public-package consumer registers and starts a closed value-state workflow whose named step returns typed StepResult.WaitForEvent and constructs a detached typed inbound value
- **THEN** its typed descriptor, parked snapshot and physical cancellation/join use statically reachable adapters, while live event delivery/resumption and outgoing publishing receive no native credit because those capabilities are not exported by the selected ephemeral closure

#### Scenario: A downstream composition path follows successful registration
- **WHEN** a native consumer executes named/delegate steps, retry, timer/resource callbacks, root Parallel and bounded ForEach, including failure aggregation
- **THEN** those paths run and preserve their original assertions rather than being credited from a successful registration or publish alone
