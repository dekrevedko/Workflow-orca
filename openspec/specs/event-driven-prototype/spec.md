## Purpose

Record the OrcaCore event-driven prototype as planning history outside v1 and define the reviewed
capability gate required before any future return.

## Requirements

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
