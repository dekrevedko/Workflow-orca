# Workflow Core Issue Pattern Review

Reviewed on March 14, 2026.

## Scope and method

This document reviews public Workflow Core issues that reflect:

- semantic ambiguity
- functional correctness problems
- user-expectation mismatches
- operational behavior that affects trust in the engine

It does not claim to summarize every issue ever filed. It summarizes the issue patterns that are directly relevant to OrcaCore design and acceptance criteria, using both open and closed issues where publicly visible evidence was available.

Primary sources:

- Issues list: https://github.com/danielgerlag/workflow-core/issues
- Repository: https://github.com/danielgerlag/workflow-core
- Docs: https://workflow-core.readthedocs.io/en/latest/

## Pattern 1: Advanced branch and wait semantics are fragile

Representative issue:

- #273 Inconsistent behavior: https://github.com/danielgerlag/workflow-core/issues/273

Observed pattern:

A workflow combining `Parallel`, `WaitFor`, `Join`, and `CancelCondition` behaved inconsistently, and adding a no-op step before `WaitFor` changed whether a continuation was executed once or twice.

What this tells us:

- branch identity was not robust enough for that composition
- join behavior was sensitive to graph shape in a way users did not expect
- cancel semantics across branches were not semantically stable

Requirement for OrcaCore:

- branch execution and synchronization semantics must be explicit, deterministic, and insensitive to structurally irrelevant steps

## Pattern 2: Start semantics did not meet idempotency expectations

Representative issue:

- #828 Idempotency when creating workflows: https://github.com/danielgerlag/workflow-core/issues/828

Observed pattern:

A user expected that a client-supplied reference could behave as an idempotency key, but multiple workflows could still be started with the same reference. The request was closed as not planned.

What this tells us:

- users expect workflow start to support retry-safe initiation
- a returned workflow ID is not enough if the caller loses the response
- workflow creation is part of the correctness boundary, not just an API convenience

Requirement for OrcaCore:

- workflow start must have an explicit idempotency contract, even if optional by policy

## Pattern 3: Recovery from failure was weaker than recovery from waiting

Representative issue:

- #829 How to retry a workflow, of which one step failed?: https://github.com/danielgerlag/workflow-core/issues/829

Observed pattern:

Users could resume suspended workflows, but there was no clear built-in way to restart from a failed step. The issue was closed as not planned.

What this tells us:

- users expect long-running orchestration to support failure recovery, not only wait/resume
- the difference between suspended, failed, canceled, and terminated workflows must be operationally meaningful

Requirement for OrcaCore:

- recovery semantics must cover at least wait-resume, failure-retry, and explicit terminal states

## Pattern 4: Persistence convenience conflicted with queryability

Representative issue:

- #377 Persist workflow data in table instead of a JSON field: https://github.com/danielgerlag/workflow-core/issues/377

Observed pattern:

A user needed reporting and extraction on workflow instance data, but the SQL persistence provider stored workflow data in a JSON field, making downstream access difficult.

What this tells us:

- operators and adjacent systems expect workflow runtime data to be queryable
- treating persistence as an opaque blob reduces operational usability

Requirement for OrcaCore:

- runtime metadata, waits, state, and correlation fields must remain queryable even if business payload is serialized

## Pattern 5: Instance query and inspection expectations were higher than the API supported

Representative issue:

- #261 Get multiple workflow instances by id: https://github.com/danielgerlag/workflow-core/issues/261

Observed pattern:

A user expected efficient retrieval of multiple workflow instances by ID instead of N single-instance calls. The concern came after filtering/search moved toward Elasticsearch.

What this tells us:

- users expect inspection APIs to be first-class, not bolted on
- search/index providers should not replace basic runtime retrieval operations

Requirement for OrcaCore:

- inspection APIs must include efficient single and bulk retrieval of workflow instances and waits without depending on optional indexing infrastructure

## Pattern 6: User expectations around lifecycle events were stricter than implementation behavior

Representative issue titles from the public issue list:

- #1353 If using EndWorkflow() no event for WorkflowCompleted LifeCycleEvent
- #1398 WorkflowTermination not ending the workflow consistently
- #1402 WorkflowHost Completes Exit Before Steps Within Workload Can Attempt To Stop Gracefully

Source:

- Issues list: https://github.com/danielgerlag/workflow-core/issues

Observed pattern:

Users expected terminal operations and host shutdown to produce consistent lifecycle behavior. Public issue titles suggest mismatches around completion events, termination, and graceful shutdown.

What this tells us:

- terminal state semantics must be explicit
- lifecycle notifications are part of the contract, not secondary features
- shutdown behavior must be defined and testable

