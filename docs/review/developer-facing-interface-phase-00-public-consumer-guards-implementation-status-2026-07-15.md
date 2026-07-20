# Developer-facing interface Phase 00 remediation status

Original evidence date: 2026-07-15. Contract-amendment update: 2026-07-16.

## Outcome

**NOT READY FOR RE-REVIEW; TASK 3.11 GUARDS REQUIRE RETARGETING.**

The second guard remediation closed the earlier task 3.5, task 3.10, and Release-build
findings. Its lease guards then exposed a deeper problem: they invented
`AcquireLease(id selector, ResourceLease selector, duration)` and encoded elapsed expiry as
capacity release. The approved planning contract now differs materially:

- structural method: `AcquireResources` on durable root and durable branch builders;
- descriptor: factory-only `ResourceLeaseRequirement(ResourcePoolName, positive int units)`;
- static and selector overloads, with no author TTL, expiry parameter, holder ID, or renewal;
- exact runtime `LeaseObligationId` occurrence spanning queued, pending-commit, held, marked,
  ambiguous, forced-stop quarantine, and terminal states;
- path-sensitive inclusive fiber-ancestry rule, with independent siblings allowed;
- dynamic selection committed before provider mutation and replayed afterward;
- expiry marks and reconciles owner state while retaining capacity; time alone never releases;
- capacity uses state-based reserved-unit conservation and grant-time admission; downward
  resize creates no-new-grants debt, and exact numeric restoration applies only to an isolated
  no-waiter/no-resize case.

No production source or test/guard source was changed by the 2026-07-16 planning amendment.
Phase 1 has not started.

## OpenSpec state

Tasks 3.1-3.11 remain unchecked and task 4.0 remains untouched. The last observed progress was
**15/111**. Tasks 3.1-3.10 retain their accepted review disposition; task 3.11 remains rejected
until the guard-only remediation below is implemented and independently reviewed.

## Prior remediation disposition

| Task | Prior result | Current state after contract amendment |
|---|---|---|
| 3.1-3.4 | accepted | Retained, but 3.4 must gain the approved strong-name and lease placement compile cases |
| 3.5 | accepted after recursive inherited/nested IR scanning | Retained |
| 3.6-3.9 | accepted | Retained |
| 3.10 | accepted after explicit no-mutation evidence | Retained |
| 3.11 | rejected | Existing authored signature, expiry behavior, ancestry coverage, and capacity assertions are superseded |

## Required task 3.11 guard remediation

### Public/compile shape

1. Replace every reflected or authored `AcquireLease(..., duration)` assumption with the exact
   `AcquireResources` static/selector overloads from document 17.
2. Require `ResourcePoolName` and factory-only `ResourceLeaseRequirement`; reject raw string,
   `TransientPoolName`, non-positive units, empty/duplicate requests, public positional
   construction, mutable/init setters, and duration/renewal overloads.
3. Make durable nested `AcquireResources` a positive compile fixture. Keep
   `WithTransientPool(TransientPoolName)` absent from durable root/nested builders and durable
   resource acquisition absent from ephemeral builders.
4. Add path-sensitive positive/negative fixtures for mutually exclusive `If` arms,
   same-fiber reacquisition, direct-loop reacquisition, per-iteration child fibers,
   ancestor/descendant rejection, independent siblings, and parent acquisition after child
   release.
5. Keep the qualified `StepResult.AcquireResources` absence guard until task 5.4 removes it.

### Behavioral depth

1. Invalid dynamic selection must fail before provider mutation; a committed normalized
   selection must be reused after restart.
2. Pending and held phases must share one exact lease-obligation occurrence. Acquired/released
   facts and provider tickets must preserve instance generation, authored node, fiber/scope
   occurrence, ticket, pool, units, and provider ownership generation.
3. Normal exit, losing-branch cancellation, failure, and parent merge must prove exact release
   ordering; forced termination must fence/quarantine until protected work stop/fence is proven.
4. A granted lease followed by `ContinueAsNew` must reject rollover and prove no aggregate or
   provider capacity leak.
5. Grant versus cancel/fail/forced-terminal races must leave one active obligation, one exact
   release, or one exact quarantine, never a ghost/partial grant.
6. Expiry coverage must separate: audible mark retaining capacity; live owner retained without
   renewal; ambiguous/bare-terminal owner held; proven orphan/release gap recovered once;
   missing ticket produces `LeaseLost`; force-release without stop/fence proof stays quarantined.
7. Isolated tests may assert exact numeric restoration. Contended tests must assert exact
   ticket/unit conservation and next-waiter eligibility instead of global snapshot equality.
8. Pending-commit/held/marked/ambiguous/quarantined states reserve units; resize exposes
   `max(0, reserved - configured)` debt and grants nothing new until exact release or upward
   resize clears debt and the next request fits.

## Strong matching-value follow-through

Phase 0/1 public and compile guards must also cover the approved non-interchangeable types:

- `EventName` for waits, routing, envelopes, and projections;
- `WorkflowOutcomeName` for named `End`; `End()` alone is unnamed;
- `AuthoredBranchId` for graph-local authored branch identity;
- `ResourcePoolName` versus `TransientPoolName`;
- `StartIdempotencyKey` versus `InstanceId`;
- authored `ExternalJobKey`, runtime `ExternalJobId`, and `EventId reportId`;
- typed application payload/results versus serialized protocol payloads.

Guards must reject implicit conversions and raw-string compatibility overloads and provider
certification must prove exact ordinal/case-sensitive scalar equality across collations.

## Historical verification evidence (not rerun on 2026-07-16)

These are the last recorded results from the guard-only remediation before the contract was
amended. They remain useful as a baseline but do not validate the new task 3.11 contract:

| Gate | Last observed result |
|---|---|
| guard project Release build | 0 warnings, 0 errors |
| `Disposition=Infrastructure` | 6 passed |
| compile fixtures, green | 11 passed in Release |
| runtime expected-red | 28 product-contract failures, 0 passes/skips |
| compile expected-red | exactly 2 product gaps |
| non-container regression suites | 968 passed, 0 failed/skipped |
| PostgreSQL + SQL Server | 136 passed, 0 failed/skipped, run sequentially |
| integration | 113 passed, 1 reproducible unrelated failure, 1 intentional skip |

The integration failure was `INT_OB_013_CatalogGaugesUseProviderAndResourcePoolState`
(missing `orca.instances.stuck`); `INT_JS_018` was the intentional slow-soak skip.

## Review gate

Do not send the current lease guards for acceptance and do not check task 3.11. After the
guard-only remediation:

1. rerun both guard lanes in Release;
2. record actual updated runtime and compile red counts;
3. strict-validate both coordinated OpenSpec changes;
4. refresh this status and the reviewer prompt;
5. request an independent re-review focused on task 3.11 plus the newly approved signature
   and strong-name guards.

Phase 0 may exit only after that review accepts tasks 3.1-3.11. No commit has been created.
