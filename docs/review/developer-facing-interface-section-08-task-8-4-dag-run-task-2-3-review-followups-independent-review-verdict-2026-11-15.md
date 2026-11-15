# DAG run Task 2.3 review follow-up independent review verdict

**Date:** 2026-11-15
**Reviewer:** independent review
**Scope reviewed:** the 10-entry freeze in the main checkout `X:/Projects/GitHub/Workflow-orca`
(branch `fix/dag-task-2-3-review-followups`) on parent `bc52d7e674467c744964621cd3883c799669e983`
(tree `0985f6df…`), named by
`developer-facing-interface-section-08-task-8-4-dag-run-task-2-3-review-followups-dirty-manifest-2026-11-15.txt`.
It adds two shipped-host scheduler tests and one child-output schema test, refactors the existing
scheduler test onto shared helpers, refreshes the declaration inventory and its Task 7.20 wording,
and records the deferred-retry decision for round-105 finding F3.

**Authorization requested:** checkpoint of this exact target and its direct-child evidence.
There is no activation.

This verdict authorizes that checkpoint and the evidence child. It authorizes no task checkbox,
no Task 8.6 work and no forward source work.

## Summary

The follow-up closes round-105 findings F1 and F2 with genuine regression tests, and resolves F3
with a reasoned decision to keep the current lease. Only tests, accounting and review metadata
change. No defect was found.

- **Round-105 chain.** Checkpoint `e955f3a` has parent `048c3a6` and tree `463b7e62…`, the tree I
  approved. Its only direct child `bc52d7e` adds my verdict byte-exact (`0ab955ba…`, 11,656 B) and
  clears the freeze.
- **Scope.** No file under `src/`, `openspec/specs/` or the DAG change differs from the parent.
  Product source is therefore byte-identical to the checkpoint qualified on PostgreSQL and SQL
  Server in round 105.
- **One-slot authored order (F1).** Two independent roots with `MaxConcurrentNodes = 1` on the
  shipped host: the first mapper invoked is ordinal 0, and admission records are `0, 1`.
- **Stale higher receipt (F1).** Roots 0 and 2 start Ready, and node 1 depends on 0, with two slots.
  - Mapper 2 can only start after node 0 has been admitted, because the scheduler runs one
    evaluation or admission per run at a time.
  - The test holds child 0 in a gated step, waits until the real join makes node 1 durably Ready
    (`WaitForChangeAsync`, no sleeps), then releases mapper 2.
  - The stale result 41 is discarded. Mapper order is `0, 2, 1, 2`, admission is `0, 1, 2`, and
    node 2 commits 42.
- **Child output schema (F2).** A genuine child completes with 42. Only the terminal checkpoint's
  output `SchemaIdentity` is rewritten. The shipped pump acknowledges nothing and poisons the join
  as `DAG_PROTOCOL_INVALID`. The run becomes `IntegrityFault` with node 0 still `StartAuthorized`,
  no output, and exactly one appended `IntegrityFaulted` record.
- **Deferred retry (F3).** Accepted as decided:
  - Shortening the shared claim lease would also shorten ownership of in-flight dispatch.
  - Immediate release reintroduces the starvation.
  - A shorter defer needs provider-authoritative retry eligibility with its own cross-host and
    all-provider controls.
  - The retry bound is the remaining lease plus pump cadence.
- **Accounting.** Three new active declarations: 1,612 → 1,615 physical and 924 → 927 active, with
  file counts unchanged. The Task 7.20 checkbox and the retired-declaration ledger are unchanged.

## Freeze anchors

| Anchor | Claimed | Reproduced |
|---|---|---|
| Raw porcelain / manifest | self-inclusive, raw order | 10 lines, 830 B, `508b4023…`; byte-equal to raw and NUL-form porcelain; LF, no BOM; nothing staged |
| Semantic record | 6 rows, 809 B, `6ddccac9…` | identical |
| Active record | fixture-bound | 9 rows, 1,384 B, `c44ca6b9…`, equal to the fixture's `activeFreeze` (base `bc52d7e`) |
| Status all-file record | not claimed | 10 rows, 1,539 B, `33f5a985…` |
| Simulated checkpoint | promised in a handoff, none found | tree `7718e29d638d8b2ace2bc40c6e51e0b94577b20c` from a copied index and from a committed disposable copy; parent `bc52d7e`; 2 A / 8 M; every blob equals the raw bytes |

