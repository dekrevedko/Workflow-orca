# Deferred independent re-review prompt: developer-facing interface Phase 00

Status update: 2026-07-16. **Do not issue this review until the task 3.11 guard source has been
retargeted and the implementation-status document contains fresh counts.**

When ready, independently review the Phase 0 guard/fixture implementation for OpenSpec change
`reshape-developer-facing-interfaces` from the viewpoint of an external OrcaCore application
developer and provider author. Tasks 3.1-3.10 were accepted after two remediation rounds.
Task 3.11 remains rejected because the existing guard invented an unapproved lease signature
and unsafe expiry behavior. Do not begin Phase 1 and do not infer completion from the report.

## Read first

1. `docs/review/developer-facing-interface-phase-00-public-consumer-guards-implementation-status-2026-07-15.md`
2. `docs/review/developer-facing-interface-phase-00-lease-contract-amendment-draft-2026-07-16.md`
3. `docs/specs/03-domain-model-and-glossary.md`
4. `docs/specs/09-requirements-management-operations.md` (MG-060 through MG-065)
5. `docs/specs/12-acceptance-criteria.md` (AC-518 through AC-527)
6. `docs/specs/16-requirements-durable-driver.md` (DR-038 and DR-AC-034 through DR-AC-038)
7. `docs/specs/17-selected-mode-capability-matrix.md`
8. all artifacts returned by
   `openspec instructions apply --change reshape-developer-facing-interfaces --json`
9. the coordinated `add-runtime-concurrency-limits` proposal/design/delta/tasks

Inspect every changed file under `tests/OrcaCore.DeveloperSurface.Guards`, the solution entry,
and the Phase 0 review documents. Verify claims against source and executable behavior.

## Required commands

Run from `X:\Projects\GitHub\Workflow-orca`:

```powershell
git status --short
dotnet build OrcaCore.slnx --configuration Release --warnaserror
dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj --no-build --configuration Release --filter "Disposition=Infrastructure" --nologo
tests/OrcaCore.DeveloperSurface.Guards/run-compile-fixtures.ps1 -Disposition Green
dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj --no-build --configuration Release --filter "Disposition=ExpectedRed" --nologo
tests/OrcaCore.DeveloperSurface.Guards/run-compile-fixtures.ps1 -Disposition ExpectedRed
openspec validate reshape-developer-facing-interfaces --strict
openspec validate add-runtime-concurrency-limits --strict
openspec instructions apply --change reshape-developer-facing-interfaces --json
git diff --check
```

The expected-red commands must return nonzero only for approved product gaps. Infrastructure,
setup, restore, path, typo, or obsolete-signature failures are unexpected. Record observed
counts; do not expect the historical 28 runtime/two compile gaps to remain unchanged after the
new fixtures.

## Review requirements

### 1. Preserve accepted Phase 0 depth

- Reconfirm exhaustive exported-type classification and recursive public-signature closure.
- Reconfirm real consumer/provider-author builds, Release configuration, definition IR
  opacity, state detachment, authored wait projections, durable golden path, split-host
  progression, facade outcomes, and their before/after mutation invariants.
- Treat any regression in tasks 3.1-3.10 as a new finding, not as outside the task 3.11 focus.

### 2. Exact authoring contract

Require precisely:

```csharp
ResourceLeaseRequirement.Require(ResourcePoolName pool, int units = 1)

DurableWorkflowBuilder<TState>.AcquireResources(
    params ResourceLeaseRequirement[] requirements)
DurableBranchBuilder<TBranchState, TResult>.AcquireResources(
    params ResourceLeaseRequirement[] requirements)
DurableWorkflowBuilder<TState>.AcquireResources(
    Func<TState, IReadOnlyList<ResourceLeaseRequirement>> requirements)
DurableBranchBuilder<TBranchState, TResult>.AcquireResources(
    Func<TBranchState, IReadOnlyList<ResourceLeaseRequirement>> requirements)
```

- Reject `AcquireLease`, any duration/TTL/expiry/renewal/holder parameter, raw-string pool
  overload, public positional descriptor constructor, mutable/init properties, defaultable
  unit wrapper, and method-name-only reflection.
- Verify durable root and durable branch presence; ephemeral absence; durable transient-pool
  absence; qualified `StepResult.AcquireResources` remains in the removal guard.
- Verify null/default pool, non-positive units, null/empty request, null requirement element,
  duplicate pools, and mutation after authoring are rejected before any pool effect.

### 3. Path-sensitive ancestry and quiescence

Inspect compile and runtime-defense coverage for:

1. then/else acquisitions that are mutually exclusive — accepted;
2. same-fiber second acquisition after a possibly held arm — rejected;
3. direct acquisition in a repeatable `While` body — rejected;
4. fresh terminating child fiber per loop iteration — accepted when parent ancestry is clean;
5. ancestor then descendant acquisition — rejected;
6. branch then nested branch acquisition — rejected;
7. two independent siblings — accepted and independently parkable/grantable;
8. child termination/release then parent acquisition — accepted only after release commit;
9. one atomic multi-resource request — accepted;
10. pending **and granted** lease followed by `ContinueAsNew` — rejected without clearing
    aggregate lease state or leaking provider capacity.

