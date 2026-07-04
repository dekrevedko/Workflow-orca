# OrcaCore — Comprehensive Code Review (phased full audit)

This package drives a **full audit** of an OrcaCore implementation against its authoritative
requirements. It is split into **bounded review phases run bottom-up by dependency**, so
each phase fits an agent's context window and a defect found in a lower layer explains
failures in the layers above it before you waste effort reviewing them.

- **Current target:** `v3-gpt/` (the furthest-along lineage — all providers, saga, DAG,
  hosting). To review a different lineage, swap `v3-gpt` for `v3` or `v3-cursor` everywhere;
  earlier lineages simply have fewer review phases (skip phases whose projects don't exist).
- **Deliverable of each phase:** a findings file under
  [`findings/`](findings/) named `R<n>-<area>.md`, using the finding format in §5.

## 1. Where the requirements live (the review's authority)

Two doc trees are normative. Code is judged against **both**.

| Tree | Role | Key files |
|------|------|-----------|
| [`docs/specs/`](../specs/README.md) | **WHAT** — requirements + acceptance criteria (normative) | Requirement IDs `CR/EV/DU/SG/CP/MG/PR/NF`; acceptance `AC-xxx` in [12](../specs/12-acceptance-criteria.md); job-scheduler scenario `JS-*` in [14](../specs/14-driving-scenario-eks-job-scheduler.md) |
| [`docs/implementation/`](../implementation/README.md) | **HOW** — stack, conventions, discipline (binding) | [00-stack-decisions](../implementation/00-stack-decisions.md) (banlist + resolved `IOQ-*`), [01-solution-architecture](../implementation/01-solution-architecture.md), [02-engineering-conventions](../implementation/02-engineering-conventions.md), [03-tdd-workflow](../implementation/03-tdd-workflow.md) |

Fast lookup by requirement prefix: `CR`→[04](../specs/04-requirements-core-runtime.md),
`EV`→[05](../specs/05-requirements-events-waits-timers.md),
`DU`→[06](../specs/06-requirements-durable-execution.md),
`SG`→[07](../specs/07-requirements-saga.md),
`CP`→[08](../specs/08-requirements-composition.md),
`MG`→[09](../specs/09-requirements-management-operations.md),
`PR`→[10](../specs/10-provider-model-and-extensibility.md),
`NF`→[11](../specs/11-non-functional-requirements.md).
The glossary is [03](../specs/03-domain-model-and-glossary.md); read it once before Phase R1.

## 2. What "full audit" covers

Every phase looks through all seven lenses, weighted per the phase's nature (§4):

1. **Spec conformance** — does the code satisfy the requirement IDs in scope, and are the
   in-scope acceptance criteria actually tested (not just asserted present)?
2. **Correctness & concurrency** — real bugs, races, the load-bearing invariants
   (serialized execution CR-040, exactly-once resume EV-023, no event loss EV-032,
   committed-state-only DU-020, join-once CP-002). This lens is never skipped.
3. **Architecture & pluggability** — dependency directions (01 §1/§3), provider boundary
   (PR-002), interface-first design ("program to interfaces"), no god-classes, and
   natural GoF pattern fit. Look for Strategy, Chain of Responsibility, State, Command,
   Observer, Adapter, Facade, and Template Method where they reduce coupling or improve
   locality. Do not pattern-hunt: a pattern is useful only when it makes the interface
   smaller or concentrates behavior that is currently scattered.
4. **Security** (NF-040) — deserialization safety (no untrusted polymorphic type
   resolution), no injected SQL in provider plugins, destructive-breadth guards (MG-004).
5. **Performance traps** (NF-030) — unbounded history loads, per-query payload
   deserialization, N+1 instance scans, hot-path allocations/reflection.
6. **API design** — no live-internal leakage (CR-021), snapshot-only surfaces, closed
   result hierarchies, async+CancellationToken (CR-013), durable-only features absent from
   ephemeral surfaces.
7. **Test quality & TDD discipline** — tests target public surface (03 §2), assert behavior
   not implementation, no sleeps/timing races, ACs carry `[Trait("AC",...)]`, `TimeProvider`
   not wall-clock (NF-020).

## 3. Before you start — establish the baseline

A review of code you haven't proven builds/tests is guesswork. In the **first phase (R0)**:

```
dotnet build v3-gpt/OrcaCore.slnx
dotnet test  v3-gpt/OrcaCore.slnx    # note pass/fail counts; provider tests may need Docker/Testcontainers
```

Record the result at the top of `findings/R0-foundations.md`. If the build is red, that is
finding #1 and the affected phase reviews note "reviewed against non-building code."

## 4. Review phases (run in this order)

Each phase is one agent session. Read the listed spec sections **first**, then the code.
Keep to the phase's project/file scope so the session fits context. "Primary lenses" get the
deepest scrutiny; correctness is always in scope.

| Phase | Scope (v3-gpt projects/areas) | Spec & convention refs | Primary lenses |
|-------|-------------------------------|------------------------|----------------|
| **R0 — Foundations & architecture** | Solution layout, `Directory.Build.props`/`.Packages.props`, every `.csproj` reference graph, banlist compliance across all projects; `OrcaCore.slnx` | 00 (banlist, defaults, resolved IOQ), 01, 10 (PR-001…003), NF-001/002/010 | Architecture, pluggability |
| **R1 — Abstractions/contracts** | `OrcaCore.Abstractions/**` (Ids, Steps, Events, Instances, Providers ports, Primitives, Durable command/event, Errors) | 03 glossary, 04 CR-011/015/020/021/022, 05 EV-001/002/021, 06 DU-011/012, 10 PR-010…016, PR-050 | Spec conformance, API design |
| **R2 — Core** | `OrcaCore.Core/**` (Building/builder+validation, Definitions/nodes, Lifecycle machine) | 04 CR-001…008, CR-030; 08 CP-040 (nesting validation) | Spec conformance, correctness |
| **R3 — Ephemeral engine** | `OrcaCore.Engine.Ephemeral/**` (Execution: interpreter, lane, waits, mailbox, correlation, parallel; Management; Governance; Timers) | 04 CR-010…044, 05 EV (all), 08 CP-001…013, 09 MG-001…005/010/060/061 | **Correctness/concurrency**, spec conformance |
| **R4 — Durable engine** | `OrcaCore.Engine.Durable/**` (Aggregates, Execution/command processor, Outbox, Versioning, Management) | 06 DU (all), 05 EV under durability, 09 MG-011…013/030…032/062…064 | **Correctness/concurrency**, spec conformance |
| **R5 — Providers & certification** | `OrcaCore.Providers.*` (InMemory reference first, then PostgreSql, SqlServer, RabbitMq, Redis, ZeroMq) + `tests/OrcaCore.ProviderCertification` | 10 PR-020…024, 06 DU-030…033, 00 plugin banlist | Correctness, security, pluggability |
| **R6 — Composition, DAG, saga** | Composition (`ForEach`/`RunChild(ren)`), DAG front-end + external-job composite + resource pools, saga (in Durable engine + Core) | 08 CP (all), 07 SG (all), 14 JS-* | Correctness, spec conformance |
| **R7 — Hosting & cross-cutting** | `OrcaCore.Hosting/**`; then a cross-cutting sweep: security (NF-040), performance (NF-030), test-quality meta-review across all `tests/**`, AC-trait coverage vs [12](../specs/12-acceptance-criteria.md) | 11 NF (all), 09 MG, 03 (TDD) | Security, performance, test quality |

Rationale for the order: R0 validates the skeleton the audit stands on; R1→R2→R3→R4 climb the
dependency graph (contracts → shared core → each engine) so lower-layer defects surface
first; R5 checks the plugins that implement the R1 ports; R6 covers features layered on the
engines; R7 closes with the host wiring and the whole-codebase quality/security/perf sweep.

## 5. Finding format (use in every `findings/R<n>-*.md`)

Rank findings most-severe first. One entry each:

```
### [P0|P1|P2|P3] <one-line defect> — <file path>:<line>
- **Requirement/convention:** <e.g. EV-032 / 02 §5 / NF-020>  (or "none — general")
- **Evidence:** <the specific code fact — quote the ≤5 lines that show it>
- **Failure scenario:** <concrete inputs/state → wrong output, crash, race, or data loss>
- **Recommendation:** <the smallest change that fixes it; mention a GoF pattern only when it is a natural fit>
- **Confidence:** CONFIRMED (traced the path) | PLAUSIBLE (needs author check)
```

Severity: **P0** spec-violating correctness — data loss, broken serialized-execution/
exactly-once/crash-safety invariant, security hole. **P1** functional bug or a spec
requirement in scope that is unmet/untested. **P2** architecture, maintainability, or
test-quality weakness. **P3** nit/style.

Module size rule for production implementation files: **500+ lines** is a review warning
and refactor trigger; **1000+ lines** is a hard finding unless there is an explicit,
temporary waiver. Public closed-family contract files may be excepted only when the
exception is documented and the grouping improves readability.

Rules: cite `file:line` for every finding (clickable). Prefer CONFIRMED — trace the code
path before asserting a race. A missing acceptance test for an in-scope AC is at least P1.
Do not report "code differs from how I'd write it" without a requirement or convention behind
it. End each findings file with a **coverage note**: which in-scope requirement IDs and ACs
you verified, and any you could not reach.

## 6. Kickoff prompt (one phase per session)

Swap `R3` / the phase row for the phase you're running.

```text
You are a senior .NET reviewer auditing the OrcaCore implementation in v3-gpt/.
This is a comprehensive, evidence-based code review — one phase per session.

1. Read docs/review/README.md in full (method, lenses, finding format, severity).
2. Read the spec/convention sections listed for your phase in §4's table:
   PHASE: R3 — Ephemeral engine
3. Read the code in that phase's scope only (do not wander into other projects).
   For any behavior you audit, open the matching test(s) and judge whether they truly
   verify the requirement (public-surface, behavioral, no timing races) — not just that a
   test with the right name exists.
4. Judge the code against the requirement IDs in scope through the seven audit lenses
   (README §2), weighting this phase's primary lenses. Correctness is always in scope.
5. Trace before you assert: for any concurrency/data-loss claim, follow the actual code
   path and mark it CONFIRMED; mark PLAUSIBLE only if it needs the author to confirm.
6. Write findings to docs/review/findings/R3-ephemeral-engine.md using the README §5
   format, most-severe first, with file:line on every finding and a closing coverage note.
   Do NOT modify any code — this is review only.
7. If the phase scope is larger than one context allows, stop at a natural sub-area
   boundary, note where you stopped in the findings file, and say so in your final message.
```

Model routing: run **R3 and R4 on the strongest model available** (Opus/Sonnet — these hold
the serialized-execution, event-sourcing, and crash-safety invariants and are where P0s
hide). R0/R1/R2/R5/R7 are fine on Sonnet. Haiku is not recommended for any audit phase —
tracing concurrency and spec-conformance needs the reasoning headroom.

## 7. After all phases — synthesis

When R0…R7 findings files exist, run one final synthesis session: collate all P0/P1s into a
ranked remediation list, note cross-phase patterns (e.g. one wrong invariant repeated across
engines), and record the overall verdict (ship / fix-then-ship / rework) in
`findings/SUMMARY.md`. If later comparing lineages, keep each lineage's findings under a
subfolder (`findings/v3-gpt/…`).
