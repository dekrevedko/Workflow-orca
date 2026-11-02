## Why

Issue #16 needs an in-process ephemeral caller to inspect the original failed
step exception and safely release resources after physical work finishes. Output
or terminal status can arrive before a timed-out attempt, callback or continuation
has returned; the current facade exposes neither that exception nor a per-instance
physical-settlement proof.

## What Changes

- Propose exactly two application members in PackageId/assembly `OrcaCore`:
  `WorkflowFailure.ExceptionType` (`string?`, runtime-created getter) and
  `WorkflowInstanceHandle.WaitForSettlementAsync(CancellationToken)` (`ValueTask`).
  Resultful handles inherit the settlement method; no new public type, constructor,
  registration option, helper overload, exception class or provider contract.
- Preserve the exact original business step exception as
  `WorkflowOutputUnavailableException.InnerException` only for an ephemeral
  instance's committed single step failure. Retain detached `Failure` as authoritative.
  Reuse existing `WF-STEP-UNHANDLED` for non-OrcaCore step exceptions rather than
  incorrectly wrapping them as `WF-LEGACY-LIFECYCLE`. Keep an OrcaCore error's own code.
- Carry the captured runtime type name as detached `ExceptionType` text. The owner
  selected the explicit property rather than encoding metadata as a synthetic cause.
  Original exception objects remain in ephemeral-owned memory and never enter
  snapshots, codecs, Core/provider DTOs, persistence, events or durable results.
- Define final-attempt, branch/item, true-aggregate, cancellation, timeout, late
  result and retirement behavior. True join aggregates get no invented live
  AggregateException; single propagated step failures retain their original reference.
- Amend the exact fixed-codec failure form: preserve the original five-field form
  and bytes when ExceptionType is null; use a closed explicitly versioned failure
  form when it is non-null. New readers accept both. Old readers reject the new
  form; no bidirectional old-binary compatibility is claimed. No serializer hook,
  reflection fallback, custom resolver, converter-catalogue expansion or codec ID change.
- Make settlement a notification-driven, observer-local, monotonic receipt on an
  already-issued ephemeral handle. It requires terminality and final physical
  quiescence; it holds no payload pin or host-join obligation, never cancels work,
  and remains usable after retirement and after Completed/owner disposal.
  Durable handles throw NotSupportedException without provider access.
- Reconcile ordinary detached/query/not-found and host shutdown rules with these
  two narrow exceptions. Existing output waits, timeouts, start/stop semantics,
  retention limits and durable failure production remain unchanged.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `workflow-contracts`: original-exception projection, nullable detached type name,
  typed output-wait behavior, and the closed failure-wire amendment.
- `structured-fiber-execution`: final-attempt and ordered join failure ownership.
- `management-and-querying`: detached inspection, passive settlement receipts,
  retired-handle behavior and Closed/Stopping/Completed boundaries.
- `developer-facing-surface`: the exact two-member catalogue and the passive wait.
- `quality-and-verification`: executable failure/settlement, retirement, shutdown,
  package-consumer and six-cell native regression evidence.

## Impact

Base: GitHub-synced master `0a8e375fbcf5005a2a2f491cf24dfbbd9d64c580`.
Branch: `codex/issue-16-ephemeral-failure-settlement`. Preview.3 is now published;
the issue's earlier “not yet published” source note is a dated observation.

The original 26-path contract target changed no product source, canonical/numbered
specification, declaration companion or executable baseline. It was independently
approved and checkpointed at 0cae922 (tree cf054886), with external verdict-only
child 7697c79. Its provenance pins and exact proposed-owner registration were
process prerequisites, not canonical or source approval.

Task 2.1 now prepares the canonical/numbered/declaration/guide transition, including
external F1's durable stored-byte boundary and the P3 clarifications. Its exact
Git-bound registry keeps canonical/source approval unset. This new target remains
uncommitted for independent review. No product source or executable API/compile/
package baseline changes here. Source implementation needs the separately approved
canonical checkpoint. Every major step retains adversarial review and green
validation; the routing/API/scenario artifacts define the later targets precisely.
