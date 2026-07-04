# OrcaCore — Product Requirements & Specifications

This folder is a **self-contained requirements and specification package** for the OrcaCore
workflow engine library. It consolidates the project's concept, goals, comparative research,
and every accepted design decision from the documentation tree into one coherent baseline
that can drive a **from-scratch implementation**.

It intentionally does **not** reference or depend on any existing source code. Where the
package cites other documents, those citations are provenance notes only — the normative
content is fully restated here.

## How to read this package

Requirement statements use RFC 2119 keywords:

- **SHALL / MUST** — mandatory; the implementation is non-conforming without it.
- **SHOULD** — strongly recommended; deviations need a recorded reason.
- **MAY** — optional capability.

Every requirement has a stable ID. Prefixes:

| Prefix | Area | Document |
|--------|------|----------|
| `CR-`  | Core runtime & authoring | [04-requirements-core-runtime.md](04-requirements-core-runtime.md) |
| `EV-`  | Events, waits, timers | [05-requirements-events-waits-timers.md](05-requirements-events-waits-timers.md) |
| `DU-`  | Durable execution | [06-requirements-durable-execution.md](06-requirements-durable-execution.md) |
| `SG-`  | Saga semantics | [07-requirements-saga.md](07-requirements-saga.md) |
| `CP-`  | Composition (parallel, fanout, child workflows) | [08-requirements-composition.md](08-requirements-composition.md) |
| `MG-`  | Management & operations | [09-requirements-management-operations.md](09-requirements-management-operations.md) |
| `PR-`  | Provider & extensibility model | [10-provider-model-and-extensibility.md](10-provider-model-and-extensibility.md) |
| `NF-`  | Non-functional requirements | [11-non-functional-requirements.md](11-non-functional-requirements.md) |
| `AC-`  | Acceptance criteria | [12-acceptance-criteria.md](12-acceptance-criteria.md) |
| `JS-`  | Job-scheduler driving scenario (DAG, EKS jobs) | [14-driving-scenario-eks-job-scheduler.md](14-driving-scenario-eks-job-scheduler.md) |
| `OB-`  | OpenTelemetry logs, metrics, traces & dashboard correlation | [15-requirements-observability-otel.md](15-requirements-observability-otel.md) |

## Document map

1. [01-concept-and-goals.md](01-concept-and-goals.md) — problem statement, product definition, goals, non-goals, guiding principles.
2. [02-lessons-from-prior-art.md](02-lessons-from-prior-art.md) — practices adopted from Temporal, Durable Functions, Orleans, Dapr, Elsa, MassTransit, Stateless, and Workflow Core; anti-patterns to avoid.
3. [03-domain-model-and-glossary.md](03-domain-model-and-glossary.md) — canonical terminology, the two product axes, state model, lifecycle.
4. [04-requirements-core-runtime.md](04-requirements-core-runtime.md) — authoring model, execution model, control flow, state separation, serialized execution.
5. [05-requirements-events-waits-timers.md](05-requirements-events-waits-timers.md) — event envelope, routing, matching, buffering, deduplication, wait semantics, timers.
6. [06-requirements-durable-execution.md](06-requirements-durable-execution.md) — persistence, recovery model, rehydration, versioning, inbox/outbox, retention.
7. [07-requirements-saga.md](07-requirements-saga.md) — compensation semantics, saga lifecycle, operator recovery.
8. [08-requirements-composition.md](08-requirements-composition.md) — parallel branches and joins, lightweight `ForEach`, durable child workflows.
9. [09-requirements-management-operations.md](09-requirements-management-operations.md) — fluent management surface, lifecycle events, observability, resource governance.
10. [10-provider-model-and-extensibility.md](10-provider-model-and-extensibility.md) — provider contracts, capability model, provider invariants.
11. [11-non-functional-requirements.md](11-non-functional-requirements.md) — platform, quality, API design, security, performance posture.
12. [12-acceptance-criteria.md](12-acceptance-criteria.md) — consolidated, numbered acceptance criteria catalog (`AC-xxx`; the scenario criteria `JS-AC-xxx` live in document 14 and are part of the catalog by reference).
13. [13-phasing-and-open-questions.md](13-phasing-and-open-questions.md) — recommended delivery slices and the decisions intentionally left open.
14. [14-driving-scenario-eks-job-scheduler.md](14-driving-scenario-eks-job-scheduler.md) — target application (EKS job scheduler with DAG runs): fit assessment, derived `JS-` requirements, library/application boundary.
15. [15-requirements-observability-otel.md](15-requirements-observability-otel.md) — OTel metrics and logs for system dashboards, log↔metric↔trace correlation, v3-gpt implementation gap review.

## Provenance

This package synthesizes (and supersedes for the purpose of a new implementation):

- `docs/architecture/project-foundation.md`, `design-synthesis.md`, `workflow-kinds-and-runtime-modes.md`,
  `instance-identity-and-rehydration.md`, `lifecycle-resource-management.md`, `management-command-surface.md`,
  `design-decisions-tracking.md`, `pseudo-dsl-draft.md`, `event-driven-durable-design-proposal.md`,
  `event-driven-outbox-design.md`, `child-workflow-orchestration-design-v3.md`, `monadic-primitives-design.md`
- `docs/requirements/` (regular, saga, durable — initial and advanced tracks)
- `docs/research/` (comparative research, Durable Functions, Orleans, MassTransit/Stateless, Workflow Core reviews)
- `docs/plans/` (roadmap, runtime resource governance)

Where the source documents disagreed, the newest reviewed decision wins; conflicts and their
resolutions are noted inline in the affected documents.
