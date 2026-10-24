## ADDED Requirements

### Requirement: Ephemeral native execution preserves closed typed dispatch
The supported ephemeral native journey SHALL retain statically reachable closed operations from typed authoring/registration through bootstrap, definition installation/identity, start snapshots, step contexts, callbacks/selectors/merges, typed waits/events, query/output, retry, branch/item snapshots and failure construction. It SHALL NOT depend on dynamic assembly loading, runtime-only generic instantiation, DynamicInvoke, compiled expression IL, property-name dispatch or nonpublic-constructor discovery. Any retained bounded reflection SHALL have individual metadata and analyzer proof. Both reference-type and value-type closures SHALL execute without assuming runtime generic code generation. Replacing dispatch SHALL preserve all existing ordering, commit authority, physical ownership, cancellation, retention and shutdown semantics.

#### Scenario: A typed event resumes a value-state workflow
- **WHEN** a native published consumer registers, starts and resumes a closed value-state workflow using typed StepResult and event payloads
- **THEN** all adapters exist at publish time and execution, detached output and physical shutdown complete through the original typed semantics

#### Scenario: A downstream composition path follows successful registration
- **WHEN** a native consumer executes named/delegate steps, retry, timer/resource callbacks, root Parallel and bounded ForEach, including failure aggregation
- **THEN** those paths run and preserve their original assertions rather than being credited from a successful registration or publish alone
