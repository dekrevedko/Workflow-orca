## Purpose

Define the target saga-orchestration behavior for OrcaCore, including compensation and durable coordination, even though saga types are not yet implemented in source.
## Requirements

### Requirement: Saga remains an explicit deferred capability
The first release SHALL expose no public Saga builder, definition, action, adapter, outcome, management member, or reflection-visible placeholder. Product planning SHALL retain Saga in the future-capability registry with a re-entry gate requiring typed action/result authoring, deterministic compensation ownership/order, durable reverse progression, compensation failure, cancellation/timeout, manual remediation, versioning, audit, and restart acceptance.

#### Scenario: First-release public surface is inspected
- **WHEN** source, reflection, package, compile, and sample guards inspect Saga-related names
- **THEN** no callable Saga contract or obsolete alias is present

#### Scenario: Future Saga work begins
- **WHEN** a contributor proposes Saga after v1
- **THEN** a separate reviewed capability amendment defines the full state machine before any public member is added
