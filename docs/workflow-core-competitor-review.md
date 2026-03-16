# Workflow Core Competitor Review

Reviewed on March 14, 2026.

## Scope

This review focuses on:

- the Workflow Core repository: https://github.com/danielgerlag/workflow-core
- the Workflow Core docs: https://workflow-core.readthedocs.io/en/latest/

Workflow Core is relevant because it is close to the same product category as OrcaCore: an embeddable .NET workflow engine with long-running workflows, external events, control structures, persistence providers, and code-first authoring.

## High-level assessment

Workflow Core is the closest direct competitor reviewed so far.

It demonstrates that an embeddable .NET workflow engine can successfully provide:

- fluent code-first workflow authoring
- JSON / YAML definitions
- built-in control structures
- pluggable persistence providers
- external-event waits
- compensation-oriented saga support

At the same time, it also shows several weaknesses that are directly relevant to OrcaCore.

The pattern is consistent across docs and issue history:

- authoring is relatively simple
- infrastructure options are broad
- runtime semantics become less clear in harder cases
- operational and correctness guarantees appear weaker than the feature list suggests

## What Workflow Core gets right

### 1. Embeddable library-first model

Workflow Core positions itself as a lightweight embeddable workflow engine targeting .NET Standard, not as a mandatory external workflow platform.

That matters because your target is also library-first and app-embedded.

Source:

- Repository README: https://github.com/danielgerlag/workflow-core

### 2. Compact authoring surface

The fluent builder API is one of Workflow Core's biggest strengths. The examples are easy to read, and the engine supports both fluent and JSON/YAML definitions.

This is important competitive pressure for OrcaCore. If your engine is more correct but much harder to author, adoption will suffer.

Source:

- Repository README fluent and JSON/YAML examples: https://github.com/danielgerlag/workflow-core

### 3. Useful control-structure coverage

Workflow Core docs explicitly support decision branches, parallel paths, loops, delays, recurring execution, and compensation-oriented sagas.

That confirms that your intended primitive list is realistic for an embedded engine.

Sources:

- Control structures: https://workflow-core.readthedocs.io/en/latest/control-structures/
- Saga transactions: https://workflow-core.readthedocs.io/en/latest/sagas/

### 4. Event wait as a first-class primitive

Workflow Core explicitly documents `WaitFor` as a first-class external event wait, including event name, key, and an optional effective date.

That is an important baseline. Your engine should also treat waits as first-class rather than as custom polling code.

Source:

- External events: https://workflow-core.readthedocs.io/en/latest/external-events/

### 5. Provider breadth

Workflow Core supports multiple persistence providers and related infrastructure packages. That is evidence that pluggable persistence is feasible in this product category.

Source:

- Repository README persistence section: https://github.com/danielgerlag/workflow-core

## Where Workflow Core looks weak

This section separates documented observations from inferences based on issue history.

### A. Event correlation model is too thin

Documented surface:

- waits are modeled primarily with event name and event key
- the host publishes an event by name and key
- workflows that subscribed to that pair are resumed

Why this is a problem:

That is a usable API, but it is a weak model for complex workflows. The docs do not show a richer event envelope, branch-scoped wait identity, deduplication rules, ordering rules, or explicit out-of-order behavior.

Implication for OrcaCore:

Do not stop at `WaitFor(eventName, eventKey)`. You likely need:

- a canonical event envelope
- subscription identity
- branch/wait token identity
- deduplication metadata
- explicit matching semantics

Source:

- External events docs: https://workflow-core.readthedocs.io/en/latest/external-events/

### B. Parallel and join semantics appear under-specified

Documented surface:

- matching multiple next steps results in parallel branches running
- the library supports `Parallel()` and `Join()`

What is missing from the docs:

- branch identity model
- whether joins are all-of or configurable first-of by default
- cancellation semantics for losing branches
- out-of-order branch completion behavior
- interaction of waits inside parallel branches

This matters because these are exactly the cases where workflow engines often become unreliable.

Relevant evidence from issue history:

Issue #273 reports deterministic inconsistent behavior when combining `Parallel`, `WaitFor`, `Join`, and `CancelCondition`; adding a trivial `.Then<NopStep>()` before `WaitFor` changed whether the post-join continuation executed once or twice.

That does not prove every branch/wait case is broken, but it is strong evidence that the semantics are fragile in at least some parallel-wait paths.

Source:

- Control structures docs: https://workflow-core.readthedocs.io/en/latest/control-structures/
- Issue #273: https://github.com/danielgerlag/workflow-core/issues/273

### C. Idempotency appears weak at workflow start

Issue #828 reports that starting a workflow with the same reference can create multiple workflow instances, and the request for idempotent start behavior was closed as not planned.

Why this matters:

For durable orchestration, start semantics are part of correctness. If the caller retries after a timeout or connection issue, duplicate workflow creation becomes a real production problem.

Implication for OrcaCore:

Start semantics should include an explicit idempotency story.

Source:

- Issue #828: https://github.com/danielgerlag/workflow-core/issues/828

### D. Recovery and resume semantics appear limited

Issue #829 asks how to retry a workflow from a failed step. The request notes that resume only applies to suspended workflows and the issue was closed as not planned.

Why this matters:

That suggests the engine has a less developed story for recovery from failures than for recovery from waits. This is a major limitation for real long-running orchestration.

Implication for OrcaCore:

Failure recovery should be designed deliberately, not treated as an afterthought to suspension/resumption.

Source:

- Issue #829: https://github.com/danielgerlag/workflow-core/issues/829

### E. Observability and querying look bolt-on rather than core

Workflow Core supports an Elasticsearch search plugin and related APIs, but issue history suggests instance retrieval and search ergonomics have been pain points.

