# OT2-00: Expand Phase O2 task index (timers & durable waits)

**Difficulty**: Sonnet        **Depends on**: Phase O1 exit (review gate passed)
**Spec**: OE-031, OE-032, OE-033, OE-012, OE-053, OE-072        **AC**: OE-AC-021, OE-AC-022, OE-AC-023, OE-AC-045, OE-AC-046

## Goal
Turn the Phase O2 index entries in [plan/README.md](README.md) (OT2-01…OT2-04) into full
task files using the template in
[implementation/04-task-protocol.md](../../implementation/04-task-protocol.md), accurate
against the code that now exists.

## Read first
- [plan/README.md](README.md) Phase O2 index
- `docs/orleans-engine/plan/SEAMS.md` — the pump-hosting seam decision from OT1-00
- `v3-gpt/src/OrcaCore.Abstractions/Providers/ProviderPorts.cs` — `ITimerScheduler`
  claim contract
- `v3-gpt/src/OrcaCore.Hosting/Services/OrcaCoreTimerHostedService.cs` — the
  `BackgroundService` hosting that must NOT be reused as-is (OE-072): wrap the pump loop
  internals in an `ILifecycleParticipant<ISiloLifecycle>` starting at
  `ServiceLifecycleStage.Active`
- `v3-gpt/src/OrcaCore.Engine.Orleans/Hosting/OrcaCoreOrleansSiloExtensions.cs`

## Deliverables
- `OT2-01…OT2-04` task files in this folder, each obeying sizing rules (split any entry
  that exceeds them; append a letter suffix to the task id if a split is required).
- Each task file lists exact "Read first" paths verified to exist, tests-first lists with
  `OE-AC` traits, and DoD commands.

## Implementation notes
- Reuse-first: the timer pump should wrap the existing pump/claim machinery, changing only
  the delivery target (grain call instead of lane). If the existing pump is not reusable
  as-is, the expansion documents the smallest public seam and flags the review gate.
- **OE-033 hard entry gate**: start from the seam (4) answer in SEAMS.md (OT1-00 already
  determined whether a claimed-but-undelivered timer can be lost) and re-confirm it against
  the code as it now stands. Phase O2 timer-pump implementation cannot start until the
  provider contract proves lease/reclaim or retry-until-commit durability. If the answer
  was/remains "it can be lost", the port/provider fix approved at the O1 gate must land as
  the first OT2 task before OT2-01 — do not paper over it with pump-local retries that die
  with the process.
- OOQ-1 (reminders) stays open — no task may introduce `Microsoft.Orleans.Reminders`.

## Out of scope
Executing any OT2 task; resolving OOQ-1.

## Definition of done
- [ ] All OT2 task files exist, sized within protocol limits, every path verified
- [ ] Expansion reviewed against [02-requirements.md](../02-requirements.md) §2.4 before
      execution starts (review gate)
- [ ] PROGRESS.md updated; committed as "OT2-00: expand Phase O2 tasks"
