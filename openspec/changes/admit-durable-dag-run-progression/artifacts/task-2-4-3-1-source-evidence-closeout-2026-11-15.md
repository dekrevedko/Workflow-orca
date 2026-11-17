# DAG Tasks 2.4/3.1 — source slice approvals and evidence disposition

Date: 2026-11-15. Closeout base: `da1a4729ea2cdb9474eb219eb507760574f83dcb`. This is a
review candidate for the bounded Task 2.4 milestone and the Task 3.1 evidence inventory.
It is not the final implementation verdict, an API amendment, or a disposition that
turns partial checks into certification.

## Task 2.4 — approvals already in Git

| Reviewed slice | Checkpoint | Approved tree | Direct-child evidence | Immutable verdict |
|---|---|---|---|---|
| 2.1 Typed identity | `c93411e574c4dc8db218c475a81f16bff5df5b85` | `ac61630f2b4d26365ae824b0bb2dcb35b0ec8b48` | `3e1fbdd4e8331fed3a799d90a9105dc64f63f2f4` | [APPROVE](../../../../docs/review/developer-facing-interface-section-08-task-8-4-dag-run-task-2-1-stream-identity-independent-review-verdict-2026-10-14.md); 16156 B / `d786d7ca7c00af25c147079d68ccf74875b28878364738c9c06198453c23cb39` |
| 2.1 Protocol and providers | `f62b0aafd8beeb69aa28efaba0f25413c4970b3b` | `0e5300632b9804f096ac45d119a602b5b9f5b9c7` | `602844547539e0e5908d250c2a0d9560fc4b25dc` | [APPROVE](../../../../docs/review/developer-facing-interface-section-08-task-8-4-dag-run-task-2-1-protocol-provider-independent-review-verdict-2026-10-29.md); 8809 B / `c971876e87d871f5fce5b48bc685a918f7d2e1dd8b6dd81869068c4bf9d5584b` |
| 2.2 Pumps and certification | `2fd7a7d789203cb1b780abf5478d0e67c070e7f7` | `42770df9ad12a5cbeaeefb53ae13ef8e3794da7e` | `3dae4aff9e190af2fa4acbc93d3b8aa2d4d1383c` | [APPROVE](../../../../docs/review/developer-facing-interface-section-08-task-8-4-dag-run-task-2-2-pumps-and-certification-independent-review-verdict-2026-11-01.md); 8396 B / `0107b814d79c36450fe1f2c6b866069114bd59adb7099763269d51a477889b7b` |
| 2.3 Bridge and scheduler | `e955f3a4151ac06ea70175549a99cc06adc95b3f` | `463b7e62b7d5bf8c74b80c5a85a02cc36cf93ab2` | `bc52d7e674467c744964621cd3883c799669e983` | [APPROVE](../../../../docs/review/developer-facing-interface-section-08-task-8-4-dag-run-task-2-3-bridge-scheduler-independent-review-verdict-2026-11-13.md); 11656 B / `0ab955ba8b37f91a0df32e740f6605e6f24de1c89630dec7d46480e884444103` |
| 2.3 Scheduler/schema follow-up | `3f6d11bfff257464fb5c1a92966d9683d97a12b4` | `7718e29d638d8b2ace2bc40c6e51e0b94577b20c` | `da1a4729ea2cdb9474eb219eb507760574f83dcb` | [APPROVE](../../../../docs/review/developer-facing-interface-section-08-task-8-4-dag-run-task-2-3-review-followups-independent-review-verdict-2026-11-15.md); 7585 B / `953c02bd07e17ea0b378cba8a001306a9a23d53efd9f73de2f2e07f797f2a946` |

Each verdict is the file added by its single-parent evidence commit, matches that
committed blob byte-for-byte, has one final APPROVE line, and is already cataloged.
Each evidence commit is on this branch's ancestry. The source guard verifies
the tree, ancestry and verdict for each row. Round 106 is tests/accounting only;
product source is unchanged from the round-105 provider qualification.
No public API baselines or provenance status are advanced by this target.

## Task 3.1 — evidence inventory, not final qualification

