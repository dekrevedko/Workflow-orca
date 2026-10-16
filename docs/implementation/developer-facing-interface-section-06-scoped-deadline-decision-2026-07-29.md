# Section 6 scoped deadline decision

**Date:** 2026-07-29  
**Task:** `reshape-developer-facing-interfaces` 6.12  
**Decision:** Do not add scoped branch, item, or lease workflow deadlines in v1.

## Question

Should root `CompleteWithin`, business-step `WithStepTimeout`, and structural `Wait` timeout be
generalized as one scoped deadline meet-semilattice, and should branch, item, or lease scopes gain
their own deadline authoring surface in v1?

## Analysis

The three approved deadline forms can share an internal absolute-time calculation:

- absence is the unbounded top value;
- an applicable deadline contributes an absolute instant; and
- the next scheduling boundary is the minimum of the applicable instants.

That calculation is meet-like, but the resulting expirations do not have one interchangeable
meaning:

- `CompleteWithin` is one start-relative workflow deadline. It includes admission, retries,
  delays, waits, lease queueing, and every continue-as-new generation. Its committed transition
  terminalizes the workflow as `TimedOut`, suppresses descendant merges, and may quarantine
  ambiguous protected work.
- `WithStepTimeout` bounds one business-step attempt. It fences that attempt's commit authority
  and produces `StepAttemptTimeoutException`, which is eligible for policy retry when budget and
  the workflow deadline permit.
- a structural `Wait` timeout races one event obligation and produces
  `WorkflowWaitTimeoutException` when the timer transition commits. It cancels the losing
  obligation and can become an ordered branch or item failure outcome.

Collapsing those meanings into a public generic scoped-deadline abstraction would require new
answers for terminal ownership, retry eligibility, sibling behavior, merge projection, lease
quarantine, nested precedence, and same-instant commit races. Those answers are not present in the
approved v1 contract and would expand the exact public builder companion.

## Decision

V1 keeps the existing exact surface:

- `CompleteWithin(TimeSpan)` remains root-only and appears at most once per authoring session.
- `WithStepTimeout(TimeSpan)` remains an immediate decorator of the preceding eligible business
  step.
- the approved `Wait` timeout remains local to its structural wait.
- branch, item, nested, and leased builders gain no `CompleteWithin` or generic deadline member.
- a lease request contains no duration, expiry, renewal, or deadline field.

The runtime may use the minimum applicable absolute instant to schedule work, but it must preserve
the distinct committed transition and failure semantics of each deadline owner. A workflow
terminal transition that has already won remains the ancestor terminal result and suppresses
local continuation or merge; local timeout handling cannot reset or extend the workflow deadline.

## Consequences

- Task 6.13 stores the single workflow deadline and structural wait deadline in the shared
  authoring session without widening any public builder.
- Section 6 tests must cover composition between the workflow deadline and step/wait deadlines,
  including retries, replay, continue-as-new, fan-out merge suppression, and leased ambiguity.
- Any future scoped workflow deadline requires a new amendment to the selected-mode matrix,
  exact declaration companion, terminal/race semantics, lease interaction, and verification
  catalog before implementation.
