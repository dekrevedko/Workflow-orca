# Independent review prompt: proposed OrcaCore public API surface (2026-07-16)

You are an independent senior API reviewer. Perform a full design review of the **proposed
public developer-facing API surface** of OrcaCore, a .NET 10 workflow engine with two runtime
modes (ephemeral in-process and durable event-sourced) behind one authoring vocabulary.

This is a **contract/design review of the normative baseline**, not a code review. The
production source does not yet implement most of the proposed surface; where source exists it
is provisional. The repository is greenfield: there are no external consumers, no compatibility
constraints, and no migration obligations. Judge the proposed surface on its merits.

## Review goal

Evaluate the proposed surface against exactly this goal:

> A consistent, comprehensive, developer-oriented API that is useful and convenient, and — as
> far as feasible — resists workflow configuration errors: unsupported, impossible, or
> obviously wrong API call chains should be unrepresentable at compile time, or rejected at
> the nearest seam with a stable diagnostic.

Weigh four dimensions independently: **consistency** (one vocabulary, one shape per concept,
no surprises between analogous members), **comprehensiveness** (every supported journey is
expressible; gaps are named, not silent), **developer orientation** (discoverability,
ergonomics, error quality, no implementation vocabulary), and **misuse resistance** (wrong
chains impossible or rejected early; defaults safe; identities and matching values typed).

## Independence requirements

- Prior reviews exist (three Phase 0 guard review rounds, two API analyses, and an applied
  lease-contract amendment). **Do not adopt their conclusions.** You may read them for
  context, but every finding you report must be re-derived from the normative documents and
  your own analysis. If you disagree with a previously accepted decision, say so with
  reasoning — previously accepted is not correct by definition.
- Verify claims against files, not reports. Quote exact text with file path and line evidence.
- Do not modify any file. Produce a review document only.

## Normative sources (authoritative, in precedence order)

1. `docs/specs/17-selected-mode-capability-matrix.md` — the single approved capability
   matrix, public signature baseline (§17.2 including the durable resource-lease contract),
   diagnostic contract (§17.3), concurrency lifetimes (§17.4), package/host boundary (§17.5),
   and projection ownership (§17.6).
2. Canonical requirements: `docs/specs/03-domain-model-and-glossary.md`,
   `04-requirements-core-runtime.md`, `05-requirements-events-waits-timers.md`,
   `06-requirements-durable-execution.md` (note DU-053 idempotent start and DU-054
   registration), `08-requirements-composition.md`,
   `09-requirements-management-operations.md` (note MG-062/MG-064 durable pools, quarantine,
   operator recovery), `10-provider-model-and-extensibility.md`, `12-acceptance-criteria.md`,
   `13-phasing-and-open-questions.md`, `14-driving-scenario-eks-job-scheduler.md`,
   `15-requirements-observability-otel.md`, `16-requirements-durable-driver.md`.
3. OpenSpec change `openspec/changes/reshape-developer-facing-interfaces/` — `proposal.md`,
   `design.md` (Decisions 1–12), `tasks.md`, and all delta specs under `specs/`.
4. Coordinated change `openspec/changes/add-runtime-concurrency-limits/` — proposal, design,
   tasks, and the `runtime-resource-governance` delta.

## Context sources (read for orientation; not review targets)

- `docs/implementation/developer-facing-interface-refactor-phased-plan-2026-07-14.md` —
  phase order and review gates.
- `docs/review/developer-facing-interface-phase-00-lease-contract-amendment-draft-2026-07-16.md`
  — the applied lease/strong-type amendment record and its decision table.
- The two Phase 0 guard review documents in `docs/review/` (2026-07-15) and the guard project
  `tests/OrcaCore.DeveloperSurface.Guards/` — note the guards are known-stale against the
  amended contract; their remediation is a tracked pending item, not a new finding.
- Current provisional source for contrast: `src/OrcaCore.Core/Building/`,
  `src/OrcaCore.Abstractions/Steps/StepResult.cs`, `src/OrcaCore.Abstractions/Ids/`,
  `src/OrcaCore.Engine.Durable/Execution/DurableWorkflowRuntime.cs`.

## Suggested commands

Run from the repository root:

```powershell
openspec validate reshape-developer-facing-interfaces --strict
openspec validate add-runtime-concurrency-limits --strict
openspec instructions apply --change reshape-developer-facing-interfaces --json
```

Search freely (ripgrep) for cross-artifact consistency. Building/testing is optional context;
the proposed surface is not implemented yet.

## Required review method

1. **Signature-by-signature pass over matrix §17.2.** For every approved signature, check
   internal consistency (parameter order, selector shapes, generic patterns, naming), and
   check that each analogous ephemeral/durable pair differs only where the capability matrix
   says it must.
2. **Write real code.** Author at least four complete workflows against the proposed
   signatures as a developer would: (a) an ephemeral parallel enrichment with a transient
   pool; (b) a durable order flow with `WaitLong`, `AcquireResources`, and the reserved
   external-job shape stubbed; (c) a durable `WhenFirst` timeout-vs-acquisition race; (d) the
   split-host external-job completion journey through the facade (registration, StartOrGet,
   typed routing outcomes, management inspection). Report every point of friction, ambiguity,
   or missing capability you hit. Include the code in your review.
