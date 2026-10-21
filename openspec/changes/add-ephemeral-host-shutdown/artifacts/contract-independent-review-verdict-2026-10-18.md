# Issue #2 shutdown contract — independent review verdict

**Date:** 2026-10-19 (request dated 2026-10-18)
**Target:** `codex/issue-2-ephemeral-shutdown`, base `fa88986`, 23 entries, candidate tree
`7d3f144f02f6ab50e1ed029fcc531c1e9be59e0f`.
- Porcelain SHA-256: `0f62cee8…`.
- Content inventory (path order) SHA-256: `71d15f56…`.
- Both were identical at the start and end of review.

## Verdict: REJECT — one owner-directed semantic change (F1), then a targeted re-review

The process side is sound and needs no rework. The rest of the contract is approved in substance:
- the seven-type surface;
- validation and decision precedence;
- the stable-zero join;
- first-dispatch cancellation ownership;
- fault fail-closed;
- independent observers;
- the explicit DI ownership protocol;
- the honest Run/RunAsync and raw-disposal limits.

F1 changes when cancellation starts and which operations stay open while admission is closed. That
changes the normative delta bytes, so the contract cannot be checkpointed as frozen.

Do not commit this target. Remediate F1 (and optionally the P3s) in place, refreeze, and request
re-review. The re-review will check only the diff from `7d3f144f…` plus the lanes.

## F1 — P1 (owner decision) — Closing admission must not start cancellation; apps must be able to drain

**Problem.** As frozen, Generic Host `ApplicationStopping` both closes admission and queues the shared
cooperative-cancel operation. After close, new output waits, queries, commands and ingress on retained
instances are rejected.
- `ApplicationStopping` is the first stop signal: `StopApplication` runs before any hosted
  `StopAsync`, and SIGTERM raises it directly. An application therefore has no point at which it can
  let in-flight workflows finish.
- The application cannot even await completion, because new waits are rejected.
- A workflow waiting for an event cannot receive it, because ingress is rejected.
- The issue asks for cooperative cancellation "where configured". No owner decision for a fixed,
  immediate cancel is recorded.

**Owner decision (2026-10-19):** close first, cancel later, with no new option.

**Required changes** to `design.md`, the API artifact and both deltas:
1. **Two phases, both irreversible.**
   - **Closed.** Business admission stops. New and replayed `StartOrGetAsync` calls and registrations
     fail as host-stopping, with the existing precedence. No cancellation is queued.
     `ApplicationStopping` (checked from the token state at every gate) and raw abortive disposal
     produce only this phase.
   - **Stopping.** The shared operation seals the remaining producers, requests cooperative
     cancellation and joins. Only the engine's hosted `StopAsync` or the public `StopAsync` starts it.
     Each of those closes first if the host is still open. The public signature and the seven types
     stay unchanged.
2. **Closed but not stopping: ordinary semantics for accepted work.** Accepted instances keep their
   normal open-host behavior for:
   - snapshot, state and output queries;
   - new output waits;
   - cancellation and termination commands;
   - event ingress;
   - delays and timers;
   - retries and resource grants.

   Draining is therefore possible. Only new business admission is closed.
3. **Operations after Stopping begins and after Completed.** Define them exactly.
   - Admitted operations stay owned and joined.
   - After Completed, nothing may create owned work, touch DI dependencies, invoke user code, arm a
     timer or commit. Any operation that could do one of those must fail as host-stopping.
   - Reads of already detached data may stay available only if they meet that rule.
   - State which of these, if any, are also rejected while Stopping, with a one-line rationale each.
4. **Drain recipe.** Document the supported way to drain.
   - **Generic Host:** an application hosted service that the host stops before the engine's (reverse
     registration order) awaits its own handles within the shared `HostOptions.ShutdownTimeout`
     budget. Name the `ServicesStopConcurrently` caveat.
   - **Explicit owner:** await your own handles, then call `StopAsync`.
   - **Plain DI:** the application controls its own starts.
5. **Task 3.1 coverage.** Add deterministic cases for:
   - natural completion between close and Stopping;
   - event ingress reaching a waiting instance while closed;
   - a retry or delay continuing while closed;
   - a new output wait admitted while closed;
   - `ApplicationStopping` with no hosted stop yet, where admission is closed and nothing is
     cancelled;
   - the drain recipe in the 3.4 consumer.
6. **Routing.** Update the routing and occurrence artifacts where this changes the numbered owners'
   text (MG-066–069, AC-543–554). Refreeze the provenance pins.

## What I verified

- **Base.** `fa88986` is PR #7's two-parent merge on synced master, and the issue #1 chain
  (`805ae2d` → `1f31652` → `a73a3b3`) is intact as its second parent.
- **Scope.** Only the provenance fixture and mechanical hash/count/catalog pins in
  `OpenSpecCorpusGuards` change: the predecessor catalog grows 16→17. The other 21 paths are new
  planning and evidence files under the change.
- **Provenance.** 195 rows / 51,141 B / `aedc327f…`, with `semanticApprovalEligible: false`.
- **Fit with the existing code.**
  - `OrcaCoreException` has a protected `(code, message, inner)` constructor, so sealed derived
    exceptions with internal constructors are expressible.
  - `Microsoft.Extensions.Hosting.Abstractions` is already a dependency of the engine, so the
    "no new dependency" claim holds.
  - The issue's acceptance criteria are each mapped to a requirement and a task.
- **Framework claims.** These match the pinned 10.0.9 behavior:
  - RunAsync disposes in `finally`;
  - an exception from a hosted stop cannot prevent that disposal;
  - an already-cancelled `CancelAsync` returns early;
  - DI disposes in reverse order.
- **Lanes,** in a disposable copy at `E:/ocv205`, which reproduced tree `7d3f144f…` and has since
  been removed:

| Lane | Result |
|---|---|
| OpenSpec strict | 23/23 |
| Release `-warnaserror` | 0 warnings, 0 errors |
| Exact feed, then Infrastructure | 249/249 |

## P3 (fold into the remediation if convenient)

- **P3-1 — RunAsync behavior change.** For ordinary `Run`/`RunAsync` applications, an Incomplete
  hosted stop now surfaces as an exception that escapes `RunAsync` (Generic Host may aggregate it)
  where it used to exit cleanly. Say this explicitly in the migration note and guide.
- **P3-2 — Closing and finishing the retention change.** `add-ephemeral-terminal-retention` is
  complete, but its change directory is still active. Schedule its archival and provenance refreeze
  in a later target. It does not need to happen in this one.

## Re-review scope (lean)

Send the diff from `7d3f144f…`, the new tree, porcelain and content hashes, the provenance counts,
and the three lanes. No new framework probes are needed unless F1's Generic Host ordering claims
change.

## Hygiene

I only read the target worktree, apart from writing this file, which sits outside the 23-path
manifest. I made no commit, push or issue change.
