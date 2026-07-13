# OT5-00: Expand Phase O5 task index (certification & e2e)

**Difficulty**: Sonnet        **Depends on**: Phase O4 exit (review gate passed)
**Spec**: OE-023, OE-080, OE-081, OE-082        **AC**: OE-AC-050, OE-AC-051, OE-AC-052, OE-AC-060

## Goal
Turn the Phase O5 index entries in [plan/README.md](README.md) (OT5-01…OT5-06) into full
task files, including the concrete list of durable and DR acceptance ids that form the
OE-081 parity subset (engine-observable semantics only).

## Read first
- [plan/README.md](README.md) Phase O5 index
- `tests/OrcaCore.Acceptance.Tests/` — how AC trait tests and shared fixtures are
  organized (read the fixture + 1 representative test file)
- `tests/OrcaCore.Integration.Tests/` — Testcontainers PostgreSQL pattern (read 1 file)
- [03-acceptance-criteria.md](../03-acceptance-criteria.md) OE-AC-050 and OE-AC-051

## Deliverables
- `OT5-01…OT5-06` task files, sized within protocol limits.
- In OT5-01: the explicit parity-subset table (durable AC id / DR-AC id → include/exclude
  → reason) reviewed at the gate before execution. Include document-16 engine-observable
  criteria such as restart-safe continuation, stale continuation no-op, stateful wait, and
  segment-budget yield where Orleans rehosts the durable interpreter.
- In OT5-02: apply the OOQ-4 resolution recorded at the Phase O3 gate (clustering provider,
  packages, membership artifacts) — resolved there, not here.
- In OT5-06: the load profile (rates, mix, duration), the latency/tail/I-O metrics and
  thresholds that satisfy OE-AC-060, and the data handed to the OOQ-6 decision.

## Implementation notes
- The capstone tasks must reuse the driving-scenario workflow definition from the durable
  acceptance suite if one exists; otherwise define it once in TestSupport and share.
- Chaos task (OT5-05) budgets: bounded runs, deterministic seeds where possible, marked
  `[Trait("Category","Chaos")]` so CI can schedule it separately.

## Out of scope
Executing any OT5 task; publishing/packaging (main program Phase 6 concern).

## Definition of done
- [ ] All OT5 task files exist, paths verified; parity subset reviewed; OT5-02 applies the
      OOQ-4 resolution already recorded at the Phase O3 gate (no new proposal here)
- [ ] PROGRESS.md updated; committed as "OT5-00: expand Phase O5 tasks"
