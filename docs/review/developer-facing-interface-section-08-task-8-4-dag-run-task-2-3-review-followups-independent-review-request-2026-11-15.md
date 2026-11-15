# Independent review: DAG Task 2.3 regression follow-up

Date: 2026-11-15. Parent: bc52d7e674467c744964621cd3883c799669e983. Parent tree: 0985f6df5431120bfd5d7fbd12245293a81b5769.
Branch: fix/dag-task-2-3-review-followups. Review the [self-inclusive raw-order manifest](developer-facing-interface-section-08-task-8-4-dag-run-task-2-3-review-followups-dirty-manifest-2026-11-15.txt) as one target.
Scope is the round-105 non-blocking scheduler, child-output and deferred-retry findings following checkpoint e955f3a4151ac06ea70175549a99cc06adc95b3f and its [independent verdict](developer-facing-interface-section-08-task-8-4-dag-run-task-2-3-bridge-scheduler-independent-review-verdict-2026-11-13.md) in the parent evidence commit.
This is not a Task 8.6 implementation, a registry activation, or SQLite work. No product source, API baseline, canonical spec, DAG amendment task state, or previously approved packet changes.

## What to review

1. **Lowest ready ordinal with one slot.** Two independent roots in the shipped DAG host, with MaxConcurrentNodes=1, complete through real Durable Hosting pumps and InMemory storage. The first mapper is ordinal 0, and committed InputAdmitted records are ordered 0, 1.
2. **Stale higher receipt.** Roots 0 and 2 are initially ready; node 1 depends on 0. With two slots, a gate holds child 0 while mapper 2 begins. The test lets child 0 commit its real join, observes node 1 durably ready, then releases mapper 2. There are no sleeps in the ordering handshake. The first result (41) cannot be used; mapper order is 0, 2, 1, 2, committed admission order is 0, 1, 2, and node 2 commits 42.
3. **Child output schema.** A genuine child completes with output 42. The InMemory fixture rewrites only the terminal checkpoint output schema identity, not its bytes, role, completion facts or real join intent. The shipped child pump must poison that intent as DAG_PROTOCOL_INVALID, set the owning run to IntegrityFault, preserve StartAuthorized and publish neither a joined fact nor a node output.
4. **Accounting only.** Three new active declarations, no new test source file: 383 physical sources / 1,615 declarations and 227 active sources / 927 declarations. Reshape Task 7.20 names this inventory refresh; its checkbox and retired-declaration accounting are unchanged.

Test-only reflection reaches the same unshipped create entry point as the existing scheduler test. It does not bypass the shipped scheduler, pump, provider or reducer.

## Deferred retry decision: keep the current lease

The durable child pump retains the existing claim for a deferred handler outcome, which makes it ineligible until lease expiry. This applies, for example, when the necessary definition is absent, a local turn queue is full, or a turn is lost. The default claim lease is five minutes; the previous starvation certification intentionally exercised lease expiration using 30 seconds and did not establish a default retry SLA. Reclaim eligibility is based on the remaining lease; pump cadence, contention and definition placement can add delay.

Changing that shared lease to a shorter default would also shorten ownership for in-flight dispatch, not just retries. Immediate release makes an older deferred batch eligible again before healthy work, reproducing the starvation regression. A deployable shorter deferral would need separate provider-authoritative retry eligibility or routing, plus explicit cross-host and all-provider starvation and claim ownership controls. No lease, provider, option or telemetry change is included here.

## Probe and validation scope

Before freezing, three single-check mutations were tested against the real Hosting suite, one at a time, and the two temporarily touched production files were restored and verified against their pre-probe raw SHA-256 hashes and HEAD:

- Select the highest ready ordinal: the one-slot test fails immediately, observing mapper 1 instead of 0.
- Keep a prepared higher receipt when a lower ordinal becomes ready: the stale-receipt test reaches the durable join, then the terminal wait fails on its 30-second test deadline.
- Drop the separate output-schema comparison: the bad-schema test fails because the pump acknowledges one join instead of zero.

After restoration, Debug and Release warnings-as-errors builds passed with zero warnings and errors; Hosting passed 53/53; OpenSpec strict validation passed 26/26. The handoff states the final guard and expected-red results and copied-index tree. The new regressions are InMemory shipped-host tests; PostgreSQL/SQL Server container lanes and external package fixtures are not requalified by these tests. Validation is in the main checkout; the copied-index projection is not a committed-worktree rehearsal.

## Freeze and disposition

- Manifest: UTF-8 without BOM, LF, final LF; byte-equivalent to raw git status --porcelain=v1 --untracked-files=all --no-renames and its NUL form converted to LF. The manifest includes itself.
- Semantic record: exclude docs/review/**, immutable-document-history.json and review-manifest-provenance.json. Each row is path<TAB>raw byte count<TAB>lowercase SHA-256, ordinal-sorted and LF-joined with one final LF: 6 rows / 809 B / 6ddccac951c07f3d281eb3da611f4c721f9cf449927a4ca33fabfd6aa13b8ed5.
- Active record: XY<TAB>path<TAB>raw byte count<TAB>lowercase SHA-256, ordinal-sorted and LF-joined with one final LF; exclude only the self-referential review-manifest-provenance.json. The remaining anchors and projected tree are in the handoff to avoid self-reference.
- Independent review only. Leave the target uncommitted. If approved later, stage exactly the manifest and verify its tree before checkpointing; add and catalog the verdict in its only direct child and clear the active freeze, keeping the history pointer. No checkbox activation or forward source work follows from this packet.