Requirement for OrcaCore:

- define exact behavior for complete, terminate, cancel, fail, and host stop
- define which lifecycle events must fire and when they are durable

## Pattern 7: Execution ordering after branching is easy to misunderstand and easy to get wrong

Representative issue title from the public issue list:

- #1357 Step execution order after Branch is completed

Source:

- Issues list: https://github.com/danielgerlag/workflow-core/issues

Observed pattern:

Users still raise questions or bugs about what executes next after branch completion.

What this tells us:

- branch completion order is not obvious to users
- a workflow engine must formalize branch scheduling and continuation ordering

Requirement for OrcaCore:

- define branch scheduling, join eligibility, and continuation order in precise terms

## Pattern 8: Queueing and persistence behavior can violate user expectations under load or restart

Representative issue titles from the public issue list:

- #1352 Workflow execution stuck in queue
- #1376 NRE in WorkflowConsumer after workflow has been completed with Redis deleteCompleted true
- #1403 NET 10: Object reference not set to an instance of an object

Source:

- Issues list: https://github.com/danielgerlag/workflow-core/issues

Observed pattern:

Operational issues continue to appear around queue processing, provider-specific persistence, deletion of completed workflows, and framework-version compatibility.

What this tells us:

- runtime/provider contracts must be strict enough to avoid hidden race conditions
- deleting completed instances can easily break in-flight consumers or lifecycle hooks
- compatibility drift creates real correctness risks

Requirement for OrcaCore:

- define provider invariants around deletion, locking, queue delivery, and resume timing
- treat compatibility and provider contract tests as part of the core acceptance suite

## Pattern 9: DSL and dynamic-definition expectations are broader than a fluent API alone

Representative issue titles from the public issue list:

- #1354 Clarification on DSL Step Input Binding and Expression Language Support
- #1375 YAML Deserializer Does Not Bind Inherited Property RunParallel for Custom Step Types for Foreach Step

Source:

- Issues list: https://github.com/danielgerlag/workflow-core/issues

Observed pattern:

Users expect serialized definitions, expression binding, and custom step metadata to behave consistently.

What this tells us:

- once a DSL exists, its semantics become part of the platform surface
- dynamic-definition support creates a second semantic contract that must match code-first behavior

Requirement for OrcaCore:

- postpone DSL support until code-first semantics are stable, or accept that DSL parity must be heavily tested

## Pattern 10: Users often expect a synchronous completion bridge even in async workflow systems

Representative issue:

- #162 Feature Request: Wait for workflow to finish: https://github.com/danielgerlag/workflow-core/issues/162

Observed pattern:

A user wanted to start a workflow inside an HTTP request and await its completion directly.

What this tells us:

- users often want a bridge between orchestration and request/response flows
- some workflows are effectively immediate and users expect a clean way to await them

Requirement for OrcaCore:

- define whether short-running workflows can be awaited directly, and how that differs from long-running durable execution

## Cross-cutting lessons

The recurring issue is not that Workflow Core lacks features. It is that feature interactions and operational semantics appear weaker than user expectations.

The strongest lessons for OrcaCore are:

1. Semantics must be explicit before features multiply.
2. Correlation, idempotency, and recovery are core behavior, not optional enhancements.
3. Queryability and inspection are product requirements, not just infrastructure concerns.
4. Terminal states and lifecycle events need precise contracts.
5. Branch, join, wait, and cancel interactions must be acceptance-tested from the start.

## Design obligations for OrcaCore

OrcaCore should explicitly define:

- idempotent or deduplicated workflow start
- legal runtime states and transitions
- wait identity and branch identity
- duplicate and out-of-order event handling
- `WhenAll` and `WhenFirst` semantics
- losing-branch cancellation semantics
- recovery after failure
- lifecycle event semantics
- provider invariants for persistence, deletion, and queueing
- inspection APIs that do not depend on optional indexing

## Source index

- Workflow Core issues list: https://github.com/danielgerlag/workflow-core/issues
- Workflow Core repository: https://github.com/danielgerlag/workflow-core
- Workflow Core docs: https://workflow-core.readthedocs.io/en/latest/
- Issue #273: https://github.com/danielgerlag/workflow-core/issues/273
- Issue #828: https://github.com/danielgerlag/workflow-core/issues/828
- Issue #829: https://github.com/danielgerlag/workflow-core/issues/829
- Issue #377: https://github.com/danielgerlag/workflow-core/issues/377
- Issue #261: https://github.com/danielgerlag/workflow-core/issues/261
- Issue #162: https://github.com/danielgerlag/workflow-core/issues/162
