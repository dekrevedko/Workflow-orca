# Issue #13 task 2.2 R1 ReSharper review — 2026-10-25

InspectCode 2026.2.3.1 completed with build enabled, Release/net10.0,
Platform=Any CPU, suggestion threshold, explicit includes and solution-wide
analysis retained. Both completed reports were read before this triage. No
CleanupCode or suppression change was applied.

| Scope | Errors | Warnings | Notes |
|---|---:|---:|---:|
| OrcaCore solution: 20 product and 10 guard/unit source files | 0 | 387 | 172 |
| NativeMetadata consumer: Program, CodecCases, StrongValueCases | 0 | 19 | 30 |

These are raw report counts, including linked-source/multiple-project contexts;
they are not counts of distinct defects. Exact scopes/commands, SARIF, exit
statuses, logs and rule-level triage remain in the compact ignored receipts.

## Findings triage

- Redundant qualifiers/usings and namespace suggestions: retain public OrcaCore
  namespaces and the existing private owners. Absolute qualifiers distinguish
  the authoritative registry from the two linked codec copies. Broad namespace
  or import cleanup adds no behavior proof and is outside this slice.
- Naming, collection expressions, patterns, method groups, constructor/style and
  unused-return suggestions: preserve existing nearby style and exact public
  signatures. A collection-expression rewrite is especially unsuitable for an
  interface-typed golden input: the compiler can choose a runtime collection
  outside the fixed codec allowlist. The fixture now explicitly supplies List.
- Unused members/properties/positional properties: these include JSON-bound
  records and fixture contracts, public fluent returns, independently compiled
  subprocess usage and deliberately unread constructor sentinel fields whose
  initialization counts constructor calls. Removing them would erase intended
  coverage or change contracts. The metadata API's caller is outside the main
  solution, not absent from executable evidence.
- Timer disposed/modified-closure warnings are on unchanged runtime lines. The
  callback acquires both lifetime and CancellationScope.TryOwn leases before
  accessing the source; reference-counted Release disposes only at zero, and
  CancelAsync owns the dispatch. Interlocked/Volatile coordinates the timeout
  flag. The underscore-assignment warning is also pre-existing callback syntax,
  not a new unowned cancellation dispatch. No ownership rewrite was made.
- Consumer disposed-barrier capture: every spawned registration is awaited by
  Task.WhenAll inside the Barrier using block, before disposal. All race controls
  pass through dotnet test/public packages; no sleep or timeout success is used.
- Existing corpus multi-enumeration/shadowing warnings and the C#14 span-overload
  note remain on unchanged guard code. The latter checks a statically typed
  string status array, not a covariant array cast or a runtime product path.
  Exact inventory and every retired declaration still validate without waivers.

## Inspection setup correction and limits

The first standalone-project consumer invocation could not resolve OrcaCore,
Core or Ephemeral project references. It reported 281 cascading errors despite
clean compiler/package builds. Its log/SARIF are retained as an incomplete
project-model attempt, not product failures or a successful inspection. The
final invocation used a temporary explicit solution containing all four projects
and the same three-file include. It resolves every reference and reports zero
errors, without code changes or disabled findings. The temporary solution was
removed after completion.

Static inspection does not establish trimming/AOT support. Actual selected
runtime closure/analyzer remediation and both native hosts remain tasks 2.3/2.4;
whole-source/native approval remains unset. This review does not advance them.
