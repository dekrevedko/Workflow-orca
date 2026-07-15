# 7. Saga Requirements (SG)

Scope: the saga semantic definition kind — compensation-aware orchestration. Sagas run on the
same runtime substrate (commands, events, waits, serialization) with different semantic
decision rules. Production-grade saga behavior targets durable mode; ephemeral saga is a
limited, clearly-labeled combination.

## 7.1 Semantic kind

### SG-001 Separate definition kind
Saga definitions SHALL be a distinct semantic kind with their own authoring surface — never a
boolean flag or optional compensation decoration on a regular workflow. Compensation
semantics MUST NOT leak into regular workflow APIs.

### SG-002 Forward and compensating actions
A saga SHALL model forward actions and compensating actions as related but distinct concepts.
A forward step MAY declare a compensation binding (`compensate by <handler>`). Child
workflows MAY participate as compensatable units in durable mode (see CP-035).

### SG-003 Compensation scope
A saga SHALL support compensation scopes that track successfully completed forward actions
eligible for rollback. Scoped failure handling (`Try`/`Catch`/`Finally`,
`catch failure → compensate scope`) SHALL be expressible; failure semantics are scoped, not
only global.

## 7.2 Compensation semantics

### SG-010 Deterministic compensation order
Committed forward actions SHALL become compensation-eligible immediately in their owning
scope. Failure or cancellation before merge SHALL cover every committed descendant action;
successful merge SHALL transfer eligibility to the parent scope without changing stable
identity. Sequential actions SHALL compensate in reverse committed sequence order. Actions
from sibling fibers SHALL compensate in reverse stable authored branch/instruction order,
independent of wall-clock completion. A per-scope override is allowed only when deterministic,
bound into the compiled-plan fingerprint, and based on stable authored identities. The
applied order SHALL be recorded.

### SG-011 Compensation trigger rules
Compensation SHALL run only when triggered: by a failing forward step per the saga's failure
policy, or by explicit invocation (operator or parent orchestration). Cancellation SHALL NOT
implicitly trigger compensation. Child completion SHALL NOT imply compensation.

### SG-012 Idempotent compensation
Compensating actions SHALL be documented and designed as idempotent at the contract level;
repeated compensation requests SHALL be idempotent. Retries and partial failures are
otherwise unsafe.

### SG-013 Saga terminal states
Saga lifecycle SHALL distinguish at least: `Completed` (success, no compensation),
`Failed` (failed without compensation), `Compensated` (compensation succeeded), and
`CompensationFailed` (a compensating action failed). Each is a distinct, queryable terminal
state.

### SG-014 Cancellation and timeout interaction
Saga specifications SHALL explicitly define how cancellation and step/scope timeouts affect
in-flight forward actions and whether/what compensation follows, per policy. Timeout outcomes
compose with EV-052.

## 7.3 Durable saga (production mode)

### SG-020 Durable compensation tracking
In durable mode, forward-action completions, compensation eligibility, compensation order,
and compensation outcomes SHALL be durable facts (compensation stack in runtime state +
saga compensation projection). A restart mid-saga SHALL resume without duplicating forward or
compensating actions.

### SG-021 Compensation audit trail
Every compensation decision and outcome SHALL be inspectable: operators can see forward
actions, compensating actions, order, and final outcome for any saga instance.

### SG-022 Operator intervention
Where policy allows, operators SHALL be able to perform recovery actions on
`CompensationFailed` sagas (retry compensation, manual resolution); interventions SHALL be
recorded in the audit trail.

### SG-023 Outbox consistency
Saga state changes with outbound messages SHALL flow through the unified outbox (DU-031..33)
so failures around the commit boundary cannot produce lost or duplicate external effects
beyond the documented at-least-once guarantee.

### SG-024 Version-aware sagas
Long-running saga instances SHALL be version-bound and protected against incompatible
definition changes exactly as DU-040/DU-041.

## 7.4 Ephemeral saga (limited mode)

### SG-030 Allowed but limited
Ephemeral saga SHALL be supported for in-process compensation semantics (useful for local or
short-lived orchestration needing rollback), with documented limits: no durable recovery
after crash, no durable compensation tracking, no post-restart inspection or operator
remediation. The API and docs SHALL label it a reduced-guarantee mode; it MUST NOT claim any
reliability story beyond one process lifetime.
