## REMOVED Requirements

### Requirement: Saga workflows use a distinct definition kind
**Reason**: Saga authoring and definition types are deferred beyond v1; publishing an empty definition kind would create a compatibility promise before compensation semantics are approved.

**Migration**: No released consumer migration exists. Use ordinary typed workflows or `OrcaCore.Dag` where compensation is not required, and wait for the future Saga proposal where it is.

### Requirement: Compensation has explicit scope and reverse ordering
**Reason**: Compensation ordering, parallel-scope ownership, cancellation, and residual external-work behavior require a separate reviewed state-machine design and acceptance evidence.

**Migration**: Keep compensation application-owned for v1; do not depend on a provisional OrcaCore compensation order.

### Requirement: Saga outcomes include saga-specific terminal semantics
**Reason**: Saga-specific terminal outcomes depend on the deferred compensation and remediation model and are not part of the first-release workflow outcome contract.

**Migration**: Represent v1 business status in typed workflow output; no Saga terminal API ships.

### Requirement: Advanced saga support is durable and auditable
**Reason**: Durable reverse progression, compensation failure, manual intervention, recovery, audit, and version evolution are deferred together rather than implemented piecemeal.

**Migration**: No public Saga builder, definition, adapter, management surface, or placeholder remains in v1.

### Requirement: Saga records are owned by fibers and scopes
**Reason**: Saga-specific ownership records are unnecessary until the separate Saga proposal defines the complete durable compensation protocol.

**Migration**: The structured-fiber runtime remains available to future implementation, but v1 persists no public Saga record contract.

## ADDED Requirements

### Requirement: Saga remains an explicit deferred capability
The first release SHALL expose no public Saga builder, definition, action, adapter, outcome, management member, or reflection-visible placeholder. Product planning SHALL retain Saga in the future-capability registry at `docs/specs/13-phasing-and-open-questions.md` §13.4 ("Future-capability registry") with a re-entry gate requiring typed action/result authoring, deterministic compensation ownership/order, durable reverse progression, compensation failure, cancellation/timeout, manual remediation, versioning, audit, and restart acceptance.

#### Scenario: First-release public surface is inspected
- **WHEN** source, reflection, package, compile, and sample guards inspect Saga-related names
- **THEN** no callable Saga contract or obsolete alias is present

#### Scenario: Future Saga work begins
- **WHEN** a contributor proposes Saga after v1
- **THEN** a separate reviewed capability amendment defines the full state machine before any public member is added
