# Developer-facing interface phased-plan review remediation

**Date:** 2026-07-15

**Review verdict received:** Approve with clarifications
**Remediation status:** Complete

## Outcome

All six review findings were accepted. The two sequencing/traceability traps were corrected,
the repository's hard engine-coverage gate and known test baselines are now explicit, and the
three polish observations became enforceable plan statements rather than implicit assumptions.

## Disposition

| Finding | Disposition | Change |
|---|---|---|
| 1. Management models appear to move before they are canonical | Resolved | Task 6.4 now owns consolidation of duplicate query/statistics/confirmation declarations into one application-tier declaration before package signatures are approved. Task 7.9 builds management roots/selections/handles over those models and deletes any remaining duplicates. The Phase 4 and 6 plan text matches that ownership. |
| 2. Phase 0 red guards conflict with the task-checkbox rule | Resolved | Rule 4 and the Phase 0 exit criteria now define success as a building harness plus a failure for the approved contract reason, recorded in the expected-red ledger. The kickoff prompt uses the same semantics. |
| 3. The 80% engine-coverage CI gate is absent | Resolved | The gate was verified in `CLAUDE.md` and `.github/workflows/ci.yml`. The global phase rules, Phase 10, tasks 10.7/10.10, TDD workflow, kickoff prompt, reviewer template, and report requirements now require the CI-equivalent `+OrcaCore.Engine.*` report and 0.80 minimum whenever production engine code changes. |
| 4. Phase 3 concurrency docs precede final package names | Resolved | Phase 3 now owns semantic/operator guidance and explicitly delegates package/extension-name consistency to Phase 9 after Phases 4 and 8 finalize those names. |
| 5. Plan serialization hides design-permitted parallelism | Resolved | Section 1 records the deliberate choice: isolated phase reviews take priority over throughput. Parallel work is allowed only within a phase when ownership/evidence remain independent and cannot bypass review order. |
| 6. Suite descriptions are not anchored to a known baseline | Resolved | Phase 0 must capture a live baseline. The planning references are 107 passed / 0 failed / 1 skipped (`INT_JS_018`) for integration and 1,218 passed / 0 failed / 1 skipped for the post-fiber verification matrix. Phase 10 reconciles against the live Phase 0 record, not a frozen historical number. |

## Evidence checked

- `CLAUDE.md` records the expected integration baseline and the 80% engine-coverage gate.
- `.github/workflows/ci.yml` collects coverage from the configured unit/acceptance/provider
  projects, generates a report filtered to `+OrcaCore.Engine.*`, and fails below line rate 0.80.
- `tests/OrcaCore.Integration.Tests/README.md` records 107 passed, 1 skipped, 0 failed, with
  `INT_JS_018` as the one-hour soak.
- `docs/review/structured-fiber-execution-implementation-status-2026-07-13.md` records 1,218
  passed, 0 failed, and 1 skipped for the post-fix matrix.

## Validation results

- `openspec validate reshape-developer-facing-interfaces --strict`: passed.
- `openspec validate add-runtime-concurrency-limits --strict`: passed.
- Markdown relative-link validation: 7 changed plan/review/task files passed.
- Task counts: reshape 15/111; concurrency 7/15; no checkbox changed.
- `git diff --check`: passed; only existing line-ending normalization warnings were emitted.
