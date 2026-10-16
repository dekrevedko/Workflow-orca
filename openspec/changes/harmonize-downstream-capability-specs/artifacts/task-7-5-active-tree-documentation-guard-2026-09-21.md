# Task 7.5 active-tree documentation guard

Date: 2026-09-21

This artifact records the first recurring active-tree classification run after Tasks 7.3 and 7.4
closed the stale documentation findings from the original vocabulary sweep.

## Initial complete fixture

The initial classification source is
`openspec/changes/harmonize-downstream-capability-specs/artifacts/task-3-1-active-corpus-vocabulary-sweep-2026-08-18.md`.
Its LF-normalized SHA-256 is
`49a674d09d66d1bbc835e853b0d3368b135293106a239030c33b9e3ff65ccb2c`.

The guard reads all 23 initial stale-negative sources from that artifact and replays their bytes at
pre-reconciliation commit `89e3ed55357e849852c1a0f6fefa2124433d7e30`. The exact 56
`path:line:classifier` results are pinned below, so removing a classifier or narrowing one of its
alternatives changes the replay even when every historical file still has some other finding.

```text
HISTORICAL	CLAUDE.md	131	superseded deferred list
HISTORICAL	docs/eks-scheduler-handoff.md	134	non-buffering pre-wait delivery
HISTORICAL	docs/end-to-end-plan.md	17	unapproved Section 7B
HISTORICAL	docs/ephemeral-engine-developer-guide.md	81	legacy event client
HISTORICAL	docs/ephemeral-engine-developer-guide.md	82	two-route ingress
HISTORICAL	docs/ephemeral-engine-developer-guide.md	84	legacy event client
HISTORICAL	docs/ephemeral-engine-diagrams.md	61	legacy event client
HISTORICAL	docs/implementation/00-stack-decisions.md	62	deferred definition fanout
HISTORICAL	docs/implementation/00-stack-decisions.md	62	non-buffering pre-wait delivery
HISTORICAL	docs/implementation/01-solution-architecture.md	155	legacy event client
HISTORICAL	docs/implementation/02-engineering-conventions.md	83	non-buffering pre-wait delivery
HISTORICAL	docs/implementation/02-engineering-conventions.md	88	deferred definition fanout
HISTORICAL	docs/implementation/developer-facing-interface-refactor-phased-plan-2026-07-14.md	169	legacy event client
HISTORICAL	docs/implementation/developer-facing-interface-refactor-phased-plan-2026-07-14.md	169	non-buffering pre-wait delivery
HISTORICAL	docs/implementation/developer-facing-interface-refactor-phased-plan-2026-07-14.md	18	unapproved Section 7B
HISTORICAL	docs/implementation/developer-facing-interface-refactor-phased-plan-2026-07-14.md	239	non-buffering pre-wait delivery
HISTORICAL	docs/implementation/developer-facing-interface-refactor-phased-plan-2026-07-14.md	384	legacy event client
HISTORICAL	docs/implementation/developer-facing-interface-refactor-phased-plan-2026-07-14.md	585	unapproved Section 7B
HISTORICAL	docs/normative-source-map.md	74	superseded deferred list
HISTORICAL	docs/orleans-engine/README.md	16	non-buffering pre-wait delivery
HISTORICAL	docs/production-readiness.md	148	legacy event client
HISTORICAL	docs/production-readiness.md	21	unapproved Section 7B
HISTORICAL	docs/production-readiness.md	45	non-buffering pre-wait delivery
HISTORICAL	docs/project-technical-overview.md	24	legacy event client
HISTORICAL	docs/specs/01-concept-and-goals.md	30	non-buffering pre-wait delivery
HISTORICAL	docs/specs/01-concept-and-goals.md	94	superseded deferred list
HISTORICAL	docs/specs/03-domain-model-and-glossary.md	194	deferred definition fanout
HISTORICAL	docs/specs/03-domain-model-and-glossary.md	196	non-buffering pre-wait delivery
HISTORICAL	docs/specs/03-domain-model-and-glossary.md	224	deferred durable publish
HISTORICAL	docs/specs/03-domain-model-and-glossary.md	241	legacy event client
HISTORICAL	docs/specs/05-requirements-events-waits-timers.md	112	non-buffering pre-wait delivery
HISTORICAL	docs/specs/05-requirements-events-waits-timers.md	130	non-buffering pre-wait delivery
HISTORICAL	docs/specs/05-requirements-events-waits-timers.md	34	legacy event client
HISTORICAL	docs/specs/05-requirements-events-waits-timers.md	50	legacy event client
HISTORICAL	docs/specs/05-requirements-events-waits-timers.md	50	superseded delivery status
HISTORICAL	docs/specs/05-requirements-events-waits-timers.md	52	non-buffering pre-wait delivery
HISTORICAL	docs/specs/06-requirements-durable-execution.md	117	deferred durable publish
HISTORICAL	docs/specs/06-requirements-durable-execution.md	202	superseded delivery status
HISTORICAL	docs/specs/06-requirements-durable-execution.md	93	non-buffering pre-wait delivery
HISTORICAL	docs/specs/09-requirements-management-operations.md	59	legacy event client
HISTORICAL	docs/specs/10-provider-model-and-extensibility.md	188	legacy event client
HISTORICAL	docs/specs/10-provider-model-and-extensibility.md	192	legacy event client
HISTORICAL	docs/specs/12-acceptance-criteria.md	134	legacy event client
HISTORICAL	docs/specs/12-acceptance-criteria.md	172	non-buffering pre-wait delivery
HISTORICAL	docs/specs/12-acceptance-criteria.md	184	non-buffering pre-wait delivery
HISTORICAL	docs/specs/12-acceptance-criteria.md	185	deferred definition fanout
HISTORICAL	docs/specs/12-acceptance-criteria.md	306	superseded delivery status
HISTORICAL	docs/specs/12-acceptance-criteria.md	33	superseded delivery status
HISTORICAL	docs/specs/13-phasing-and-open-questions.md	30	non-buffering pre-wait delivery
HISTORICAL	docs/specs/13-phasing-and-open-questions.md	31	non-buffering pre-wait delivery
HISTORICAL	docs/specs/13-phasing-and-open-questions.md	32	two-route ingress
HISTORICAL	docs/specs/14-driving-scenario-eks-job-scheduler.md	117	non-buffering pre-wait delivery
HISTORICAL	docs/specs/14-driving-scenario-eks-job-scheduler.md	234	non-buffering pre-wait delivery
HISTORICAL	docs/specs/16-requirements-durable-driver.md	314	legacy event client
HISTORICAL	docs/specs/16-requirements-durable-driver.md	323	legacy event client
HISTORICAL	docs/specs/16-requirements-durable-driver.md	692	legacy event client
```

