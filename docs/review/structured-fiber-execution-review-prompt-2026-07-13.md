# Structured Fiber Execution Decision Review Prompt - 2026-07-13

Use this prompt to perform a complete independent re-review of the architecture,
requirements, feasibility, and implementation readiness of the corrected
`adopt-structured-fiber-execution` OpenSpec change before any refactoring begins.

```text
You are a principal .NET 10/C# workflow-engine architect reviewing OrcaCore in:

X:\Projects\GitHub\Workflow-orca

Objective:
Determine whether the corrected `adopt-structured-fiber-execution` decision
package is coherent, complete, implementable, testable, and ready to govern a
breaking refactor of the current cursor-based durable driver. Independently
verify the claimed dispositions of F-01 through F-13 in the prior review.

This is a review-only task. Work through the entire package and current root
implementation, write a durable findings report, and return an explicit
Approve or Request Changes verdict. Do not implement the refactor and do not
edit the decision package during review.

Repository state and assumptions:
- The active implementation has been promoted to the repository root.
- Root `OrcaCore.slnx`, `src/`, `tests/`, `samples/`, `benchmarks/`, and `docs/`
  are authoritative.
- Do not inspect or propose new work under the removed `v3-gpt` subtree.
- OrcaCore is in active development with no external library consumers and no
  production durable instances.
- Backward compatibility with the provisional branch Interface, cursor
  envelopes, or development-store data is not required.
- Durable format versioning, replay safety, and definition binding are still
  required as properties of the final library.
- The worktree may contain large promotion and implementation changes. Preserve
  every unrelated change. This review must be read-only except for its report.
- `openspec/` is locally excluded from normal Git status, but its files are the
  primary review subject and must still be read directly.
- The prior report is evidence and review history, not authority for the new
  verdict. Do not inherit either its findings or the remediation claims without
  checking the current artifacts and root implementation.

Primary decision package:
1. openspec/changes/archive/2026-07-15-adopt-structured-fiber-execution/proposal.md
2. openspec/changes/archive/2026-07-15-adopt-structured-fiber-execution/design.md
3. openspec/changes/archive/2026-07-15-adopt-structured-fiber-execution/tasks.md
4. Every file under:
   openspec/changes/archive/2026-07-15-adopt-structured-fiber-execution/specs/**/spec.md
5. Prior review and remediation mapping:
   docs/review/structured-fiber-execution-review-2026-07-13.md

The package currently contains these capability deltas:
- structured-fiber-execution
- workflow-authoring
- workflow-contracts
- state-driven-runtime
- durable-runtime
- event-routing-and-waits
- saga-orchestration
- runtime-resource-governance
- quality-and-verification

Required comparison documents:
- docs/specs/03-domain-model-and-glossary.md
- docs/specs/04-requirements-core-runtime.md
- docs/specs/05-requirements-events-waits-timers.md
- docs/specs/06-requirements-durable-execution.md
- docs/specs/07-requirements-saga.md
- docs/specs/08-requirements-composition.md
- docs/specs/12-acceptance-criteria.md
- docs/specs/13-phasing-and-open-questions.md
- docs/specs/16-requirements-durable-driver.md
- openspec/changes/add-runtime-concurrency-limits/proposal.md
- openspec/changes/add-runtime-concurrency-limits/design.md
- openspec/changes/add-runtime-concurrency-limits/tasks.md
- Existing baseline specs under openspec/specs/ for every modified capability.

Required implementation inspection:
- src/OrcaCore.Core/Building/WorkflowBuilder.cs
- src/OrcaCore.Core/Definitions/Nodes.cs
- src/OrcaCore.Abstractions/Steps/StepContext.cs
- src/OrcaCore.Abstractions/Steps/StepResult.cs
- src/OrcaCore.Abstractions/Durable/DurableExecutionEnvelope.cs
- src/OrcaCore.Engine.Ephemeral/Execution/SequenceExecutionContext.cs
- src/OrcaCore.Engine.Ephemeral/Execution/ParallelNodeRunner.cs
- src/OrcaCore.Engine.Ephemeral/Execution/WhenFirstNodeRunner.cs
- src/OrcaCore.Engine.Ephemeral/Execution/ForEachNodeRunner.cs
- src/OrcaCore.Engine.Ephemeral/Execution/ForEachWorkScheduler.cs
- src/OrcaCore.Engine.Durable/Driver/DurableDriverCatalog.cs
- src/OrcaCore.Engine.Durable/Driver/DurableDriverSegmentRun.cs
- src/OrcaCore.Engine.Durable/Driver/DurableDriverSegmentRun.Steps.cs
- src/OrcaCore.Engine.Durable/Driver/DurableDriverSegmentRun.Children.cs
- src/OrcaCore.Engine.Durable/Driver/DurableWorkflowDriver.cs
- src/OrcaCore.Engine.Durable/Definitions/DurableDefinitionRegistry.cs
- src/OrcaCore.Engine.Durable/Execution/DurableWorkflowRuntime.cs
- The aggregate, wait, timer, child, job, resource, checkpoint, commit,
  continuation, outbox, and provider code reached from those files.

Required test inspection:
- tests/OrcaCore.Core.Tests/Building/WorkflowBuilderTests.cs
- tests/OrcaCore.Acceptance.Tests/ParallelAcceptanceTests.cs
- Ephemeral `Parallel`, `WhenFirst`, `ForEach`, wait, yield, and saga tests.
- Durable driver acceptance, reviewed regression, host, recovery, versioning,
  wait, child, resource, external-job, and saga tests.
- Provider certification plus PostgreSQL and SQL Server integration tests that
  currently round-trip runtime state or replace a host.

Required first steps:
1. Inspect `git status --short`; do not clean, revert, stage, or rewrite existing
   work.
2. Read the proposal, design, all nine spec deltas, and tasks in full.
3. Run:
   openspec status --change adopt-structured-fiber-execution --json
   openspec validate adopt-structured-fiber-execution --type change --strict --no-interactive
4. Read the listed canonical requirements and the active concurrency-limits
   change, noting direct contradictions and precedence questions.
5. Trace the present root implementation and representative tests before
   judging whether a proposed replacement or reusable Seam actually exists.

Review method:
- Lead with findings, ordered by severity.
- Verify claims against current files; do not rely on chat history or the
  decision documents' description of current behavior.
- Cite a clickable repository-relative `file:line` for every finding.
- Mark factual defects CONFIRMED only after tracing the relevant code and spec.
- Mark a concern PLAUSIBLE when a concrete implementation detail remains
  undecided and explain exactly what evidence or decision would resolve it.
- Distinguish product semantics, compiled-plan design, durable persistence,
  implementation sequencing, and test-enablement gaps.
- Do not report stylistic preferences without a correctness, maintainability,
  operability, or requirements consequence.
- Do not stop after the first blocking issue. Complete every review lens and
  synthesize cross-cutting conflicts.
- For F-01 through F-13, record CLOSED, PARTIALLY CLOSED, or OPEN with direct
  artifact evidence. A wording change is insufficient when the requirement,
  scenario, TDD task order, or cross-document precedence remains inconsistent.
- Reviewers may delegate independent read-only slices, but the final report
  must reconcile all results into one non-duplicated verdict.

Severity:
- P0 Critical: the decision can cause state corruption, duplicate effects,
  stranded instances, invalid replay, unsafe cancellation, or fundamentally
  unimplementable contracts.
- P1 High: an unresolved semantic contradiction, missing invariant, or missing
  task/test gate blocks safe implementation.
- P2 Medium: material maintainability, operability, performance, validation, or
  verification weakness that should be resolved before completion.
- P3 Low: clarity, terminology, traceability, or minor task-ordering issue that
  does not block the architecture.

Review lenses and mandatory questions:

1. Workflow structure and outcomes
- Does exactly one root `Init` and one root `End` represent every successful
  workflow shape without conflating business outcomes with lifecycle status?
- Are failure, cancellation, termination, poison, and parking exits specified
  without requiring root `End`?
- Is `BranchReturn<TResult>` sufficient and unambiguous for nested branches?
- Are root-only `ContinueAsNew` and branch terminal restrictions complete?

2. Authoring Interface and compiled representation
- Is rejecting public `EndIf`, `EndWhile`, and `EndParallel` the right choice for
  nested fluent builders?
- Do compiler-generated `IfJoin`, `LoopBack`, `LoopExit`, `ScopeJoin`, and
  `ScopeExit` have distinct, necessary semantics and stable identities?
- Is it clear which fiber executes each structural instruction, especially
  `ScopeJoin` and `ScopeExit` while the parent is blocked?
- Can all conditionals, loops, waits, nested scopes, and successful exits lower
  to the proposed single-position fiber model?
- Are reachability and statements-after-terminal rules fully specified?

3. Branch state and C# type feasibility
- Can branches have different private state types while one scope exposes a
  common `TResult` without creating an unusable or reflection-heavy builder?
- Is branch input materialization genuinely deep and isolated in both engines?
- Is the proposed common result record/union approach practical for normal
  workflow authoring?
- Is ordinary `StepResult` correctly kept separate from business results?
- Is serializer and schema validation early enough to prevent runtime parking?
- Does ephemeral `ForEach` use isolated item state and ordered item outcomes
  through the same scheduler, and is durable rejection explicit and early?

4. Merge semantics
- Is the replacement-state Merge Interface implementable with current mutable
  class-based `TState` contracts?
- Can the runtime prevent partial parent mutation when Merge throws?
- Are purity, determinism, serialization-before-commit, retries, and diagnostics
  specified strongly enough?
- Does crash-before/after-Merge produce one logical Merge and never rerun branch
  bodies?
- Are authored branch order and result identity sufficient for deterministic
  Merge across replay and future true parallel execution?

5. Scheduling, Yield, and fairness
- Is one user-step invocation plus bounded internal instructions a precise and
  practical quantum?
- Does persisted round-robin state guarantee bounded sibling progress after
  segment limits, waits, resumed events, new nested fibers, retries, crashes,
  and duplicate continuation claims?
- Does `Yield` end a fiber quantum, release the durable turn when appropriate,
  and avoid immediate first-fiber reselection?
- Is scheduler order observable business semantics or only a fairness rule?
- Are command, elapsed-time, and active-fiber budgets composed without hidden
  starvation?

6. Scope lifecycle and join behavior
- Are parent preservation, scope phases, child creation, join, cleanup, Merge,
  and parent resume valid as atomic transitions?
- Is `WhenAll` fail-fast behavior deterministic enough when multiple failures
  race, and is the selected/aggregated failure contract defined?
- Is `WhenFirst` "first committed terminal branch" correct for successful and
  failed outcomes, retries, same-commit ties, and externally resumed fibers?
- Does removal of `Ignore` and `LetRemainingComplete` eliminate all detached
  local work, including `ForEach.WhenAny`, or do other nodes still create an
  equivalent orphan behavior?
- Is rejecting every shape with an unsupported induced cancellation set before
  registration both sufficient and feasible across all joins and root control?

7. Status, ownership, and cleanup
- Is `Running` while any fiber is runnable and `Waiting` only when all remaining
  fibers are blocked consistent with current lifecycle and wait requirements?
- Does every wait, timer, resume token, child group, job, resource ticket, retry,
  and cancellation record have a clear owner and release transition?
- Is post-order scope cleanup correct for failure, cancellation, termination,
  ContinueAsNew, Merge failure, and `WhenFirst` loser selection?
- Are late events, completions, grants, and duplicate deliveries handled after
  an owner is cancelled or removed?

8. Durable persistence and multi-host replay
- Does envelope format 2 contain every field required to resume fibers, scopes,
  Merge, scheduler position, ownership, retries, and diagnostics exactly?
- Are plan fingerprint, definition version, compiler format, and envelope format
  binding rules atomic and operationally diagnosable?
- Is atomic registration feasible with the current catalog/registry split?
- Can every proposed scope transition map to the existing one-command/one-commit
  aggregate and provider transaction model?
- Are optimistic-conflict, lost-response, duplicate-claim, and continuation
  retention cases covered?
- Given no clients or production data, is rejecting format-1 data and deleting
  the legacy executor a complete and safe development migration policy?

9. Module design and maintainability
- Are `DefinitionCompiler`, `LinearFiberInterpreter`, `FiberScheduler`,
  `ScopeReducer`, and execution-state Adapters deep Modules with clear
  responsibilities, or are any artificial wrappers around the same state?
- Is shared Core logic free from durable/provider dependencies?
- Is enough behavior shared to prevent ephemeral/durable semantic drift without
  forcing persistence logic into Core?
- Are the existing host, command processor, aggregate, outbox, child dispatch,
  and provider Seams genuinely reusable under the new model?
- Does the design actually eliminate frame-prefix ownership and
  `MergeCompletedCursors`-style candidate selection?

10. Saga, DAG, and resource governance
- Is canonical authored compensation order correct for sibling saga actions, or
  does it conflict with established reverse-completion semantics?
- Is the compensation eligibility/commit point relative to scope Merge defined?
- Does child-instance DAG execution fit fiber/scope ownership without needing a
  second local DAG scheduler?
- Do named resource waits release the fiber quantum and preserve fair scheduling?
- Does the design reconcile all concurrency wording in
  `add-runtime-concurrency-limits`?

11. Performance and limits
- Are branch snapshots, private state, results, and recursive scopes likely to
  create unacceptable envelope growth?
- Are Depth, active-fiber, result-size, and envelope-size limits required at
  build time, registration, runtime, or all three?
- Do one-step quanta create excessive commits or continuation churn?
- Are benchmarks and operational metrics sufficient to detect regression before
  release?

12. Requirements, tasks, and TDD traceability
- Does every design decision have a normative requirement and at least one
  executable scenario?
- Does every requirement map to an ordered implementation task and intended test
  project?
- Are all production-code tasks preceded by a failing behavior, characterization,
  crash, provider, or repository-guard test?
- Are task dependencies correct, especially compiler before runtime, shared model
  before adapters, ownership before residual composition, and fibers before
  saga/DAG?
- Do the final gates prove parity, replay, fairness, cleanup, and provider behavior
  rather than only compilation?

Required report:
Write the completed re-review to:

docs/review/structured-fiber-execution-rereview-2026-07-13.md

Do not modify proposal.md, design.md, tasks.md, any spec delta, source code, or
tests during this review.

Report format:

# Structured Fiber Execution Decision Re-Review - 2026-07-13

## Verdict
Use exactly one:
- APPROVE
- REQUEST CHANGES

State whether implementation may begin. APPROVE requires no unresolved P0 or P1
finding and no ambiguous blocking decision.

## Findings
List findings first, ordered P0 through P3. For each finding include:
- Finding ID and severity.
- CONFIRMED or PLAUSIBLE.
- Exact decision/spec/task and current code/test evidence with `file:line`.
- Why it matters for correctness or implementation.
- The exact required decision or document change.
- The requirement/scenario/test/task that must be added or modified.

If no findings exist, say so explicitly and identify residual risks.

## Prior Finding Disposition
For each F-01 through F-13 from the prior report, record CLOSED, PARTIALLY
CLOSED, or OPEN with the exact current artifact evidence and any remaining
required correction.

## Decision Checklist
For every checkbox in `design.md` Review Checklist, record:
- ACCEPT
- REJECT
- ACCEPT WITH CHANGE

Provide one concise reason and link every changed decision to a finding.

## Traceability Matrix
For each of the 15 design decisions, map:
- design section;
- normative requirement(s);
- implementation task(s);
- intended test project(s);
- current Implementation areas replaced or retained.

Mark every missing or weak mapping.

## Current Implementation Gap Map
Separate:
- reusable Seams;
- code that must be replaced;
- current defects that should be fixed independently before refactor;
- proposed behavior with no current enabling contract.

## Verification Assessment
Record:
- OpenSpec status and strict validation result;
- relative-link or structural validation performed;
- code and tests inspected;
- commands run and their results;
- anything not verified and why.

## Required Changes Before Apply
Provide one ordered checklist of document-only corrections required before
`/opsx:apply`. Do not include implementation tasks already correctly captured in
tasks.md unless the task itself is missing or incorrectly ordered.

## Accepted Risks
List risks that are deliberately accepted because OrcaCore has no consumers and
is in active development, separately from risks that remain intrinsic to a
durable engine.

Completion criteria:
- Every decision-package file was read in full.
- All nine capability deltas were reviewed against their baseline specs.
- All 15 design decisions and every Review Checklist item were assessed.
- The promoted root code and representative tests were traced.
- Canonical spec and concurrency-change conflicts were enumerated.
- Requirements-to-task-to-test traceability was completed.
- The durable re-review report was written at the required path without
  overwriting the original report.
- Final verdict is explicit and justified.
- No implementation or decision document was modified.

Stop conditions:
- Do not stop at a plan or partial findings list.
- Do not ask the user to restate decisions already present in the package.
- Continue despite unrelated dirty worktree files.
- Stop only after the complete report is written, unless required files are
  genuinely missing or unreadable; if blocked, identify exact missing paths and
  the review sections that could not be completed.

Final response:
- Lead with APPROVE or REQUEST CHANGES.
- Summarize P0/P1 counts and the most important decision issue.
- Link the written review report.
- State whether `/opsx:apply` may begin.
- Do not claim implementation readiness while any P0/P1 or blocking ambiguity
  remains.
```
