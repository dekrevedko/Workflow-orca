# 7. Deferred Saga Design Constraints (SG)

Status: **deferred beyond the first release**. No saga builder, definition, status, adapter,
management member, command, compile fixture, alias, or placeholder ships in v1. This document
keeps the compensation design visible for a future explicit amendment; its `SHALL` statements
constrain that future amendment and are not first-release conformance requirements or license
to predeclare public signatures.

A future saga is a compensation-aware semantic definition kind over the same workflow runtime
substrate with different failure/recovery rules. It must be justified independently rather
than added as a boolean flag or partial set of compensation decorators.

## 7.1 Semantic kind

### SG-001 Separate future definition kind
A future saga definition SHALL be a distinct semantic kind with its own authoring surface —
never a boolean flag or optional compensation decoration on a workflow. Compensation semantics
MUST NOT leak into first-release workflow APIs.

### SG-002 Forward and compensating actions
A saga SHALL model forward actions and compensating actions as related but distinct concepts.
A forward step MAY declare a compensation binding (`compensate by <handler>`). Child
workflows MAY participate only if a future saga amendment also approves a public typed child
contract; the v1 DAG-internal child protocol (CP-023) is not such a contract.

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
independent of wall-clock completion. A per-scope override is allowed only when deterministic
and based on stable authored identities. Its structural option/value is fingerprinted; changing
opaque ordering code requires a new `DefinitionVersion`. The applied order SHALL be recorded.

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

## 7.3 Future durable saga

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

## 7.4 Future ephemeral saga (optional limited mode)

### SG-030 Allowed but limited
If a future amendment includes ephemeral saga, it SHALL support only in-process compensation
semantics (useful for local or short-lived orchestration needing rollback), with documented
limits: no durable recovery
after crash, no durable compensation tracking, no post-restart inspection or operator
remediation. The API and docs SHALL label it a reduced-guarantee mode; it MUST NOT claim any
reliability story beyond one process lifetime.

## 7.5 Required amendment gate

Before any saga surface is implemented, document 17 and the coordinated OpenSpec change SHALL
approve the typed action/result API, scope and nesting rules, durable reverse progression,
timeout/cancellation behavior, failure and operator-remediation model, restart evidence, and
package impact together. Until that gate passes, SG acceptance scenarios remain a future test
catalog and SHALL NOT be counted toward first-release readiness.
