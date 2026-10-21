# Section 3 implementation decisions

Canonical authority is 2e51fbd / 6253f6c, bound by the schema-18 registry and Git
checks. This source candidate has no independent source approval yet.

- One root ledger shares the existing visibility gate. Closed rejects start,
  replay and compatible registration; initialized starts can still be accepted.
  Only the engine/public stop enters Stopping. All live-operation and attempt
  gates consult it, while immutable metadata errors retain precedence.
- Provisional publication, a live instance, every physical/continuation handoff,
  API pin, lane lifetime, entered timer/sweep callback and cancellation dispatch
  holds an obligation. Successors acquire ownership before predecessor release.
  The join never snapshots replaceable `WhenSettled` tasks.
- The ephemeral lane owns its queue through eviction and its final notification.
  Its existing channel semantics remain local to the ephemeral assembly; Core and
  durable source are unchanged. A failing enqueue notification still observes the
  already transferred item, preserving its error and allowing a newly initialized
  instance to be fenced instead of abandoned.
- Stopping closes producers before cancelling/fencing instances. Producer cleanup
  failures retain a fault obligation and still attempt other cleanup/cancellation.
  An unaccepted start retains publication through its own return/error decision;
  a worker fence cannot retire it while that path still uses the runtime.
- Root host-cancellation translation is tied to the admitted execution token or
  the sealed-attempt gate. An unrelated authoritative OperationCanceledException
  retains its original token when host stop races it. The pre-handle matrix also
  covers propagation through step-timeout child tokens.
- `CancelAsync` dispatch is asynchronous and owned, including linked propagation
  and handler return. Each workflow retains its first dispatch task. A throwing
  returned handler is diagnosed and settles; repeated cancellation cannot replace
  a still-running first dispatch with a misleading completed task. Step timeout
  cancellation uses the same contained path and wakes logical timeout independently
  of a blocked handler. Retired units dispose their workflow cancellation source.
- The required load run exposed a cancellation-source disposal race: disposing a
  source immediately after `CancelAsync` can discard queued handlers. Step sources
  are now reference-counted across the logical turn, physical attempt, entered
  timer and asynchronous dispatch. Final disposal occurs before the corresponding
  lifetime obligation is released; a logical timeout cannot silently lose a handler.
- Observers themselves never self-join. Dependency-using clock setup and timer
  disposal are owned work. Active finite observer timers transfer exactly once to
  cleanup jobs before stable zero; this prevents clock disposal from running after
  `Completed`, losing a thrown cleanup failure, or keeping an observer waiting for
  itself. No completed observer/token/result history is stored. Cached Completed
  observes validation and disposal precedence without clock access.
- Host application-lifetime dependencies and their token are resolved before owned
  timer allocation. The actual host ledger is attached to retention before its first
  timer is armed, so a sweep entering during construction is already owned.
- Clock methods and retirement cleanup execute outside admission locks. Retention
  compares monotonic timestamp durations using a captured frequency and decimal
  division, preserving age/count order without invoking virtual clock members or
  overflowing age/frequency products under the gate. Null policy and fingerprints
  are unchanged. Sealed stale timer trampolines only gate-check and return.

The new runtime tests use real production paths, explicit notifications/barriers
and fake clocks. Test-first records include behavior assertions for missing
shutdown completion, provisional-return ownership, clock setup failure, enqueue
failure and observer cleanup failure. Early harness/build corrections and deliberate
API/source candidate capture failures are separate from these behavior-red records.
No CI-red source checkpoint was committed.

The ordinary package consumer covers all three ownership recipes, real root/scoped/
external dependency sentinels, pending starts, local wait cancellation, fresh roots,
Incomplete retention and the concurrent-stop caveat. Its application-event case
and the separate production-router unit coverage are distinguished in its README.
Source review and publication remain separate gates.