3. **Adversarial misuse pass.** Attempt to express wrong programs and classify each attempt:
   compile-impossible / rejected at fluent call / rejected at `Build()` / rejected at
   registration or startup / runtime diagnostic / **silently accepted (finding)**. Cover at
   least: durable `ForEach`; ephemeral `WaitLong`/`ContinueAsNew`/`AcquireResources`; nested
   `Init`/`End`/external jobs; transient pool on durable builders; lease reacquisition on the
   same fiber ancestry and in a `While` loop; `ContinueAsNew` with a held lease; duplicate
   pool names in one request; empty/default identities and names; destructive operations with
   default confirmation; starting an unregistered definition; completing an external job with
   the authored key instead of the runtime occurrence id; mismatched `TransientPoolName` /
   `ResourcePoolName` usage; a dangling or mis-targeted prefix decorator (retry/timeout/pool
   policy authored immediately before `Wait`, `End`, or the end of a branch — determine
   whether the contract specifies a diagnostic or permits a silent drop).
4. **Lifecycle-model stress.** Evaluate the no-author-TTL lease model on its own terms
   (§17.4, MG-062/064, durable-runtime delta): deterministic release, review-deadline
   mark/reconcile, causal-proof recovery, forced-termination quarantine, capacity/debt
   accounting, `LeaseLost`, operator force-release with fencing. Look for unstated states,
   unreachable recoveries, livelocks, capacity leaks, or operator dead ends. The deliberate
   tradeoff — ambiguous ownership can block capacity pending operator action — is accepted;
   review whether the surrounding contract makes it safe and observable, not whether the
   tradeoff should exist.
5. **Vocabulary and projection audit.** Verify the strong matching-type family (`EventName`,
   `WorkflowOutcomeName`, `AuthoredBranchId`, `ResourcePoolName`, `TransientPoolName`,
   `StartIdempotencyKey`, `ExternalJobKey`, `ExternalJobId`, `EventId reportId`,
   `LeaseObligationId`) is complete, consistently specified (validation, ordinal equality,
   scalar serialization, no implicit string conversion), and used everywhere its concept
   appears. Flag any remaining raw primitive that is a cross-call-site or cross-host matching
   contract. Verify application projections expose no `FiberId`/`ScopeId`/`WaitSequence` or
   compiled IR.
6. **Gap hunt.** Name journeys or contracts the surface cannot express or leaves
   underspecified. Consider at minimum: definition versioning with in-flight instances;
   observability of parked acquisitions and queue depth; saga authoring symmetry; DAG
   execution surface; pause/resume semantics; error taxonomy completeness for facade
   outcomes; the reserved `RunExternalJob` block (task 5.0) — assess whether the reservation
   note constrains it enough to prevent another invented-contract cycle; and the signatures
   matrix §17.2 does **not** pin — `Then` (including `Then<TStep>()`), `If`, `While`, step
   decorators, and the six-parameter ephemeral `ForEach` — the same unapproved-region failure
   mode that previously produced an invented lease contract.

## Known-open items (re-reporting these verbatim is not a finding; challenging them is welcome)

- `RunExternalJob` exact signature is reserved behind task 5.0.
- Phase 0 guard project still targets the superseded `AcquireLease` shape; a ten-point
  remediation list exists in the amendment record.
- Durable transient-pool authoring is absent until `add-runtime-concurrency-limits` durable
  enforcement lands (Phase 3).
- `Saga.Durable` is not shipped until runtime-owned durable progression exists (Phase 7).
- Acquisition-timeout overload deliberately absent; the authored idiom is a `WhenFirst` race.
- One reproducible integration baseline failure (`INT_OB_013`) is tracked separately.

## Required output

Produce a single review document containing:

1. **Verdict** for the proposed surface baseline: **APPROVE**, **APPROVE WITH CHANGES**, or
   **REJECT** — with the two or three considerations that drove it.
2. **Findings** ordered P0 (contradiction or unsound contract) / P1 (misuse hole, unsafe
   default, or blocking inconsistency) / P2 (ergonomic or specification gap that will surface
   in Phase 1–5 implementation) / P3 (polish). Each finding: exact file/line evidence, the
   developer or operator scenario where it bites, and the smallest normative remediation.
3. **Dimension scores** with one-paragraph justifications: consistency, comprehensiveness,
   developer orientation, misuse resistance.
4. **The journey code you wrote** (method step 2) with inline friction notes.
5. **Misuse classification table** (method step 3) — every attempted wrong program and where
   the surface stops it, or that it doesn't.
6. **Answers to these focused questions:**
   - Is the mode-first builder split (distinct root, branch, and definition types per mode)
     carrying its weight versus the duplication it creates?
   - Does the lease contract (no TTL, ancestry rule, quarantine recovery) compose safely with
     `WaitLong`, `WhenFirst` cancellation, `ContinueAsNew`, and forced termination?
   - Is the strong-type family the right size — anything missing, anything that is ceremony
     without a matching contract behind it?
   - Are `Wait` vs `WaitLong` and `AcquireResources` (vs lease vocabulary) the right names
     for what they do?
   - Can a developer discover why something is absent (e.g. durable `ForEach`, nested `If`)
     without reading the spec repository?
   - Fluency: should the ephemeral builder offer a named inline lambda step
     (`.Then("name", async (ctx, ct) => ...)`) or is class-only `IStep<TState>` the right
     contract for both modes? Is the trailing-merge shape on `Parallel`/`WhenFirst` the right
     tradeoff versus a terminal `.Merge(...)`? Can teams write reusable authoring blocks that
     apply to both root and branch builders, and if not, is that acceptable?
   - What single change would most improve the surface?
7. **Explicit non-findings**: decisions you examined, initially doubted, and now endorse —
   with reasoning. This is as valuable as the findings.

Do not begin implementation, do not edit specs, and do not restate the documents back at
length — every sentence of the review should carry a judgment, evidence, or a remediation.