The [approved RC-3 contract](task-1-1-run-contract-rc-3-remediation-2026-10-05.md)
and [transition addendum](task-1-3-1-4-rc-3-transition-addendum-2026-10-05.md)
still own the certification criteria. The table credits only the stated
assertions. InMemory Hosting cases are not SQL cases, an internal bridge call
is not a public handle call, and repeat/replay is not crash injection.

**Shared provider tests:** [DagRunCertification](../../../../tests/OrcaCore.ProviderCertification/DagRunCertification.cs)
runs through the InMemory, PostgreSQL and SQL Server certification wrappers.
**Shipped-host tests:** [Scheduler](../../../../tests/OrcaCore.Hosting.Tests/DagSchedulerTests.cs),
[child recovery](../../../../tests/OrcaCore.Hosting.Tests/DagChildRecoveryTests.cs),
[outbox partitions](../../../../tests/OrcaCore.Hosting.Tests/DagOutboxPumpTests.cs),
[creation and receipts](../../../../tests/OrcaCore.Hosting.Tests/DagRunCreationTests.cs)
and [restart input binding](../../../../tests/OrcaCore.Hosting.Tests/DagRunInputBindingTests.cs).
These Hosting tests use InMemory. The shared scheduler case runs the shipped
assemblies and real provider on all three, with reflection only at the test
entry to the as-yet internal create/wait/cancel surface.

| Contract row | Evidence covered by the slices | Limit or remaining qualification |
|---|---|---|
| DG-P01 | Typed stream identity, unchanged workflow codec baseline, rejected DAG/default IDs and mixed workflow effects, GUID collision in both directions. | Scope is identity and port fencing, not a public DAG start. |
| DG-P02 | Shared exact create/replay and separate caller-key namespace; Hosting rejects altered graph/input/identity and a missing binding. | Hosting creation is internal, not public registration or start. |
| DG-P03 | Shared checkpoint plus tail and codec tests for invalid or noncanonical coordinates/fields and committed hashes. | No claim of exhaustive damaged-store fixtures. |
| DG-P04 | Shared stale-fence rejection, operation replay and atomic append/addressed notice assertions. | No exhaustive interruption at each production append point. |
| DG-P05 | Shared FIFO, dead waiter, consumed request, concurrent stores, deadline and stale fence/version assertions. | Provider lease cases do not imply full host-broker scheduling coverage. |
| DG-P06 | Persisted admission/cancellation plus Hosting receipt/ordinal deferral. | Restart/takeover with changing live host ceilings needs dedicated end-to-end qualification. |
| DG-P07 | Outbox partition guards, wrong-lane release, capable-sink-only readiness, role-free child execution and batch-1/batch-3 deferral without starvation. | Scope is internal routing, not external transport or application delivery. |
| DG-P08 | Shipped scheduler decodes a real predecessor output; mapper/runtime-view cases cover input shape and null policy; child schema corruption poisons the real join on InMemory. | No production-provider matrix combining eager unused-output decode corruption and present-null committed bytes. |
| DG-P09 | Deterministic start replay, identity and lineage checks, atomic terminal notice/join and collision poison in Hosting. | No exhaustive real-storage crash schedule across intent, child append, join and acknowledgement. |
| DG-P10 | Shared failure/blocked-descendant closure and same-append terminal invariant; Hosting authored ordering and independent progress. | Credit the observed transitions, not every production failure schedule. |
| DG-P11 | Provider cancellation and integrity states; Hosting tests for pre-barrier rejection, materialization before cancellation and keeping uncertain admission. | No exhaustive two-host cancellation/start race matrix. |
| DG-P12 | Shared addressed notices and subscription ownership; shipped terminal wait and locally installed input-type rebinding. | No complete subscribe race/lost-ack/boot-host-death matrix. |
| DG-P13 | Exact API/friend and compiled boundary guards; the shipped Hosting harness and the existing reflection, UnsafeAccessor and expression restrictions with DagRuntimeView.cs in the evidence audit. | Public Task 8.6 fixture intentionally remains red; no new signature or package edge. |
| DG-P14 | Provider FIFO/deadline and out-of-turn mapper isolation. | A continuously renewing short I/O turn with a real remote child join behind it is not demonstrated as one end-to-end case. |
| DG-P15 | Internal cancellation of a blocked run on each provider; role-free child cancellation/join is tested separately. | **Public DagRunHandle is Task 8.6.** Neither check substitutes for the two-host public cancellation scenario. |
| DG-P16 | Provider fence/purpose rules, queue behavior and Hosting receipt deferral/cancellation. | No combined production-provider test of changing owner limits, idle release and stale captured-turn rejection. |
| DG-P17 | Shared controlled-clock observer expiry pruning retains live claims, poisoned observer notices and retryable or poisoned child-start work. | Explicit unsubscribe purge and process-death recovery for unaddressed progression hints remain unqualified. |
| DG-P18 | Shared compact transition, checkpoint/tail and atomic terminal cases; Core canonical codec validation. | Public Running projection and public snapshot shape remain unqualified. |
| DG-P19 | The shipped scheduler progresses a healthy run beside a blocked mapper. Cancellation reaches a terminal run with no committed node input while the mapper is still blocked. | The tests release the mapper after those assertions but do not check whether a late receipt is refused. Explicit post-release admission refusal and the controlled >15-second provider-clock, terminating receipt survival and version/capacity churn case remain unqualified. |
| DG-P20 | Shared observer-pressure / retained-work assertions; compiled active/retired evaluation gauges and worker lifecycle in the approved scheduler source. | No full production pressure/gauge matrix including retirement while a cancelled mapper still runs. |
| DG-P21 | Provider FIFO, concurrent owners and operation/purpose checks. | No full mixed-purpose local/remote queue saturation, coalescing and captured-limit matrix. |
| DG-P22 | The shared scheduler executes a dependency chain on all three providers; its order is imposed by dependencies. Round 106 adds separate tests for one-slot authored order among ready roots and lower-ready receipt invalidation on InMemory only. | Simultaneous-ready authored-order qualification with skewed mappers remains open on PostgreSQL and SQL Server for both one and two hosts, as does production stale-rank qualification. |
| DG-P23 | All three providers run the shipped scheduler with blocked/cancelled and healthy runs; one evaluation is observed. The approved code counts active/retired lanes and reuses the worker per run. | No exhaustive nonterminating-mapper/gauge and 10,000-node no-look-ahead qualification. |
| DG-P24 | Hosting validates wake acceptance before acknowledgement; normal shipped discovery/wait execution. | No production-provider crash before/after acknowledgement and lost/poisoned-wake rediscovery matrix. |

