# Reviewer Prompt - Developer-Facing Interface Refactor After Structured Fibers

Copy the prompt below into a review agent working at the repository root.

---

You are the review agent for Workflow-orca's greenfield developer-facing API refactor.

Review only. Do not modify source, tests, samples, OpenSpec artifacts, or canonical docs. Write
your report to:

`docs/review/developer-facing-interface-post-fiber-openspec-review-2026-07-14.md`

## Objective

Determine whether the rebased OpenSpec change
`reshape-developer-facing-interfaces` is ready for implementation after
`adopt-structured-fiber-execution` was completed, promoted, and archived.

Review from the perspective of an external .NET developer who will consume OrcaCore, not only
from the perspective of an internal implementer. The repository is greenfield with no external
users or API clients. There is no backward-compatibility dimension: treat every existing path
as provisional repository code and select the best final shape without aliases or shims. Do
not describe provisional in-repository code as a compatibility contract.

## Authoritative scope

Use the repository root only:

- `src/`
- `tests/`
- `samples/`
- `docs/`
- `openspec/`
- `OrcaCore.slnx`

Do not use historical `v3-gpt` implementation paths.

Read these materials completely before reaching a verdict:

1. `docs/review/developer-facing-interface-post-fiber-review-brief-2026-07-14.md`
2. `docs/specs/17-selected-mode-capability-matrix.md`
3. `openspec/changes/reshape-developer-facing-interfaces/proposal.md`
4. `openspec/changes/reshape-developer-facing-interfaces/design.md`
5. `openspec/changes/reshape-developer-facing-interfaces/tasks.md`
6. every spec under `openspec/changes/reshape-developer-facing-interfaces/specs/`
7. `docs/review/developer-facing-interface-openspec-review-2026-07-13.md`
8. `docs/review/structured-fiber-execution-implementation-status-2026-07-13.md`
9. the proposal, design, tasks, and spec deltas under
   `openspec/changes/add-runtime-concurrency-limits/`
10. the archived design/tasks/specs under
    `openspec/changes/archive/2026-07-15-adopt-structured-fiber-execution/`

Inspect the current git diff so you review the actual rebase rather than only the resulting
files.

## Required review work

### A. Verify the rebase against current code

Validate, with source references, at least these claims:

- both delivered mode-first builders still return the same
  `WorkflowDefinition<TState>`;
- that definition publicly exposes `CompiledPlan` and still carries
  `RequiresDurableEngine` / fallback-plan behavior;
- compiled plan, instruction, scope, branch, policy, compiler-option,
  serializer-registry, and fingerprint-source types are currently public;
- the common nested `BranchBuilder<TBranchState,TResult>` exposes
  `WithPoolKey` inside durable structured authoring;
- root durable `ForEach` and root transient-pool authoring are already absent
  and compiler-defended;
- public structural `Wait`, `WaitLong`, child workflows, and root-only
  continue-as-new are already delivered;
- structural external-job and durable-lease authoring nodes are still missing;
- portable `StepResult` still contains durable-only variants;
- `ActiveWaitSnapshot` still exposes `FiberId`, `ScopeId`, and `WaitSequence`;
- the durable facade and management paths still have the application/protocol
  problems claimed by the proposal.

If any claim has drifted, identify the exact affected proposal/design/spec/task text.

### B. Review the seven principal decisions

Give an explicit approve / approve-with-change / reject disposition for each:

1. distinct immutable `EphemeralWorkflowDefinition<TState>` and
   `DurableWorkflowDefinition<TState>` types;
2. mode-specific public nested branch/scope builder families over one internal
   implementation;
3. executable compiler IR internalized while domain-facing authoring limits,
   payload serialization/copy registration, fingerprint contribution, and
   validation diagnostics remain supported;
4. static builders expose only mode-guaranteed capabilities, with per-step
   throttle as host policy, ephemeral transient pools now, durable transient
   pools only after universal durable-host enforcement, and durable leases as
   a separate persisted capability;
5. structural external-job/lease nodes land before durable-only portable
   `StepResult` variants are removed;
6. `GetStateAsync<TState>` returns only detached committed root state and
   application wait snapshots expose authored metadata instead of raw fiber
   routing identities;
7. superseded provisional authoring and dual execution fallback are deleted before package
   splitting and facade expansion.

Challenge whether each contract is deep enough, mechanically enforceable, and
clear in IntelliSense. Do not approve an abstraction solely because it reduces
the public type count.

### C. Audit consistency and completeness

Check all proposal, design, matrix, delta specs, and tasks for:

- stale language that still treats structured fibers as active prerequisite
  work;
- contradictions between exact signatures and normative requirements;
- application types that still mention compiled plan/fiber/protocol concepts;
- root versus nested capability mismatches;
- impossible host-dependent compile-time promises;
- missing state/wait behavior, error outcomes, or acceptance scenarios;
- tasks marked complete without evidence;
- tasks ordered before their dependencies or too broad for a reviewable slice;
- requirements from the prior 2026-07-13 review that were lost during rebase;
- conflicts with the active concurrency change or promoted fiber specs.

Pay special attention to whether `WorkflowAuthoringOptions` is a sufficient
application contract, whether internal/friend access to the compiled plan is a
sound cross-assembly implementation strategy, and whether separate nested
builder types are necessary or can be reduced without losing compile-time
capability absence.

### D. Validate mechanically

Run at least:

```powershell
openspec validate reshape-developer-facing-interfaces --strict
openspec validate add-runtime-concurrency-limits --strict
openspec list --json
git diff --check
```

Use focused source searches and compile/test inspection as necessary. Do not
run or change implementation merely to make the review pass.

## Severity rubric

- P0: unsafe or internally impossible design; implementation must not start.
- P1: contract contradiction, missing prerequisite, or likely major rework.
- P2: material developer-experience, correctness, tiering, or verification gap.
- P3: clarity, task sizing, traceability, or low-risk consistency improvement.

## Required report structure

1. Overall verdict and P0/P1/P2/P3 counts.
2. Executive summary focused on implementation readiness.
3. Claim-verification table with code evidence.
4. Findings ordered by severity, each with impact, recommendation, and required
   regression/acceptance evidence.
5. Seven-decision disposition table.
6. Missing requirements/scenarios and cross-change conflicts.
7. Task-graph/readiness assessment and any revised sequence.
8. Exact document-level changes required before `/opsx:apply`.
9. Commands run and validation results.

If there are no blocking findings, say explicitly that source implementation
may begin and name the first safe task slice. If changes are required, separate
blocking document changes from improvements that may proceed during
implementation.

---
