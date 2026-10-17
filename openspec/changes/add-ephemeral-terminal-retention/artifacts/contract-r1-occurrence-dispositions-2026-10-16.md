# R1 expanded occurrence dispositions — 2026-10-16

This supplements the rejected initial keyword sweep; it does not rewrite that dated request. Scope: every `.md`/`.cs` under active `docs/` (excluding `docs/archive/` and immutable `docs/review/`) plus every canonical capability spec. Pattern: `retention|idempotency|not.found|evict|archive|purge|WaitForOutputAsync|EphemeralEngineHostOptions|HostOptions|StartOrGetAsync|lifetime|memory`, case-insensitive. The adjacent TSV maps every matched line to its file, exact line, nearest heading, owning-rule/cross-capability/acceptance classification and transition disposition. No match is unclassified. No normative document is edited in this contract target.

## Explicit resolutions of reviewer P3-2 through P3-5

- **repository-foundation, Package topology and ownership are exact:** this is a cross-capability dependency, not a no-hit capability. Its table reference must resolve to doc 17's ephemeral assembly/namespace row including the new policy. Existing capability block remains unchanged.
- **runtime-resource-governance and doc 17 distinct limits:** automatic settled-terminal retention is a retained-data count, never a host-wide running-instance/advancement ceiling, logical path limit, physical throttle/pool capacity, or DAG node budget. The existing prohibition and host-option placements stand.
- **doc 17 §17.4 and namespace/assembly row:** task 2.1/2.2 carries full host-option shape and policy signature text under `OrcaCore.Hosting` in `OrcaCore.Engine.Ephemeral`. The authoring declaration companion declares builders but no host options, so it is explicitly excluded and stays byte-exact; adding the policy alone there would be incomplete.
- **MG-020:** describe successful automatic terminal retirement as best-effort internal `evicted` observation after actual removal. Keep public archive/delete events and destructive commands deferred. This grants no public lifecycle API.
- **MG-050/051/053 and doc 03 terminal-state text:** task 2.1 qualifies safe cold activation eviction, active-memory residency caps, quick terminal removal and continued durable inspectability as durable behavior. Accepted ephemeral units retain for host lifetime by default, optionally retire only settled terminals, and never rehydrate an evicted ephemeral payload. Aborted unaccepted provisional units instead terminally fence and clean up after settlement regardless of policy.
- **NF-031:** qualify active-instance caps as durable residency policy, consistent with MG-051; remove any reading as an independent ephemeral host-wide admission cap. Classify terminal-retention count separately; active/stuck/pinned work and caller-held payloads are beyond its bound.
- **Steady-state delta wording:** the policy requirement now states the resulting members and null-default lifetime, rather than instructions to add them or an ambiguous current-version promise.
- **Event router accepted map:** name `(InstanceId, EventId)` deduplication ownership explicitly in design, delta and test tasks; clear all retired/aborted-unit entries with other per-instance data.
- **Very small SweepInterval:** retain accepted positive-only validation, including one tick; explicitly document potential continuously busy sweeps/CPU overhead and operational sizing. No new floor or silent clamp is introduced.
- **Pins:** pin admission and retirement linearize. Pin-first means ineligible; retire-first rejects new pins. Eligible retirement therefore has no readers left to drain.
- **Permanent post-gate records:** required for the two capabilities at 2.2 after actual 1.3 approval/1.4 checkpoint evidence. Before approval this is a new ADDED-only proposal with pending canonical operations, not an approved successor; no current post-gate fixture row or separate process-amendment target is required or fabricated. Current contract provenance is refrozen in this target.

## Expanded sweep inventory

499 matched lines in 54 files; record 128197 UTF-8 bytes / SHA-256 `6d19956655c8951c72517204b460437916e5172d135b56bb45f91418491c5e4b`. Recipe: ordinal tab-delimited rows, LF and one final LF. The record has no header; columns are `path`, `line`, `nearest heading`, `classification`, `disposition`.