The request and manifest catalog rows (5,872 B `34ae0fb3…`, 830 B `508b4023…`) match the files.
All anchors were rechecked unchanged after validation.

## Validation

SDK 10.0.403, plain `-warnaserror`, disposable detached copy on `E:`. Container lanes were not
rerun because product source is unchanged since round 105.

| Lane | Result |
|---|---|
| Debug and Release non-incremental `-warnaserror` builds | 0 warnings, 0 errors |
| Exact package feed / package fixtures / compile fixtures | 12 packages / green, exactly `dag-hosting` red / green |
| Core / Ephemeral / Durable / Acceptance / Hosting / ProviderCertification | 390 / 405 / 113 / 37 / 53 / 116 |
| Full guard project, frozen state | 319 passed plus exactly 14 expected-red (333) |
| `Disposition=Infrastructure`, Release | 319/319 |
| `Disposition=ExpectedRed` | exactly 14, all scenario guards |
| Strict OpenSpec / `git diff --check` | 26/26 / clean |
| Guards on the committed simulation | 319 passed plus the same 14 expected-red |
| Evidence state: committed simulation with the freeze cleared | Infrastructure 319/319 |

## Independent probes

These were run in a second disposable copy against the four scheduler and schema tests. They are
the same one-line mutations that round 105 found uncaught.

| # | Probe | Result |
|---|---|---|
| Q0 | unmodified, five consecutive runs (alongside the guard lane) | 4/4 green every run |
| Q3 | keep a higher receipt after a lower node becomes Ready | **red**: `ShippedHost_DiscardsHigherReceiptWhenAChildJoinMakesLowerOrdinalReady` |
| Q4 | evaluate the highest Ready node | **red**: both new scheduler tests; one fails immediately, having observed mapper 1 instead of 0 |
| Q7 | drop the separate output-schema comparison | **red**: `ChildPump_PoisonsTerminalOutputWithWrongSchemaAndKeepsAdmission` (one join acknowledged instead of zero) |

## Findings

No finding.

- **Recorded:**
  - The F3 decision and its retry bound are recorded only in this frozen review request. Record
    the bound in the durable operator guide when mixed-definition deployments are supported.
  - The Task 7.20 and accounting-guard wording still says "Task 2.3 bridge/scheduler candidate",
    which is now checkpointed. This is the usual stale status phrasing.
  - The new tests are in-memory shipped-host tests, as the request discloses. Real-storage
    qualification of the unchanged source comes from round 105.
  - The scheduler tests reach the internal pre-8.6 creation entry point through test-only
    reflection, as before. Shipped Hosting is unchanged.

## Hygiene and checkpoint sequence

The main checkout is at `bc52d7e674467c744964621cd3883c799669e983` with the 10 frozen entries and
nothing staged. When this verdict was written, it showed exactly those entries plus this new,
untracked verdict. My disposable worktrees are removed. `X:/ocv301` (`fix/ephemeral-start-gate`)
is not mine and was not touched.

1. Confirm zero drift against the 10-entry manifest and the anchors above.
2. Stage exactly those 10 paths from the manifest (not `git add -A`), expecting tree `7718e29d…`
   on parent `bc52d7e`, and commit.
3. In its only direct child, add and catalog this verdict and clear the active freeze, keeping the
   history pointer.

There is no activation.

## Determination

The new tests drive the shipped scheduler, pumps, provider and reducer deterministically. Each one
fails under the mutation it targets, and they are stable over repeated runs. The deferred-retry
decision is sound. No product source or test was weakened.

**Verdict:** **APPROVE**
