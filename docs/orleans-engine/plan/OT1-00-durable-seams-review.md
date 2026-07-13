# OT1-00: Durable seam review (gate task)

**Difficulty**: Sonnet        **Depends on**: OT0-04
**Spec**: OE-040, OE-042, OE-072        **AC**: none directly (unblocks OE-AC-010 and OE-AC-013 tasks)

## Goal
Harden the four seams most likely to misbehave under retries, restarts, or scale-out
**before** more Orleans behavior lands: (1) cluster-safe start reservation, (2) public
durable command dispatch for event delivery, (3) lifecycle-participant pump hosting,
(4) claimed-timer loss semantics (OE-033: what happens to a wake-up claimed from
`ITimerScheduler` if the claimer dies before the `FireTimer` outcome commits — lease/
reclaim, or lost?). Output: a short findings note + the minimal `Engine.Durable`/`Hosting`
seam proposals for the review gate. Seams (1) and (2) are then **implemented** by OT1-03a
and OT1-01a; seam (4)'s answer feeds the OT2-00 expansion; this task changes **no
production behavior** itself.

## Read first
- `v3-gpt/src/OrcaCore.Engine.Durable/Execution/DurableStartService.cs` — confirm the
  process-local lock/cache; identify what a cluster-wide reservation needs from
  `IWorkflowStartIdempotencyStore` (atomic reserve-or-return-winner? already atomic?)
- The durable event routing/matching component used by `Engine.Durable` for
  `RaiseEvent`-style delivery (locate from `DurableWorkflowRuntime` usings; read ≤2 files) —
  determine whether correlation-targeted resolution and definition-targeted fanout are
  separate public surfaces the Orleans facade can call, or internal
- `v3-gpt/src/OrcaCore.Hosting/Services/OrcaCoreTimerHostedService.cs` — confirm
  `BackgroundService` hosting; identify whether the pump *loop internals* are separable
  from the hosting shell for `ILifecycleParticipant<ISiloLifecycle>` wrapping (OE-072)
- `v3-gpt/src/OrcaCore.Abstractions/Providers/ProviderPorts.cs` — `ITimerScheduler`
  claim contract, plus the provider implementation of claim state (locate in
  `Providers.InMemory` or `Providers.PostgreSql`; read 1 file) — answer seam (4)

## Deliverables
- `docs/orleans-engine/plan/SEAMS.md` — one section per seam (all four): current state
  (file/line), what Orleans needs, smallest viable seam (existing public API / new public
  member / extracted internal), and the recommendation. For seam (4): whether the current
  claim contract can lose a claimed timer, and if so the proposed port-level fix. No code.
- Updated "Assumptions/resolved questions" sections appended to OT1-03 and OT1-04 task
  files reflecting the findings (paths + type names verified to exist).

## Tests to write FIRST
None — analysis/gate task. (The seams get their tests in OT1-03/04 and OT2-01/OT3-01.)

## Implementation notes
- Bias to wrapping over modifying: an `Engine.Durable` public-type change triggers the
  package review gate and certification-suite impact analysis.
- If `IWorkflowStartIdempotencyStore` cannot express atomic reservation, that is a port
  change — flag it explicitly; do not design around a racy check-then-act.

## Out of scope
Implementing any seam; grain code; pump code.

## Definition of done
- [ ] SEAMS.md complete with file:line evidence for all four seams
- [ ] OT1-01a/OT1-03a/OT1-03/OT1-04 assumptions updated with verified type names
- [ ] Review gate sign-off recorded in PROGRESS.md before OT1-01a or OT1-03a starts
- [ ] PROGRESS.md updated; committed as "OT1-00: durable seam review"
