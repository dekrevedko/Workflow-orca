# OT1-06: Deactivation, rehydration, and grain-free queries

**Difficulty**: Haiku        **Depends on**: OT1-04
**Spec**: OE-022, OE-050, OE-051, OE-052        **AC**: OE-AC-020, OE-AC-003

## Goal
Prove "memory is a cache": a deactivated waiting instance resumes correctly from durable
state on the next call, and management queries never wake grains up.

## Read first
- `tests/OrcaCore.Engine.Orleans.Tests/Grains/WaitResumeTests.cs`
- Orleans deactivation API surface: `IGrainManagementExtension`/`DeactivateOnIdle` via
  TestingHost (no repo file; use the TestingHost docs pattern already proven in fixtures)
- [02-requirements.md](../02-requirements.md) §2.6

## Deliverables
- `WorkflowInstanceGrain`: add explicit `DeactivateOnIdle()`-friendly behavior only if a
  test demands it (expected: none — the grain is stateless between turns by design).
- Test-support helper in the Orleans test project: activation counter (via
  `Orleans.Runtime.IManagementGrain.GetGrainActivationCount`-style API or silo statistics)
  used by both tests.

## Tests to write FIRST
In `tests/OrcaCore.Engine.Orleans.Tests/Grains/RehydrationTests.cs`:
1. `DeactivatedWaitingInstance_ResumesOnEvent` — `[Trait("AC","OE-AC-020")]` — start →
   Waiting → force deactivation of the activation → `RaiseEventAsync` → completes; stream
   shows a single continuous history (no re-run of pre-wait steps).
2. `Queries_DoNotActivateGrains` — `[Trait("AC","OE-AC-003")]` — with zero activations for
   a waiting instance, run status/wait queries through projections → results correct AND
   activation count for the grain type unchanged.
3. `NoCriticalWorkInDeactivate` — code-shape guard: `WorkflowInstanceGrain` overrides
   `OnDeactivateAsync` either not at all or with best-effort-only content (assert via
   reflection that no override exists — simplest honest check).

## Implementation notes
- If queries currently route through the facade into grains anywhere, fix the facade to hit
  `IWorkflowProjectionStore` directly — that is in scope here (OE-052).

## Out of scope
Silo restart (OT2-02), activation-collection tuning (OT4-01).

## Definition of done
- [ ] All listed tests green; solution builds zero-warning
- [ ] Phase O1 exit check: OE-AC-001, OE-AC-002, OE-AC-003, OE-AC-010, OE-AC-011,
      OE-AC-012, OE-AC-013, OE-AC-020 all green
      (`dotnet test --filter "AC~OE-AC"` or trait-filtered run)
- [ ] Record OOQ-2 recommendation (keep or bypass lane) in PROGRESS.md for the phase gate
- [ ] PROGRESS.md updated; committed as "OT1-06: rehydration + grain-free queries (OE-AC-020, OE-AC-003)"