Issue #261 asked for retrieving multiple workflow instances by ID because iterating one by one was not performant.

Why this matters:

Operational visibility should be a core runtime capability, not something that depends on optional search plumbing for common scenarios.

Implication for OrcaCore:

Basic instance inspection and wait inspection should be part of the engine contract, not an optional plugin story.

Sources:

- Elasticsearch plugin docs: https://workflow-core.readthedocs.io/en/latest/elastic-search/
- Issue #261: https://github.com/danielgerlag/workflow-core/issues/261

### F. Persistence model looks convenient but opaque

Issue #377 discusses workflow data being stored as JSON in SQL persistence and the resulting difficulty for reporting and integration.

Why this matters:

This is a common tradeoff in embedded workflow engines: convenience for the runtime versus poor operational queryability.

Implication for OrcaCore:

Be careful not to let persistence become an opaque blob store. Even if workflow payload is serialized, runtime metadata and waits should remain queryable first-class records.

Source:

- Issue #377: https://github.com/danielgerlag/workflow-core/issues/377

### G. Operational rough edges still show up in recent issue history

The repository page currently shows a substantial open issue count, and the issue list still includes recent problems such as termination consistency, host shutdown behavior, and a .NET 10 null-reference issue.

This does not mean the project is inactive. In fact, the releases page shows recent releases, including v3.17 on October 11, 2025. But it does suggest ongoing operational complexity and maintenance load.

Implication for OrcaCore:

We should assume these engines are hard to get right even after years of iteration. That is a warning against under-specifying semantics early.

Sources:

- Repository page: https://github.com/danielgerlag/workflow-core
- Releases: https://github.com/danielgerlag/workflow-core/releases
- Issues listing: https://github.com/danielgerlag/workflow-core/issues

## About the specific concern: waits in loops and different events in branches

Your claim is directionally plausible, but I want to separate evidence levels.

### What is documented

Workflow Core docs show:

- `WaitFor` external events
- loops and recurring constructs
- parallel paths and joins

But the docs do not provide a strong semantic contract for combining these features in difficult cases such as:

- repeated waits inside cycles
- multiple outstanding waits across branches
- branch cancellation when one branch wins or fails
- deduplication or out-of-order events against repeated waits

That absence is itself a meaningful weakness.

### What is evidenced publicly

Issue #273 provides direct evidence that at least one combination of parallel branches, waits, and join cancellation behaved inconsistently.

### What remains an inference

I did not find a source in this pass that conclusively proves all cyclic wait scenarios or all "different events in branches" scenarios are broken.

So the safe conclusion is:

- Workflow Core exposes the primitives for these scenarios.
- The public docs do not define their semantics strongly enough.
- Public issue history suggests some of these advanced combinations have had correctness problems.

That is enough reason for OrcaCore to treat these cases as first-class design targets rather than optional edge cases.

## Competitive takeaways for OrcaCore

Workflow Core sets a meaningful bar in these areas:

- embeddable form factor
- ease of authoring
- breadth of built-in primitives
- pluggable persistence
- support for external event waits

OrcaCore should aim to beat Workflow Core in these areas:

### 1. Stronger semantics for waits and branches

Explicitly define:

- branch identity
- wait token identity
- allowed resume paths
- duplicate event handling
- out-of-order event handling
- `WhenAll` vs `WhenFirst` semantics
- losing-branch cancellation behavior

### 2. Better correctness around long-running coordination

Explicitly define:

- idempotent start behavior
- per-instance concurrency policy
- consistency between persisted state and outbound events
- failure and retry behavior after partial execution

### 3. Better operational model

Make these first-class:

- instance query APIs
- active wait/subscription inspection
- history / checkpoint inspection
- definition version tracking
- clearer failure and recovery controls

### 4. Better persistence shape

Avoid forcing operators to treat runtime state as an opaque JSON blob for basic inspection and reporting.

### 5. Better research discipline before coding

Workflow Core is a useful warning that having many primitives is not enough. The hard part is the semantic contract when those primitives interact.

## Requirement changes implied by this review

OrcaCore requirements should explicitly include:

1. Idempotent workflow start or an equivalent deduplicated start contract.
2. Formal semantics for waits inside loops and repeated waits.
3. Formal semantics for multiple simultaneous waits across branches.
4. Formal join behavior for all-of and first-of synchronization.
5. Formal cancellation semantics for losing branches.
6. A queryable persistence model for runtime metadata and waits.
7. A failure recovery model beyond suspended-wait resume.

## Final conclusion

Workflow Core is important because it proves the market demand and the basic embeddable shape.

It is not the right benchmark for runtime rigor.

The best way to compete with it is not to copy its API surface one-for-one. The better strategy is:

- keep a similarly approachable authoring model
- define much stronger runtime semantics
- treat event correlation, branching, idempotency, and operability as core design requirements from the beginning

## Sources

- Workflow Core repository: https://github.com/danielgerlag/workflow-core
- Workflow Core docs home: https://workflow-core.readthedocs.io/en/latest/
- Control structures: https://workflow-core.readthedocs.io/en/latest/control-structures/
- External events: https://workflow-core.readthedocs.io/en/latest/external-events/
- Saga transactions: https://workflow-core.readthedocs.io/en/latest/sagas/
- Elasticsearch plugin: https://workflow-core.readthedocs.io/en/latest/elastic-search/
- Releases: https://github.com/danielgerlag/workflow-core/releases
- Issue #273: https://github.com/danielgerlag/workflow-core/issues/273
- Issue #261: https://github.com/danielgerlag/workflow-core/issues/261
- Issue #377: https://github.com/danielgerlag/workflow-core/issues/377
- Issue #828: https://github.com/danielgerlag/workflow-core/issues/828
- Issue #829: https://github.com/danielgerlag/workflow-core/issues/829
