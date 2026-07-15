## Purpose

Define the operator-facing query and command surface used to inspect and control OrcaCore workflow instances.

## Requirements

### Requirement: Management API is scope-oriented and fluent
The management surface SHALL support engine-wide, definition-scoped, and instance-scoped operations through a fluent model that separates selection from terminal commands and terminal queries.

#### Scenario: Operator targets one workflow instance
- **WHEN** an operator needs to inspect or control a specific instance
- **THEN** they can enter instance scope directly and invoke instance-specific queries or commands without list-style indirection

### Requirement: Filtering semantics are constrained and translatable
Management filtering SHALL use a constrained, translatable predicate model over queryable runtime metadata rather than arbitrary runtime delegates.

#### Scenario: Durable provider evaluates a filter
- **WHEN** an operator filters instances by status, definition, or timestamps
- **THEN** the filter can be translated consistently across in-memory and durable execution modes

### Requirement: Inspection exposes operationally useful metadata
The management surface SHALL expose active waits, runtime status, timestamps, grouped statistics, and related operational metadata without requiring business payload deserialization alone.

#### Scenario: Operator investigates waiting instances
- **WHEN** an operator queries for waiting workflows
- **THEN** the resulting inspection surface includes wait identity, correlation data, and runtime metadata needed for troubleshooting

### Requirement: Command availability follows execution-mode capability
The management surface SHALL expose core commands such as start, event delivery, cancel, terminate, and retry broadly, while durable-only commands such as pause, resume, archive, purge, or history retrieval remain explicitly mode-gated.

#### Scenario: Operator invokes a durable-only command in ephemeral mode
- **WHEN** a caller requests a command that only makes sense with durable persistence
- **THEN** the API hides or rejects that command explicitly instead of pretending the capability exists
