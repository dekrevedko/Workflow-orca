# Task 6.1 authoring-session lifecycle requirement

Date: 2026-08-22

## Result

Task 6.1 adds `CR-009a Authoring sessions have one explicit lifecycle` to
`docs/specs/04-requirements-core-runtime.md`. The numbered requirement mirrors the approved
canonical `workflow-authoring` requirement `Authoring handles are phase-bound and definitions are
frozen`; it does not introduce new runtime or public-surface semantics.

## Required lifecycle

| Contract dimension | Numbered requirement | Existing executable evidence |
|---|---|---|
| Session states | exactly `Open`, `JoinPending`, `Frozen` | `AuthoringLifecycleSession` and `AuthoringLifecycleTests` |
| Handle validity | session, epoch, and lexical-scope token | stale root and callback-expiry cases |
| Root fan-out | `Open` to `JoinPending`, one selected join, successor epoch | ephemeral and durable successor-facade cases |
| Root terminal | atomic terminal commit, immutable snapshot, `Frozen` | `End` and `ContinueAsNew` stability cases |
| Governed rejection | no graph mutation for stale/superseded, duplicate join, frozen, expired, or losing concurrent operations | lifecycle diagnostics `SFE-AUTH-LIFECYCLE-001` through `-005` |

`AuthoringLifecycleTests` is bound to `CR-009a` through its requirement trait. The former `AC-021`
trait was incorrect because canonical `AC-021` is the lambda-mode-safety criterion; Task 6.3 owns
the new lifecycle acceptance criterion and its bidirectional mapping.

## Executable guard

`Task61_CoreRuntimeDocumentsAuthoringSessionLifecycleAndExecutableEvidence` requires:

- one exact `CR-009a` heading;
- all three lifecycle states and all five stable diagnostic codes;
- the session/epoch/lexical-scope, root fan-out, frozen-snapshot, and mutation-free clauses;
- the matching canonical OpenSpec requirement; and
- the requirement trait plus representative stale-root, duplicate-join, frozen-snapshot,
  callback-expiry, and concurrent-race evidence members.

Changing the numbered heading from `CR-009a` to `CR-009b` makes the guard fail on the missing stable
ID. The mutation was restored before validation.

## Scope boundaries

- No `src/**` file changes.
- No public API or package-baseline change.
- Task 6.3 still owns the acceptance criterion.
- Task 6.5 still owns the explicit decision about the unchanged public-authoring-contract sketch.