| File | Hits | Classification |
|---|---:|---|
| `docs/active-implementation-index.md` | 1 | CrossCapabilityDependency |
| `docs/durable-development-store-reset.md` | 1 | CrossCapabilityDependency |
| `docs/eks-scheduler-handoff.md` | 10 | CrossCapabilityDependency |
| `docs/end-to-end-plan.md` | 9 | CrossCapabilityDependency |
| `docs/ephemeral-engine-developer-guide.md` | 6 | CrossCapabilityDependency |
| `docs/ephemeral-engine-diagrams.md` | 4 | CrossCapabilityDependency |
| `docs/github-publishing-status.md` | 2 | CrossCapabilityDependency |
| `docs/implementation/00-stack-decisions.md` | 7 | CrossCapabilityDependency |
| `docs/implementation/01-solution-architecture.md` | 12 | CrossCapabilityDependency |
| `docs/implementation/02-engineering-conventions.md` | 7 | CrossCapabilityDependency |
| `docs/implementation/03-tdd-workflow.md` | 3 | CrossCapabilityDependency |
| `docs/implementation/04-task-protocol.md` | 1 | CrossCapabilityDependency |
| `docs/implementation/developer-facing-interface-refactor-phased-plan-2026-07-14.md` | 13 | CrossCapabilityDependency |
| `docs/implementation/KICKOFF.md` | 2 | CrossCapabilityDependency |
| `docs/implementation/README.md` | 7 | CrossCapabilityDependency |
| `docs/normative-source-map.md` | 17 | CrossCapabilityDependency |
| `docs/observability/resource-governance-dashboard.md` | 3 | CrossCapabilityDependency |
| `docs/orleans-engine/README.md` | 2 | CrossCapabilityDependency |
| `docs/packages.md` | 1 | CrossCapabilityDependency |
| `docs/prerelease-notes.md` | 2 | CrossCapabilityDependency |
| `docs/production-readiness.md` | 16 | CrossCapabilityDependency |
| `docs/project-technical-overview.md` | 9 | CrossCapabilityDependency |
| `docs/README.md` | 8 | CrossCapabilityDependency |
| `docs/specs/01-concept-and-goals.md` | 9 | CrossCapabilityDependency |
| `docs/specs/02-lessons-from-prior-art.md` | 9 | CrossCapabilityDependency |
| `docs/specs/03-domain-model-and-glossary.md` | 19 | CrossCapabilityDependency |
| `docs/specs/04-requirements-core-runtime.md` | 4 | CrossCapabilityDependency |
| `docs/specs/05-requirements-events-waits-timers.md` | 3 | CrossCapabilityDependency |
| `docs/specs/06-requirements-durable-execution.md` | 18 | CrossCapabilityDependency |
| `docs/specs/07-requirements-saga.md` | 1 | CrossCapabilityDependency |
| `docs/specs/08-requirements-composition.md` | 1 | CrossCapabilityDependency |
| `docs/specs/09-requirements-management-operations.md` | 33 | OwningRule |
| `docs/specs/10-provider-model-and-extensibility.md` | 17 | CrossCapabilityDependency |
| `docs/specs/11-non-functional-requirements.md` | 1 | CrossCapabilityDependency |
| `docs/specs/12-acceptance-criteria.md` | 16 | AcceptanceReference |
| `docs/specs/13-phasing-and-open-questions.md` | 9 | CrossCapabilityDependency |
| `docs/specs/14-driving-scenario-eks-job-scheduler.md` | 6 | CrossCapabilityDependency |
| `docs/specs/15-requirements-observability-otel.md` | 1 | CrossCapabilityDependency |
| `docs/specs/16-requirements-durable-driver.md` | 12 | CrossCapabilityDependency |
| `docs/specs/17-public-authoring-contract.cs` | 3 | OwningRule |
| `docs/specs/17-selected-mode-capability-matrix.md` | 85 | OwningRule |
| `docs/specs/README.md` | 5 | CrossCapabilityDependency |
| `openspec/specs/developer-facing-surface/spec.md` | 13 | OwningRule |
| `openspec/specs/durable-persistence-and-outbox/spec.md` | 18 | CrossCapabilityDependency |
| `openspec/specs/durable-runtime/spec.md` | 16 | CrossCapabilityDependency |
| `openspec/specs/event-routing-and-waits/spec.md` | 5 | CrossCapabilityDependency |
| `openspec/specs/management-and-querying/spec.md` | 10 | OwningRule |
| `openspec/specs/quality-and-verification/spec.md` | 8 | AcceptanceReference |
| `openspec/specs/repository-foundation/spec.md` | 4 | CrossCapabilityDependency |
| `openspec/specs/runtime-resource-governance/spec.md` | 10 | CrossCapabilityDependency |
| `openspec/specs/state-driven-runtime/spec.md` | 2 | CrossCapabilityDependency |
| `openspec/specs/structured-fiber-execution/spec.md` | 1 | CrossCapabilityDependency |
| `openspec/specs/workflow-authoring/spec.md` | 3 | CrossCapabilityDependency |
| `openspec/specs/workflow-contracts/spec.md` | 14 | CrossCapabilityDependency |
