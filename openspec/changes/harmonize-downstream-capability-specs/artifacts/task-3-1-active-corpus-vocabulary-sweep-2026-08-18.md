# Task 3.1 active-corpus vocabulary sweep

Date: 2026-08-18

This artifact records the bounded task 3.1 audit. It is evidence for the later correction and
recurring-guard tasks; it does not claim that the active corpus is already coherent.

## Scope

The sweep enumerated 89 tracked Markdown and C# contract sources from:

- `CLAUDE.md` and the root `README.md`;
- active `docs/` content, excluding immutable `docs/archive/` and `docs/review/` records;
- every canonical `openspec/specs/*/spec.md`; and
- every non-archived `openspec/changes/` proposal, design, task ledger, and delta spec.

Immutable reviews and archived planning records were deliberately excluded. Their historical
vocabulary remains evidence and must not be rewritten.

## Required classification

| Classification | Exact concepts | Permitted active treatment |
|---|---|---|
| Removed | `WaitLong`; workflow-authored `Yield` | Negative absence, migration history, or immutable provenance only. No alias, tombstone, placeholder, or positive how-to call. |
| Deferred | `WhenFirst`; Saga; public generic jobs; public children; nested `Parallel`/`While`/`ForEach`; public pause/resume/archive/purge; workflow-authored `Cancel` | Future-registry or re-entry discussion only. No positive v1 call or current implementation claim. |
| Current Section 7B | durable pre-wait buffering; direct, correlation, definition-fanout, and exact-definition start-or-deliver ingress; durable workflow-authored `Publish` | Must not be described as absent, deferred, non-buffering, or dependent on broker redelivery after accepted ownership. |

## Positive removed/deferred API scan

The active scope contains zero positive public call forms matching:

- `WaitLong(`;
- `Yield(`;
- `WhenFirst(`;
- `Saga(`;
- `RunExternalJob(`;
- `RunChild(` or `RunChildren(`; and
- `.Pause(`, `.Resume(`, `.Archive(`, `.Purge(`, or `.Cancel(`.

Mentions inside future registries, negative requirements, superseded task provenance, and the
approved reshape proposal are correctly classified rather than treated as positive usage. Task
7.1 retains ownership of removing any positive guide usage found after this snapshot, and task 7.5
owns the recurring active-tree rejection.

## Stale negative treatment of current Section 7B behavior

Twenty-three active documentation sources still teach or record at least one superseded claim:
`IWorkflowEventClient`, caller-selected two-route delivery, non-consuming `NoActiveWait`, no
durable pre-wait mailbox, deferred definition fanout, deferred durable `Publish`, or a pre-approval
Section 7B prohibition.

1. `CLAUDE.md`
2. `docs/end-to-end-plan.md`
3. `docs/eks-scheduler-handoff.md`
4. `docs/ephemeral-engine-developer-guide.md`
5. `docs/ephemeral-engine-diagrams.md`
6. `docs/implementation/00-stack-decisions.md`
7. `docs/implementation/01-solution-architecture.md`
8. `docs/implementation/02-engineering-conventions.md`
9. `docs/implementation/developer-facing-interface-refactor-phased-plan-2026-07-14.md`
10. `docs/normative-source-map.md`
11. `docs/orleans-engine/README.md`
12. `docs/production-readiness.md`
13. `docs/project-technical-overview.md`
14. `docs/specs/01-concept-and-goals.md`
15. `docs/specs/03-domain-model-and-glossary.md`
16. `docs/specs/05-requirements-events-waits-timers.md`
17. `docs/specs/06-requirements-durable-execution.md`
18. `docs/specs/09-requirements-management-operations.md`
19. `docs/specs/10-provider-model-and-extensibility.md`
20. `docs/specs/12-acceptance-criteria.md`
21. `docs/specs/13-phasing-and-open-questions.md`
22. `docs/specs/14-driving-scenario-eks-job-scheduler.md`
23. `docs/specs/16-requirements-durable-driver.md`

The selected-mode matrix is not in this list. Its `Absent` cells distinguish ephemeral from durable
availability, and its negative `NoActiveWait` language prohibits silent loss; both are current.
Likewise, the canonical quality-and-verification spec correctly rejects `NoActiveWait` as durable
evidence and is not stale for that vocabulary.

Task 7.3 owns correction of 22 sources. Task 7.4 separately owns the active
`docs/orleans-engine/README.md` future-hosting note. Reshape tasks 9.6 and 9.9 remain the
source-change owners for the late Section 7B amendment and its registry/canonical reconciliation.
Task 7.5 must then enforce zero stale negative claims in the active tree.

## Canonical provenance comparison

The sweep compared every `ADDED`, `MODIFIED`, and `REMOVED` requirement block in the approved
`reshape-developer-facing-interfaces` deltas with the matching canonical requirement block.
It found 50 unmatched approved operations across 10 canonical capabilities:

| Canonical capability | Unmatched approved operations |
|---|---:|
| `developer-facing-surface` | 7 |
| `durable-persistence-and-outbox` | 5 |
| `durable-runtime` | 8 |
| `event-routing-and-waits` | 8 |
| `management-and-querying` | 1 |
| `quality-and-verification` | 5 |
| `repository-foundation` | 2 |
| `state-driven-runtime` | 2 |
| `workflow-authoring` | 3 |
| `workflow-contracts` | 9 |

Forty-two of the 50 mismatches are in the seven vocabulary-bearing capabilities
`developer-facing-surface`, `durable-persistence-and-outbox`, `durable-runtime`,
`event-routing-and-waits`, `state-driven-runtime`, `workflow-authoring`, and
`workflow-contracts`. Examples include the still-present canonical authored-`Yield` requirement,
the old `IWorkflowEventClient`/`NoActiveWait` contract, and missing durable inbox, fanout,
start-or-deliver, and publish/outbox requirements.

This comparison is diagnostic only. The harmonize change does not duplicate or synchronize
reshape-owned requirement headings. Task 5.1 must verify the approved reshape synchronization and
consume all 42 vocabulary findings; task 5.2 must account for the remaining eight mismatches as
part of its complete reshape-exit verification. Tasks 4.1 and 4.2 own the recurring
capability-wide provenance check that prevents this post-gate drift from recurring.

## Exit disposition

Task 3.1 is complete as a classified corpus sweep because it:

- proved zero positive removed/deferred public call forms in the active scope;
- separated legitimate negative/history mentions from stale negative Section 7B claims;
- enumerated every active documentation source needing correction;
- measured the approved reshape-to-canonical divergence without taking duplicate ownership; and
- assigned every unresolved class to an open task with an explicit closure obligation.

The corpus is not eligible for the final harmonization gate until tasks 4.1-4.3, 5.1-5.3,
7.1-7.5, and the final task 8 evidence are complete.