## Recurring scope

Checkpoint snapshot: 86 evolving active contract sources. This count records the reviewed run; it
is not a live cardinality invariant. The guard re-enumerates and scans the current corpus, so benign
new sources and normal archival remain valid when they introduce no finding.

- root `CLAUDE.md` and `README.md`;
- active `.md` and `.cs` documentation under `docs/`, excluding `docs/archive/` and `docs/review/`;
- every canonical `openspec/specs/*/spec.md`; and
- every non-archived OpenSpec change proposal, design, task ledger, and delta `spec.md`.

The recurring result is:

- 0 positive removed/deferred calls;
- 0 stale Section 7B claims; and
- all 23 initial stale-negative sources closed in the current tree.

The positive-call classification is the same one enforced by Task 7.1. The stale-negative
classification covers the superseded event client/status, non-buffering pre-wait delivery,
two-route ingress, deferred definition fanout, deferred durable `Publish`, and unapproved Section
7B assertions. Explicitly marked replacement/supersession sentences remain permitted as searchable
planning history.

## Additional correction

The evolving scan found one stale source outside the 2026-08-18 list:
`docs/implementation/README.md` still called Section 7B pending and non-authoritative. Task 7.5
updated it to the approved Section 7A/7B checkpoint and retained the harmonization gate before
Section 8.

## Disposition

Task 7.5 is complete when the recurring guard, this artifact, the corrected implementation index,
and the Task 7.4 Orleans reconciliation are frozen and independently reviewed together. This
artifact records no product-source authority and does not authorize Task 7.6.
