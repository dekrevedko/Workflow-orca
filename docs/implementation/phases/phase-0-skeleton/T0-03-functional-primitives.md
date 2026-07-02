# T0-03: Functional primitives — Result, Option, Validation

**Difficulty**: Haiku        **Depends on**: T0-01
**Spec**: PR-050        **AC**: none (foundation for many)

## Goal
Implement the three (and only three) functional primitives in `OrcaCore.Abstractions`,
exactly per spec PR-050: `Result<T>` (expected success/failure), `Option<T>`
(present/absent), `Validation<T>` (accumulated errors). These are used pervasively later;
their contracts must be boringly solid.

## Read first
- Spec: [specs/10-provider-model-and-extensibility.md](../../../specs/10-provider-model-and-extensibility.md) §10.6 (PR-050)
- [02-engineering-conventions.md](../../02-engineering-conventions.md) §2

## Deliverables
In `v3/src/OrcaCore.Abstractions/Primitives/`:
- `Result<T>` — `readonly record struct`; factories `Success(value)` / `Failure(error)`
  where error is `OrcaCoreException`; members `IsSuccess`, `IsFailure`, `Value` (throws
  `InvalidOperationException` when failure), `Error`; combinators `Map`, `Bind`, `Match`.
- `Option<T>` — `readonly record struct`; `Some(value)` / `None`; `HasValue`, `Value`
  (throws when none); `Map`, `Bind`, `Match`, `GetValueOrDefault(fallback)`.
- `Validation<T>` — `sealed record` with `Value` and `IReadOnlyList<ValidationError>`;
  `Valid(value)` / `Invalid(errors)`; `IsValid`; combine/merge; `ValidationError`
  (`Code`, `Message`, optional `Path`).
- `OrcaCoreException` base type in `v3/src/OrcaCore.Abstractions/Errors/`.

## Tests to write FIRST
In `v3/tests/OrcaCore.Core.Tests/Primitives/` (referencing Abstractions):
1. `Success_ExposesValue_AndIsSuccess`
2. `Failure_ExposesError_AndValueThrows`
3. `Map_OnSuccess_Transforms` / `Map_OnFailure_PropagatesError`
4. `Bind_ChainsResults_ShortCircuitsOnFailure`
5. `Match_InvokesExactlyOneArm` (both variants)
6. `Some_HasValue` / `None_ValueThrows` / `None_GetValueOrDefault_ReturnsFallback`
7. `OptionMap_OnNone_StaysNone`
8. `Valid_IsValid_NoErrors`
9. `Invalid_CollectsAllErrors_PreservesOrder`
10. `Combine_TwoInvalids_MergesErrorLists`
11. `DefaultStructs_AreSafe` — `default(Result<T>)`/`default(Option<T>)` behave as
    failure/none, never as a torn "success with null"

## Implementation notes
- No LINQ-style `SelectMany` sugar, no async combinators, no implicit conversions —
  the spec caps the surface deliberately. Test 11 drives the constructor/`default` design.

## Out of scope
- Any use of the primitives elsewhere; exception taxonomy beyond the base type.

## Definition of done
- [ ] All listed tests green; placeholder SkeletonTests removed from touched projects
- [ ] `dotnet build v3/OrcaCore.slnx` — zero warnings; Abstractions still references nothing
- [ ] PROGRESS.md updated; committed as "T0-03: functional primitives (PR-050)"
