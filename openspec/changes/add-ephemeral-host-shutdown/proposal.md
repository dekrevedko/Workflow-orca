## Why

Issue #2 needs a supported cooperative stop-and-join for ephemeral hosts, including
starts still running before they return a handle. Per-instance ownership from issue
#1 does not close host admission, join entered callbacks or protect application DI
ownership when a stop observation is incomplete.

## What Changes

- Add exactly the seven engine-owned public types in
  `artifacts/contract-public-api-2026-10-18.md`: one singleton lifetime interface,
  a closed Completed/Incomplete result, one three-value reason enum and two typed
  operational exceptions. Its sole method is
  `ValueTask<EphemeralHostShutdownResult> StopAsync(TimeSpan timeout, CancellationToken cancellationToken = default)`.
- Separate irreversible Closed and Stopping phases. ApplicationStopping closes
  new starts/registrations without cancelling anything; accepted work drains with
  ordinary queries, waits, commands, ingress, timers and retries. Only the engine's
  hosted StopAsync or direct StopAsync starts cancellation/join of remaining work.
  Preserve pre-handle ownership: initialized pending starts can be accepted during
  Closed, and acceptance/abort is elected only at Stopping (F2).
- Use one fixed cooperative-cancel policy. No new host option, role fingerprint,
  registration overload, hosted-service toggle, dependency, provider or friend is added.
- Replace the readiness hosted service's no-op stop with the same lifetime join;
  ApplicationStopping closes admission without running application code inline.
  Deadlines and observer cancellation report Incomplete while cleanup continues.
- Require one application disposal owner to retain the root/scopes/dependencies on
  Incomplete. Qualify explicit Generic Host and plain-DI ownership; ordinary
  RunAsync's unconditional final disposal cannot protect noncooperative work.
- Specify ordered Generic Host and app-controlled plain-DI drain recipes, including
  the shared host budget and concurrent-stop caveat. After Stopping begins, seal
  live instance operations; after Completed allow only passive identity/error checks
  and detached cached data, with no DI/user work, timers or commits. Preserve
  original-ID not-found precedence and previously detached caller values.
- Document RunAsync's new incomplete-stop exception and continuing disposal finally.
  Schedule issue #1 retention archival/provenance refreeze only in a later target.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `developer-facing-surface`: two ADDED requirements for the exact stop/join
  service and DI/Generic Host ownership protocol.
- `management-and-querying`: four ADDED requirements for close admission,
  host-wide physical quiescence, truthful incomplete observations and repeated stop.

## Impact

The exact proposed namespace is `OrcaCore.Hosting`, owned solely by
`OrcaCore.Engine.Ephemeral`. Shutdown is a host-lifetime operation; it adds no
workflow management retry, pause, resume, archive or purge command. Durable modes,
the nine-friend boundary, authoring companion and current public API/compile/package
baselines stay unchanged in this contract target.

`design.md` and `artifacts/contract-transition-routing-2026-10-18.md` enumerate the
future numbered/canonical/guide targets. The contract includes all provenance
prerequisites with six pending operations and semantic approval false. Existing
canonical blocks and earlier approval evidence remain byte-exact. A green process
lane validates that pending state; it does not authorize canonical or source work.

Base: GitHub-synced master `fa88986aaac219c998b15e96748efdf2f245de51`.
Contract R1 is approved at checkpoint 3c8d33c, verdict-only child 4bd7c3e. This
canonical transition applies the approval-required F2 correction and awaits its
own independent review; source remains unimplemented. The seven declarations stay
unchanged; current corrected catalogue: `artifacts/task-2-1-public-api-2026-10-19.md`. The initial
assessment is retained as dated history; this target supersedes its unresolved
design decisions and unrefrozen-inventory status only.
