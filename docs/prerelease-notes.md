OrcaCore `0.1.0-preview.2` for .NET 10. This prerelease includes the independently
reviewed ephemeral retention and shutdown changes, and the native publishing
assessment from issues [#1](https://github.com/dekrevedko/Workflow-orca/issues/1),
[#2](https://github.com/dekrevedko/Workflow-orca/issues/2), and
[#3](https://github.com/dekrevedko/Workflow-orca/issues/3).
APIs, provider schemas, and persistence formats may change.

## Ephemeral terminal retention

- Hosts can opt in through
  `EphemeralEngineHostOptions.TerminalRetention = EphemeralTerminalRetentionPolicy.Create(...)`.
  The default remains `null`, retaining the existing host-lifetime behavior.
- Count and age limits apply only after terminal instances have physically settled.
  Retirement waits for work and callbacks to settle and for query/output-wait pins to release.
  Retirement removes the instance and its start binding together.
- A result can retire between a start returning and the caller beginning observation.
  Maximum age is not a minimum retention guarantee. After retirement, observation
  returns the normal not-found result; reusing an expired start key can execute the job again.

## Cooperative ephemeral shutdown

- `IEphemeralWorkflowHostLifetime.StopAsync(timeout, cancellationToken)` reports
  `Completed` or `Incomplete` with a reason and pending-work count. Cancellation of
  the caller's observation does not cancel the shared shutdown operation.
- Generic Host's `ApplicationStopping` closes admission without cancelling accepted
  workflows. Initialized pending starts can finish and become accepted during this
  drain phase. Existing queries, waits, commands, events, timers and retries keep working.
- The engine's hosted stop or a direct `StopAsync` begins cancellation and joins the
  remaining physical work. Only `Completed` permits the actual owner to dispose
  dependencies safely; `Incomplete` is not proof that work has stopped.
- Hosted stop throws `EphemeralHostShutdownIncompleteException` for an incomplete
  shutdown, potentially within an aggregate exception. Applications using `Run` or
  `RunAsync` may now observe a shutdown exception where the old no-op stop returned
  cleanly. Those helpers still dispose in `finally`: an application that must retain
  dependencies after an incomplete stop needs an explicitly owned host or provider.
- The [engine guide](https://github.com/dekrevedko/Workflow-orca/blob/master/docs/ephemeral-engine-developer-guide.md)
  documents ordered Generic Host drain, an explicitly owned host, and plain-DI
  submission-gate recipes. Inspect existing stuck-step diagnostics before stopping;
  the incomplete result reports a count rather than individual stuck instances.

## Native publishing assessment

- The independently reviewed public-package consumer passed all 16 JIT scenarios on
  native Windows Server 2025 and Ubuntu 24.04. This is compatibility evidence for the
  exact tested profile, not a library-wide deployment support promise.
- Trimmed and Native AOT profiles are **unsupported**, including the separately
  labelled runs with JSON reflection explicitly enabled. Default builds fail at the
  first registration with a schema error that hides disabled JSON reflection.
  Reflection-enabled runs expose further constructor/property metadata failures.
- Unsupported profiles have unavailable performance/memory measurements, not zeroes.
  No codec replacement, blanket warning suppression or AOT product remediation is included.
  The [assessment](https://github.com/dekrevedko/Workflow-orca/tree/master/tools/ephemeral-native-assessment)
  contains the reproducible harness and links to the native evidence.

## Install and remaining limitations

The assets contain all twelve `.nupkg` packages and `SHA256SUMS`. Install the desired
engine role at version `0.1.0-preview.2` from NuGet.org after its publication check
succeeds, or download the complete asset set into a directory feed. Reference
`OrcaCore.Engine.Ephemeral`, or `OrcaCore.Durable.Hosting` together with the chosen
provider. The GitHub release URL itself is not a NuGet feed. See the
[installation guide](https://github.com/dekrevedko/Workflow-orca/blob/master/docs/packages.md).

Durable DAG execution remains incomplete. Closing the three issues and publishing
these packages does not complete its implementation gates or qualify a stable release.