Require exact compiler diagnostics: `SFE-AUTH-LEASE-001` at the conflicting acquisition with
the active acquisition as related location; `SFE-AUTH-LEASE-002` at the loop acquisition with
the loop header related; and `SFE-AUTH-LEASE-003` at `ContinueAsNew` with possible active
acquisitions related. Runtime ancestry defense must end `Failed` with `SFE-RUN-002` before pool
mutation—never parked, retried, or poisoned. Runtime non-quiescent continue-as-new must end
`Failed` with `SFE-RUN-001`, emit no rollover fact, and preserve lease capacity for cleanup.

Runtime checks must inspect a persistent owned-lease state or the union of pending obligations
and held active tickets. A check of pending obligations alone is insufficient.

### 4. Selector and occurrence identity

- Null-list, empty-list, null-element, invalid, and duplicate-pool dynamic results fail before
  provider mutation.
- A selector may retry before its normalized selection commits; after commit, restart reuses
  that value and does not reevaluate it.
- One occurrence identity includes instance, continue-as-new generation, authored node, fiber
  occurrence, and scope-entry occurrence. `AuthoredLocation` or reusable `HolderKey` alone is
  rejected.
- Provider reservation, workflow fact/checkpoint, tickets, and release preserve the exact
  obligation, ticket IDs, pool/unit set, and provider ownership generation.
- Pending grant cannot start guarded work before the workflow acquisition fact commits.

### 5. Release, races, and capacity

- Normal exit, losing-branch cancellation, failed owner, graceful cancellation, and parent
  merge preserve exact release identity and release once. Forced termination fences/quarantines
  until protected work stop or end-to-end fencing is proven.
- Terminal child cleanup/release commits before parent merge/resume.
- Grant versus cancellation/failure/forced-terminal cleanup leaves one committed active
  obligation, one exact release, or one exact quarantine; no ghost grant, partial set, or lost
  units.
- An isolated no-waiter/no-resize case restores exact numeric capacity.
- A contended case may transfer capacity directly to a waiter and therefore asserts exact
  ticket/unit conservation and waiter eligibility, not global snapshot equality.
- Queued/released/cancelled states reserve zero; pending-commit/held/marked/ambiguous/
  quarantined states reserve exact units.
- Downward resize may create visible over-capacity debt but revokes nothing and grants nothing
  new until debt is zero and the next whole request fits. Assert debt is
  `max(0, reserved - configured)` and recomputes after exact release or upward/downward resize.

### 6. Expiry is mark-and-reconcile, not renewal

Reject any test that advances time and immediately expects replacement acquisition. Require:

- due review marks/audits expiry and keeps capacity held;
- exact live/reconstructable owner stays held without holder renewal;
- exact released/never-committed owner, or terminal owner with proven cleanup plus confirmed
  protected-work stop/end-to-end fencing, is recovered once by obligation/ticket/provider-
  generation compare-and-act; bare terminal status remains capacity-reserving quarantine;
- unavailable/ambiguous state stays held for operator action;
- active owner with missing expected ticket/fence receives `LeaseLost` and cannot continue or
  silently reacquire;
- audited force release targets the exact obligation, fences owner resume, and does not make
  units grantable without protected-work stop/end-to-end fence proof;
- crash alone does not prove ownership ended.

Automatic time-only reclaim is out of baseline. If any guard expects it, reject the guard
unless a separately approved end-to-end fenced renewal protocol has been added to the specs.

### 7. Strong matching-value guards

Verify positive signatures, type-swap negatives, raw-string absence, default rejection, scalar
round-trip, and exact provider equality for:

- `EventName`;
- `WorkflowOutcomeName` (`End()` alone is unnamed);
- `AuthoredBranchId`;
- `ResourcePoolName` versus `TransientPoolName`;
- `StartIdempotencyKey` versus `InstanceId`, including 512 accepted/provider-round-tripped
  without truncation and 513 rejected before persistence;
- author `ExternalJobKey`, runtime `ExternalJobId`, and `EventId reportId`.

Application external-job/event payload APIs remain typed; only advanced protocol/provider
records expose serialized bytes plus content type. Do not approve an invented full
`RunExternalJob` overload: task 5.0 must approve and record its complete signature before task
5.1 may implement it.

## Scope and required output

Confirm no production source, compatibility shim, task checkbox, Phase 1 implementation,
archive action, or unrelated cleanup entered this guard-only remediation.

Return exactly one verdict: **APPROVE**, **APPROVE WITH CHANGES**, or **REJECT**. List findings
by P0/P1/P2/P3 with file and tight line evidence, affected task, expected-product-red versus
unexpected infrastructure classification, consumer impact, and smallest remediation.

Finish with:

1. task-by-task disposition for 3.1-3.11;
2. exact observed infrastructure/green/runtime-red/compile-red counts;
3. strict-validation results for both changes;
4. whether Phase 0 may exit;
5. whether tasks 3.1-3.11 may be checked and task 4.0 may begin separately.
