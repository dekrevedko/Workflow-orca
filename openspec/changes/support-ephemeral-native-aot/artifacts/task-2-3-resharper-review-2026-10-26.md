# Issue #13 task 2.3 ReSharper triage

InspectCode 2026.2.3.1 completed on the reviewed bytes with build enabled,
Release/net10.0, Platform=Any CPU, suggestion threshold and solution-wide analysis
retained. No CleanupCode or suppression was applied. The main solution scope is
54 explicit source/guard files, including the unchanged public throttle
boundary. The separate temporary consumer solution lists Abstractions, Core,
Ephemeral and NativeMetadata so project references resolve correctly.

| Completed scope | Errors | Warnings | Notes |
|---|---:|---:|---:|
| Main solution | 0 | 581 | 343 |
| NativeMetadata explicit consumer graph | 0 | 28 | 42 |

Counts include linked/multiple-project contexts and are not distinct defects.
Raw SARIF, exact scope/commands, logs and rule counts are retained in ignored
compact receipts. The failed concurrent-build attempt is excluded.

- Qualifier, namespace, naming, braces, using, constructor and collection style:
  preserve surrounding owners and explicit authoritative-registry qualification;
  do not perform broad formatting. Interface-typed golden inputs keep concrete
  List/Dictionary values so compiler collection-expression lowering cannot
  change the fixed runtime allowlist.
- Unused/never-instantiated/return/positional-member notes: external consumers,
  source-generated serialization, internal virtual adapters and closed generic
  helpers are executable evidence, not dead members. JSON descriptor members,
  deliberate counters, no-op setter and fluent returns remain intact.
- Covariant expression argument-array warning: unchanged legacy expression
  construction passes the array read-only to Expression.Invoke; Generated mode
  never enters this factory. The new selected Func path has no expression IL.
- Disposed/modified closure and indentation warnings in the runtime adapter are
  unchanged ownership/timer lines. Timer callbacks acquire lifetime and
  CancellationScope.TryOwn leases; ref-counted release and CancelAsync retain
  physical ownership. Existing ownership tests remain unedited and green.
- The fixture's disposed Barrier capture joins all spawned workers inside its
  using scope. The new ownership fixture joins physical work before provider
  disposal and uses cancellation-notification/settlement barriers, no sleeps.
- Nullable identity checks on partitioner callback properties preserve legacy
  null identity text even for malformed internal records. Catalog null-guard
  and now-unused private stateType argument are harmless, not a new API issue.
- Existing guard multiple-enumeration/span-overload notes concern the unchanged
  corpus/retired-ledger validation. No assertion or guard is relaxed to remove
  a diagnostic.

Separate native analysis still reports IL2090 on public
StepExecutionThrottle.For<TStep>. That is not a ReSharper error; it is the
unapplied catalogue-amendment finding and prevents task 2.3 completion.
