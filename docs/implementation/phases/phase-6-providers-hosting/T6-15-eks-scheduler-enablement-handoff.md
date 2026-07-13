# T6-15: Write EKS scheduler enablement handoff

**Difficulty**: Sonnet        **Depends on**: T6-14
**Spec**: JS-003, JS-004, DU-053        **AC**: JS-AC-008

## Goal
Document the boundary between OrcaCore and the scheduler application repo. The handoff must
cover Kubernetes dispatcher/watcher adapter contracts, scheduled occurrence idempotency via
`StartOrGet`, and which JS acceptance criteria belong to the app.

## Read first
- `docs/specs/14-driving-scenario-eks-job-scheduler.md`
- `docs/specs/10-provider-model-and-extensibility.md`
- `docs/specs/06-requirements-durable-execution.md`
- `src/OrcaCore.Abstractions/Providers/ProviderPorts.cs`
- `src/OrcaCore.Engine.Durable/Execution/DurableStartService.cs`
- Spec: `docs/specs/12-acceptance-criteria.md` JS-AC-008

## Deliverables
- Add `docs/eks-scheduler-handoff.md`
- Add or update public XML docs only if existing public APIs need clearer `StartOrGet` occurrence-key guidance
- Add documentation tests if the repo already has a docs-check pattern

## Tests to write FIRST
No product tests unless XML documentation changes introduce examples that compile.

## Implementation notes
Keep Kubernetes concepts out of workflow definitions. The library owns normalized outbox and
inbox contracts; the scheduler app owns cron triggering, Kubernetes configuration, and
tenant policy.

## Out of scope
Implementing Kubernetes adapters, adding a cron engine, and changing durable pool semantics.

## Definition of done
- [ ] Handoff doc explains JS-003 and JS-004 boundaries
- [ ] Handoff doc specifies deterministic occurrence-key shape for JS-AC-008
- [ ] `dotnet build OrcaCore.slnx` passes with zero warnings if XML docs changed
- [ ] PROGRESS.md updated; committed as "T6-15: eks scheduler enablement handoff"
