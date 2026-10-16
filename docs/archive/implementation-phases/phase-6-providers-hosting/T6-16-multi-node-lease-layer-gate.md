# T6-16: Gate optional multi-node lease layer

**Difficulty**: Sonnet        **Depends on**: T6-15
**Spec**: DU-060        **AC**: AC-315

## Goal
Only run this task if the owner explicitly chooses to pursue multi-node execution in this
implementation run. The task defines the lease model and follow-up implementation split
before any code changes.

## Read first
- `docs/specs/06-requirements-durable-execution.md`
- `docs/specs/12-acceptance-criteria.md`
- `docs/implementation/01-solution-architecture.md`
- `src/OrcaCore.Abstractions/Providers/ProviderPorts.cs`
- `src/OrcaCore.Engine.Durable/Execution/DurableCommandProcessor.cs`
- Spec: `docs/specs/12-acceptance-criteria.md` AC-315

## Deliverables
- If pursued, add a design note under `docs/multi-node-leases.md`
- If pursued, add follow-up implementation task files under `docs/implementation/phases/phase-6-providers-hosting/`
- If not pursued, append a PROGRESS.md line recording that AC-315 is out of scope for this run

## Tests to write FIRST
No product tests. This is an optional owner-decision gate and task-splitting task.

## Implementation notes
Do not weaken single-host correctness. Any lease design must preserve expected-version
append semantics and one logical mutator per instance.

## Out of scope
Implementing leases, changing provider ports, and running AC-315 tests before the owner
chooses to pursue multi-node execution.

## Definition of done
- [ ] Owner decision recorded in PROGRESS.md
- [ ] If pursued, follow-up task files are explicit and path-safe
- [ ] If not pursued, Phase 6 exit notes say AC-315 is not part of this run
- [ ] PROGRESS.md updated; committed as "T6-16: gate multi-node lease layer"
