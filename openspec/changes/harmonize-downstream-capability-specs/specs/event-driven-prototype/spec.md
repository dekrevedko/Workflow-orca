## ADDED Requirements

### Requirement: The event-driven prototype is outside the first release
The event-driven prototype SHALL NOT be part of the first-release project, package, or public surface. The exhaustive first-release project list in `repository-foundation` SHALL remain authoritative, and no prototype engine, definition family, persistence model, or hosting extension SHALL ship in v1. No first-release guard, acceptance criterion, or documentation claim SHALL depend on prototype behavior.

#### Scenario: First-release package set is inspected
- **WHEN** package and architecture guards inspect the first-release manifest
- **THEN** no event-driven prototype project or package is present

#### Scenario: Contributor looks for the prototype runtime
- **WHEN** a contributor looks for the append-only prototype engine in the active codebase
- **THEN** it is absent, and this capability records it as a deferred exploration rather than an implemented slice

### Requirement: Future prototype work requires a reviewed capability amendment
Any future append-only or event-sourced runtime exploration SHALL enter through a separate reviewed capability amendment that defines its engine, persistence model, control-flow coverage, and package boundary against the accepted v1 contract. It SHALL NOT reintroduce removed concepts such as `WaitLong` or an authored `Yield`, and it SHALL NOT claim parity with the state-driven runtime without its own acceptance evidence.

#### Scenario: Contributor proposes prototype work after v1
- **WHEN** an append-only runtime slice is proposed
- **THEN** a separate reviewed amendment defines its scope before any project, package, or public member is added

## REMOVED Requirements

### Requirement: Prototype remains a separate runtime slice
**Reason**: No prototype project exists under `src/`, and `repository-foundation` declares the first-release project and package list exhaustive without it. Requiring the slice to exist as a distinct runtime and project boundary contradicts that exhaustive list. Replaced by "The event-driven prototype is outside the first release".

**Migration**: No released consumer migration exists. Use the ephemeral or durable engine; append-only exploration re-enters through a separate reviewed capability amendment.

### Requirement: Prototype uses append-only facts with derived state
**Reason**: The requirement described the persistence model of a runtime that does not ship in v1 and carries no first-release obligation. Its design intent is preserved as future work rather than as a normative first-release requirement.

**Migration**: No released consumer migration exists. The durable engine's event-sourced aggregate remains the supported restart-safe model.

### Requirement: Current implemented slice is intentionally narrow
**Reason**: The requirement documented the coverage of an implemented slice that is absent from the active codebase, so it described capability that no longer exists rather than bounding one that does.

**Migration**: No released consumer migration exists. Consult the accepted v1 node set and mode matrix for supported control flow.

### Requirement: Unsupported control-flow and infrastructure features stay explicit
**Reason**: The requirement enumerated `WaitLong` among features the prototype must not imply, but `WaitLong` is a removed concept required to carry no alias or tombstone, so the enumeration reintroduced the removed term. Future-scope control is retained by "Future prototype work requires a reviewed capability amendment".

**Migration**: No released consumer migration exists. Wait residency is a runtime and hosting policy; there is no separate authored long-wait member to reason about.
