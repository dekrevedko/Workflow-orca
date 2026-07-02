# 02. Engineering Conventions

Read this before every task. Deviations require an explicit note in the task's PROGRESS line.

## 1. Language & compiler

- .NET 10, `LangVersion` latest (C# 14), `Nullable` enable, `TreatWarningsAsErrors` true,
  `AnalysisLevel` latest, `ImplicitUsings` enable — set once in `Directory.Build.props`.
- Use freely: file-scoped namespaces, records (`record` / `readonly record struct`),
  primary constructors, `required` members, collection expressions `[..]`, pattern matching
  (`switch` expressions with exhaustive handling), `init` setters, `field` keyword where it
  simplifies, `params` collections, target-typed `new`.
- Avoid: unsafe code, reflection (except the single explicit spot in serializer wiring),
  dynamic, preprocessor directives, partial classes (except source-generator targets).

## 2. Types and API design

- **Interfaces first**: every seam a consumer touches is an interface in Abstractions or an
  internal interface in the owning module. Concrete classes are `internal sealed` unless a
  documented reason exists. Constructors take interfaces; `new` of a collaborator inside a
  class body is a smell (factories/DI instead).
- **Closed hierarchies for results/events**: `abstract record` base + `sealed record`
  variants (e.g. `StepResult.Completed/Failed/WaitForEvent/Yield`). The consumer `switch`
  must be exhaustive — add a `_ => throw new UnreachableException()` arm only where the
  compiler cannot prove exhaustiveness.
- **Immutability**: contracts, snapshots, envelopes, definitions are immutable records.
  Mutable state lives only inside engine internals guarded by the instance lane.
- **Functional primitives** (spec PR-050): `Result<T>` for expected operational outcomes
  (routing, command decisions, append outcomes), `Option<T>` for absence-without-failure
  (lookups), `Validation<T>` for accumulated build-time errors. Public happy-path APIs may
  throw `OrcaCore*Exception` types instead — never force `Result` chains on end users.
  Never nest `Task<Result<Option<T>>>`; split the method.
- IDs are strongly-typed readonly record structs (`InstanceId`, `EventId`, `WaitId`,
  `CommandId`, `PoolName`…) wrapping `Guid`/`string` — no bare primitives across seams.

## 3. Async rules

- All potentially-working public APIs: `async`, suffix `Async`, take `CancellationToken`
  (last parameter, no default in internal code; `default` allowed on public facade).
- `ValueTask` only on measured hot paths; otherwise `Task`.
- No `async void`; no `.Result`/`.Wait()`/`GetAwaiter().GetResult()`; no `Task.Run` in
  library code (the host owns threads); `ConfigureAwait(false)` everywhere in `src/`.
- Time only via injected `TimeProvider`; delays via `timeProvider`-aware mechanisms so tests
  control the clock. Randomness only via injected seams. (Determinism — spec NF-020.)

## 4. Naming & layout

- One public type per file; file name = type name. Folder = module.
- `OrcaCore.<Project>.<Module>` namespaces mirror folders.
- Options records end in `Options`; ports start with `I` and end in the role
  (`IWorkflowEventStore`); fakes in TestSupport are `Fake<PortName>`.
- No abbreviations in public API (`definition`, not `def`).

## 5. Errors and logging

- Exception taxonomy (Abstractions): `OrcaCoreException` base →
  `WorkflowDefinitionException` (build/validation), `WorkflowConcurrencyException`,
  `WorkflowRoutingException` (no-match/ambiguous — clear messages are an acceptance
  requirement, EV-012), `WorkflowLifecycleException` (illegal trigger, CR-030),
  `WorkflowVersionException` (DU-041). Messages must state what the caller should do.
- Logging via `ILogger<T>` abstractions with source-generated `LoggerMessage` definitions;
  no string interpolation in log calls; no logging in Abstractions.

## 6. Comments & docs

- XML docs on all public contracts: state the *contract* (guarantees, failure modes,
  threading), not the implementation.
- Inline comments only for non-obvious constraints ("commit must precede mailbox removal —
  EV-032"), citing spec IDs. No narrative/change-log comments.

## 7. Git hygiene (per task)

- One task = one commit (or a small series); message: `T1-04: builder validation
  accumulates errors (CR-002)` — task id first, spec IDs in parentheses.
- Never commit failing tests, commented-out code, or TODOs without an owner task id.