For linked acceptance, the bridge review supplies internal lineage evidence
(AC-614), deterministic reattachment, internal start/outbox and mapping coverage.
It does **not** certify the public lineage result, public snapshot/start/handle,
or the full authored-order and provider failure matrix. Existing compile
exclusions, acceptance traits, waivers, the expected-red list and the
`dag-hosting` package fixture are unchanged.

The current-status notes are refreshed in the non-overlapping numbered and
guide documents. Six shared numbered documents (03, 04, 08, 10, 12 and 17) are
whole-file-bound by the independent ephemeral-failure settlement transition.
Their older DAG status notes remain historical; this inventory is the current
DAG disposition. No exception is made to the other change's documentation pins.

## Status and review boundary

- Task 2.4 records the five independently approved source/review slices to date.
  Any later slice needs its own independent approval, checkpoint and guard row.
  This candidate does not change the existing states of Tasks 2.1–2.3.
- Task 3.1 remains open for the missing full acceptance/provider qualification.
  This inventory is reviewable now; outstanding cases stay obligations, not waivers.
- The four registry rows stay `ApprovedPending`. Task 3.2, reshape 8.4/8.5 and
  public Task 8.6 remain open. No registry promotion, activation, archive, or
  forward implementation is requested.
- The shared default outbox claim stays five minutes. Round 106 accepted that
  shortening the shared lease also shortens in-flight ownership, while immediate
  release reintroduces starvation. Revisit retry eligibility and operator
  guidance with separately reviewed mixed-definition deployment support.
