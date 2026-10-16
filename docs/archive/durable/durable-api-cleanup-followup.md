# Durable API Cleanup Follow-Up

Saved on 2026-03-16.

## Purpose

This document captures the remaining durable API cleanup work after the review-remediation plan was implemented.

The durable runtime is functionally in good shape now. The items here are not foundational correctness gaps. They are public-surface cleanup, naming, and usability decisions that should be reviewed before the next round of API shaping.

## Current state

The durable surface now includes:
- async instance inspection APIs
- durable query scopes
- typed durable definition scopes
- explicit delete and purge management APIs
- policy-based retention configuration
- pluggable outbox dispatch infrastructure

The remaining gaps are mostly about keeping the public API narrow, intentional, and future-proof.

## Cleanup goals

- prefer one obvious public way to perform a management operation
- keep provider-facing contracts separate from application-facing convenience APIs where practical
- avoid naming that will become awkward once archive/terminate semantics are added
- make the typed and untyped durable surfaces feel consistent
- avoid exposing low-level persistence details unless they are intentionally part of the provider contract

## Proposed work

### 1. Make retention policy the primary public purge API

Status:
- completed for runtime scopes
- `DurableInstanceScope` and `DurableSelectionScope` now expose only policy-based purge
- raw cutoff handling remains on `IWorkflowStore` as provider-facing infrastructure

Current issue:
- `DurableInstanceScope` and `DurableSelectionScope` expose both:
  - `PurgeArtifactsAsync(DateTimeOffset olderThan, ...)`
  - `PurgeArtifactsAsync(DurableArtifactRetentionPolicy policy, ...)`

Why this is awkward:
- the raw timestamp overload is lower-level and less expressive
- the policy overload is more aligned with real retention decisions
- both overloads create ambiguity about the preferred application-level usage

Recommendation:
- keep `DurableArtifactRetentionPolicy` as the primary public runtime API
- review whether raw `DateTimeOffset olderThan` overloads should:
  - be removed from the public runtime scope APIs
  - be kept only on the store/provider contract
  - or be marked/documented as low-level convenience only

Implemented end state:
- application/runtime callers use policy-based purge
- provider authors can still work with explicit cutoffs where needed

Decision:
- raw cutoff purge no longer remains public on runtime scopes
- low-level cutoff handling is documented as provider-oriented

### 2. Reassess `DurableArtifactRetentionCutoffs` visibility

Status:
- kept public because it remains part of `IWorkflowStore`
- documented as provider-facing infrastructure

Current issue:
- `DurableArtifactRetentionCutoffs` is public because it appears in `IWorkflowStore`

Why this matters:
- it is a provider-facing concept, not a typical workflow-application concept
- the public runtime package now exposes a low-level type that most consumers should not need

Recommendation:
- keep it public only if `IWorkflowStore` is intentionally a public provider authoring contract
- otherwise consider narrowing the provider contract surface in a later pass

Possible options:
- keep as-is and document it as provider-facing infrastructure
- split runtime-user APIs from provider APIs into separate assemblies/namespaces later
- add XML docs clarifying that cutoffs are for store implementations and advanced operators

### 3. Review delete/purge naming before archive/terminate exists

Current issue:
- `DeleteAsync` and `PurgeArtifactsAsync` are clear enough today
- but future lifecycle operations may introduce:
  - terminate
  - archive
  - soft delete
  - retention-driven cleanup

Potential naming tension:
- `DeleteAsync` sounds final and destructive
- `PurgeArtifactsAsync` is specific but may overlap with future archive/cleanup flows

Recommendation:
- keep current names for now because they are usable and already implemented
- review future lifecycle surface as a single set before adding:
  - `TerminateAsync`
  - `ArchiveAsync`
  - `DeleteAsync`
  - `PurgeArtifactsAsync`

Suggested rule set:
- `TerminateAsync`: stop execution but retain durable record
- `ArchiveAsync`: move terminal instance to long-term historical storage
- `DeleteAsync`: remove instance and associated durable record
- `PurgeArtifactsAsync`: prune inbox/outbox/history artifacts without deleting the instance

### 4. Typed durable surface consistency

Current issue:
- `DurableWorkflowEngine<TState>` provides:
  - typed `Start`
  - typed fanout
  - typed scoped query convenience
- management operations are still reached via instance/selection scopes rather than a typed management surface

This is not wrong, but it may feel uneven.

Review options:
- keep current design
  - typed engine stays focused on start/query/fanout
  - management remains on scopes
- add typed selection convenience later if real usage demands it

Recommendation:
- keep current design unless actual user flows show friction
- avoid multiplying typed management wrappers without clear value

### 5. Query API positioning

Status:
- documented on the scope types as admin/management-oriented and in-memory

Current issue:
- durable queries are currently:
  - snapshot-list based
  - expression-compiled in memory
  - suitable for admin/management use, not large-scale querying

Recommendation:
- document this explicitly in API docs and README notes
- avoid implying a scalable remote-query abstraction
- if a future provider-backed query model is added, introduce it as a separate capability rather than evolving the current API in-place

Possible follow-up:
- add XML docs or README wording that `All()` / `Where(...)` are management surfaces

### 6. Public/provider boundary review

Current issue:
- the runtime currently exposes some types that are really for provider authors and advanced operators

Examples:
- `IWorkflowStore`
- `DurableArtifactRetentionCutoffs`
- persistence DTOs

Recommendation:
- review whether the package structure should eventually distinguish:
  - runtime consumer API
  - provider authoring API

This is not urgent, but it becomes more valuable as the durable surface grows.

### 7. Documentation cleanup follow-up

Current issue:
- the remediation and implementation docs are up to date
- but user-facing docs still do not fully explain:
  - which APIs are preferred
  - which APIs are low-level
  - which APIs are provider-facing

Recommendation:
- add short API guidance to public docs:
  - prefer policy-based purge for application code
  - use raw cutoffs only for advanced/operator/provider scenarios
  - query scopes are management/admin oriented

## Suggested implementation order

1. Decide whether raw cutoff purge stays public on runtime scopes
2. Add documentation/API comments for preferred purge usage
3. Review lifecycle naming against future terminate/archive semantics
4. Decide whether provider-facing contracts should be separated more clearly
5. Revisit typed management convenience only if usage shows need

## Suggested acceptance criteria

- public docs clearly indicate the preferred purge API
- low-level/provider-facing types are either documented as such or narrowed
- delete/purge naming is reviewed against future lifecycle operations
- query APIs are documented as management/admin surfaces, not large-scale data-query surfaces
- no new duplicate management entry points are added without a clear reason

## Review prompts

- Should `PurgeArtifactsAsync(DateTimeOffset olderThan, ...)` remain public on runtime scopes?
- Should `DurableArtifactRetentionCutoffs` be considered part of the supported public provider contract?
- Do we want to reserve `DeleteAsync` exactly as-is for hard deletion, or rename before archive/terminate arrives?
- Is the current typed durable surface sufficient, or do we want typed management convenience too?
- Should provider contracts eventually move into a distinct package/namespace boundary?
