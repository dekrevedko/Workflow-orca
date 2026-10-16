# Structured Fiber Execution Implementation Status - 2026-07-13

Change: `adopt-structured-fiber-execution` (archived by OpenSpec as
`2026-07-15-adopt-structured-fiber-execution`)

Branch: `feature/v3-rebuild`

Status: **implementation findings fixed, all verification gates passed, and the completed
OpenSpec change archived**.

## Outcome

The root implementation now uses one compiled linear-fiber runtime model for selected
ephemeral and durable workflows. Structured branching is represented by recursive scopes,
private child state, typed branch/item results, deterministic reducer-owned joins, and one
explicit replacement-state merge. The durable runtime persists only the format-2
fiber/scope envelope.

The following legacy execution surfaces are removed:

- durable cursor records, frame split/join, candidate scanning, and format-1 resume;
- shared-state ephemeral `Parallel`, `WhenFirst`, and `ForEach` runners;
- detached `WhenFirst` and `ForEach` residual policies;
- runtime parking as a substitute for selected-mode compiler validation.

Development databases containing cursor checkpoints must be reset as documented in
[`durable-development-store-reset.md`](../durable-development-store-reset.md). A stale
format-1 checkpoint is diagnosed as incompatible and is never interpreted.

## Delivered Architecture

- Mode-first `Workflow.Ephemeral<TState>` and `Workflow.Durable<TState>` builders compile to
  immutable fingerprinted plans through one compiler and deterministic diagnostic contract.
- Fibers own one instruction position and private state; recursive scopes own child fibers,
  results, merge, and every residual wait/timer/job/resource/child obligation.
- Persisted round-robin scheduling, bounded 1,024-instruction default quanta, and explicit
  `Yield` prevent one local branch from starving siblings.
- `WhenAll`, strict `WhenFirst`, and ephemeral dynamic `ForEach` use typed isolated results;
  durable `ForEach` remains absent and compiler-rejected.
- Durable format-2 checkpoints preserve scheduler, scope-entry sequence, identities,
  ownership, pending results, and continuation position across replay and host replacement.
- Saga eligibility, child groups, terminal cleanup, resource ownership, and continue-as-new
  use fiber/scope ownership. Continue-as-new is root-only and requires quiescence.
- PostgreSQL and SQL Server migrations and certification preserve owner indexes and nested
  format-2 checkpoint replacement.
- Repository guards prohibit reintroducing cursor execution and enforce compile-time
  capability validation and the production-file size boundary.

## Final Verification Findings

Two issues were found by the final gates and fixed before completion:

1. Selected ephemeral retry/timeout support pushed `InMemoryExecutionStateAdapter.cs` above
   the 1,000-line repository limit. Step suspension helpers were extracted to a partial file;
   the guard is green.
2. Structured durable step-result commits reused the graceful-drain cancellation token. A
   stop at the drain boundary could discard an already-finished step result. Step bodies
   remain cancelable, while every returned result now completes its current durable commit
   without shutdown cancellation, matching DR-033 and the promoted driver contract.

The reference-model gate was also expanded so final verification genuinely covers seeded
nested scopes, bounded dynamic `ForEach`, randomized completion order, yields, duplicate
deliveries, failures, cancellation, and crash/reload points.

## Post-Review Remediation

The implementation review found three normative behaviors present only in shared Core tests
and one ambiguous production interpreter surface. Each finding now has a production-facing
regression and remediation:

1. Ephemeral management status is refreshed from `ExecutionStatusDeriver` while structured
   execution is active. Durable checkpoint and management projections derive `Running` or
   `Waiting` from the committed format-2 envelope after event replay. A waiting branch no
   longer hides a runnable or executing sibling.
2. Ephemeral waits now receive monotonic per-instance `WaitSequence` values and structured
   owner identities. Unscoped delivery selects the lowest sequence with stable `FiberId`
   tie breaking, matching durable routing.
3. Both production engines enforce `MaxInternalInstructionsPerQuantum`. Durable rotation
   commits a requeued envelope and ephemeral rotation cooperatively yields; both increment
   forced-rotation diagnostics. Checkpoint-only runnable commits now emit a successor
   `continue` record so Required-mode pumping cannot strand a rotated fiber.
4. The full dispatch implementation formerly named `LinearFiberInterpreter` is now an
   explicit `ReferenceLinearFiberInterpreter` in the Core test tree. Production adapters
   share the compiler, reducers, scheduler, status derivation, and quantum accounting, while
   retaining engine-specific persistence and suspension dispatch.

The associated correctness and hot-path suggestions were also applied: structured delays
use their actual logical timer identity, external-job completion uses one shared event-name
constant, immutable plans own instruction/scope identity indexes, typed delegate and branch
step invokers are cached, and scheduler admission uses hash-backed uniqueness checks.

During expanded verification, the hosted outbox test exposed a pre-existing synchronization
race: it stopped the host after transport dispatch but before the pump persisted the
`Dispatched` state. The test now waits for that persisted state and does not alter runtime
behavior.

## Archive-Blocking Review Remediation

The archive-blocking implementation review identified nine additional correctness gaps.
Editorial correction (2026-07-14): an earlier draft said eleven, while the numbered findings
and implemented remediations total nine.
They are now covered by focused regressions and fixed in the production paths:

1. Compiler validation now rejects empty scopes, checks every loop path for a quantum-ending
   operation, enforces configured static scope-depth and active-fiber bounds, and validates
   both serialized-size limits as positive. Dynamic `ForEach` admission also respects the
   per-instance active-fiber limit.
