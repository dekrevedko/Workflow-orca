# OT3-00: Expand Phase O3 task index (distribution & pumps)

**Difficulty**: Sonnet        **Depends on**: Phase O2 exit (review gate passed)
**Spec**: OE-040, OE-052, OE-071, OE-072; DU-031/032 unchanged        **AC**: OE-AC-030, OE-AC-031, OE-AC-043, OE-AC-045

## Goal
Turn the Phase O3 index entries in [plan/README.md](README.md) (OT3-01…OT3-05) into full
task files per the protocol template, accurate against the current code. OT3-05 (production
clustering spike) is the input to the OOQ-4 pre-production gate at this phase's exit —
its task file must state the exact packages (AdoNet clustering pinned to the same Orleans
version), the membership DB artifacts to provision, and the client configuration to validate.

## Read first
- [plan/README.md](README.md) Phase O3 index
- The existing outbox pump and its hosting registration (locate under `Engine.Durable`
  `Outbox/` and `Hosting`; read ≤3 files)
- The durable management operations surface (`Engine.Durable/Management/`; read ≤2 files)
- `tests/OrcaCore.Engine.Orleans.Tests/Testing/OrleansClusterFixture.cs` — extend to
  2-silo clusters here if not already parameterized

## Deliverables
- `OT3-01…OT3-05` task files in this folder, sized within protocol limits, `OE-AC` traits
  and DoD commands per task.

## Implementation notes
- Outbox pump: hosting-only work — the pump itself must not change; dispatch guarantees are
  already certified.
- Cross-silo test must force placement spread (e.g. many instances + activation-count
  assertion per silo) rather than hoping for it.
- Management operations that mutate instances go through grains (they are commands);
  queries stay projection-only.

## Out of scope
Executing any OT3 task (including the OT3-05 clustering spike itself — this task only
writes its task file). Production clustering is decided **in this phase** at the OOQ-4
gate; only its full e2e exercise remains in Phase O5.

## Definition of done
- [ ] All OT3 task files exist, paths verified; expansion reviewed against
      [02-requirements.md](../02-requirements.md) §2.5–2.6 before execution
- [ ] PROGRESS.md updated; committed as "OT3-00: expand Phase O3 tasks"