2. Branch/item result payloads are size-checked before their transition is committed, and
   durable format-2 envelopes are serialized and checked before checkpoint commit. An
   oversized result fails its owning scope without retaining the oversized payload.
3. The configured serializer registry is now the codec used by both production engines.
   The default registry rejects unsupported contracts during compilation rather than
   claiming availability that runtime JSON serialization cannot satisfy.
4. Plan fingerprints include captured declared configuration with cycle-safe deterministic
   traversal and an explicit fingerprint-source contract for opaque configuration. Compiled
   instruction, scope, branch, policy, and allowlist collections are deep immutable views.
5. Successful merge prunes its completed scope/fiber subtree. Cumulative yield and rotation
   diagnostics survive pruning and durable replay, so repeated scopes no longer grow the
   envelope while historical scheduler diagnostics remain monotonic.
6. Ephemeral retry performs at most one user-step invocation per scheduler turn. Zero-delay
   retry rotates behind runnable siblings; timed backoff is an owned retry block resumed
   outside the instance turn.
7. Ephemeral advancement, global-step, and named-pool governance now use bounded token
   channels. A saturated structured pool blocks only the exact owning fiber, lets runnable
   siblings advance, releases the instance lane, and resumes that fiber after channel grant.
8. Transient in-process pool authoring is absent from the durable root builder and is
   compiler-rejected as defense in depth for manually or branch-authored durable policies.
9. Format-2 durable envelopes persist a monotonic per-instance next registration sequence;
   removing a completed wait, timer, job, resource, or child obligation cannot reset it.

Regression coverage also protects captured-delegate cycles, custom serializer invocation,
scope-loop retention, saturated local-pool sibling progress, retry fairness, durable
wait-sequence reuse, and the repository production-file size guard.

## Ephemeral Correctness Re-review Remediation - 2026-07-14

The second implementation review empirically reproduced two ephemeral workflow-wedging
defects and identified two adjacent failure-reporting gaps. All four findings now have
permanent production-path regressions:

1. `ForEach` `WhenAny` and fail-fast residual cancellation now uses the same post-order
   ownership traversal as fixed structured scopes. Losing item fibers, their nested scopes,
   and every descendant fiber are terminal before scope pruning and merge; nested waits are
   removed from the scheduler and instance registry.
2. Ephemeral merge, branch-return projection, scope-input projection, and structural wait
   correlation boundaries convert non-cancellation exceptions into fiber/scope failure and
   an observable failed workflow. Internal `StructuredMergeException` no longer escapes
   `StartAsync`, event delivery, timer pumping, or delayed resumption.
3. Delayed retry and channel-backed resource-grant continuations have a final exception
   boundary. An unexpected continuation failure cancels owned waits/timers, fails the exact
   fiber ancestry where possible, commits a failed management snapshot, and does not become
   an unobserved fire-and-forget task exception.
4. A user step throwing `NotSupportedException` follows the normal step-failure path. The
   adapter's explicit durable-only `StepResult` rejection remains separate and retains its
   existing public behavior.

The three reviewed hot paths were also tightened without changing semantics:

- aggregate status derivation runs only at suspension, scope, merge, and no-runnable-work
  boundaries that can change residency, rather than on every structural instruction;
- compiled plans expose a frozen O(1) sequential-successor index used by ephemeral advance;
- durable envelope validation serializes once and passes that exact checked payload to the
  command, avoiding a second serialization during implicit checkpoint conversion.

## Verification Matrix

All commands ran from the repository root. The second-remediation suites ran against the
current Debug build, followed by a complete Release solution build with warnings treated as
errors.

| Project or gate | Result |
|---|---:|
| `OrcaCore.Core.Tests` | 363 passed |
| `OrcaCore.Engine.Ephemeral.Tests` | 168 passed |
| `OrcaCore.Engine.Durable.Tests` | 280 passed |
| `OrcaCore.Hosting.Tests` | 15 passed |
| `OrcaCore.Acceptance.Tests` | 71 passed |
| `OrcaCore.ProviderCertification` | 71 passed |
| `OrcaCore.Providers.PostgreSql.Tests` | 75 passed |
| `OrcaCore.Providers.SqlServer.Tests` | 61 passed |
| `OrcaCore.Integration.Tests` | 114 passed; 1 catalogued slow-soak test skipped |

Current post-fix verification total: **1,218 passed, 0 failed, 1 skipped**. PostgreSQL,
SQL Server, and integration tests were rerun sequentially against a live Docker service
after the second remediation.

Additional gates:

- `dotnet build OrcaCore.slnx --configuration Release --no-restore --warnaserror`: 0
  warnings, 0 errors.
- examples, dashboard, and benchmark projects: Release builds clean.
- benchmark coverage: compiler cost, quantum scheduling, envelope size, nested advancement,
  merge replay, and provider checkpoint materialization.
- `openspec validate adopt-structured-fiber-execution --strict`: valid.
- 25 canonical Markdown files: every local link resolves.
- 232 canonical spec heading IDs: no duplicates.
- production/test/sample search: no references to deleted cursor or shared-state runner types.

## Task State

OpenSpec section 15 records this re-review. **102 of 102 tasks are complete**. The
correctness implementation and its complete container-backed verification matrix passed
final review gates, and the completed change was synchronized into the main OpenSpec
capabilities before archival.
