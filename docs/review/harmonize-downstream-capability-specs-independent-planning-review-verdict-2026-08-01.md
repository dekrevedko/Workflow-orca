# Harmonize Downstream Capability Specs Independent Planning Review Verdict

**Date:** 2026-08-01  
**Disposition:** Immutable audit-only review  
**Verdict:** **REJECT**

Task `1.4` remains open. Canonical synchronization is not authorized. Neither this verdict nor the
green structural validations authorize implementation of reshape Section 7B or any Section 8 work.

## Reviewed target and provenance

- Repository HEAD: `ac46d99543daf85c0fa3234272997ba40f47f96b`
- HEAD tree: `28f4033c4773ea7761afa905c9836fd25866f1c0`
- Reviewed change: `openspec/changes/harmonize-downstream-capability-specs`
- Exact reviewed artifact set: eight files (`proposal.md`, `tasks.md`, and six capability deltas)
- Ordered path/file-hash set SHA-256:
  `16af80dea68a11d5a39938a7d7d2d3f98051f5e728d32fa59dbf57ffdc362b4e`
- Pre-verdict worktree: 419 ordered porcelain entries; sorted-status SHA-256:
  `2de08a0275a7cc104a154afa778999bf8911762b6d2eed91d33b1e3da699074b`

The eight-artifact hash and the 419-entry status hash reproduced exactly after the review commands.
No reviewed proposal, task, delta, canonical spec, product source, test, or existing documentation
artifact was edited. This verdict is the sole review-authored path.

## Findings

### P1-1 — Harmonize would synchronize the event contract that reshape task 7.23 explicitly supersedes

The harmonize proposal and `event-routing-and-waits` delta require non-buffering delivery,
`NoActiveWait`, exactly two route names, and absence of definition fanout
([proposal.md](../../openspec/changes/harmonize-downstream-capability-specs/proposal.md),
[event delta](../../openspec/changes/harmonize-downstream-capability-specs/specs/event-routing-and-waits/spec.md)).

The live reshape proposal and Decisions 24/25 instead require durable ownership before broker
acknowledgement, pending pre-wait inboxes, removal of `NoActiveWait`, direct/correlation/fanout/
start-or-deliver routes, cold activation, and durable workflow-authored `Publish`
([reshape proposal](../../openspec/changes/reshape-developer-facing-interfaces/proposal.md),
[reshape design](../../openspec/changes/reshape-developer-facing-interfaces/design.md)). Most
decisively, reshape task `7.23` says to remove the contradictory non-buffering/fanout-removal
ownership from harmonize **before either change synchronizes canonical specs**
([reshape tasks](../../openspec/changes/reshape-developer-facing-interfaces/tasks.md)).

Approving harmonize task `1.4` now would authorize exactly the synchronization that task `7.23`
forbids. The unchanged canonical baseline may remain temporarily inconsistent while the amendment is
open; it must not be synchronized to either competing answer until the replacement contract is
approved.

### P1-2 — The outbound dispatch owner and public shape conflict

The harmonize `durable-persistence-and-outbox` delta requires outbound dispatch through the advanced
provider contract `OrcaCore.Abstractions.Providers.IMessageDispatcher` owned by
`OrcaCore.Provider.Abstractions`. The live reshape design instead makes
`OrcaCore.Durable.Hosting.IWorkflowEventDispatcher.DispatchAsync(WorkflowOutboundEvent, ...)` the sole
application-shaped external dispatch boundary and prohibits provider `OutboxWrite`, claim, stream,
and checkpoint data from crossing it.

Those are different public owners, arguments, and tiers. In this greenfield repository they must be
resolved to one coherent contract, not retained as compatibility aliases. Harmonize cannot claim it
introduces no semantics while selecting the superseded raw provider seam.

### P1-3 — The claimed approved four-friend graph has no approval anchor and is not implementation-complete

Harmonize task `1.1` is checked and says reshape Decision 22 was already approved. Decision 22 is not
present in HEAD `ac46d99`, and the immutable review corpus contains no Decision-22 or four-product-
friend approval evidence. It exists only in the current uncommitted reshape design. Harmonize's
proposal and `repository-foundation` delta nevertheless make those four product friends exhaustive.

Current source still crosses the intended boundary through non-public reflection in staged
authoring, engine application-contract factories, runtime-context factories, and
`AuthoringKernelProxy`; the active infrastructure lane already rejects the two named bridge families.
The exact boundary therefore still needs an approved typed ownership decision. The narrow candidate
is an `OrcaCore -> OrcaCore.Core` friend edge paired with moving authoring ownership so the reverse
reflection proxy disappears, but an alternative typed design may be approved instead. What is not
valid is canonizing the current four-edge allowlist as settled while its implementation requires
prohibited private-access bridges.

The synchronization scope is incomplete as well: reshape adds the requirement
"Implementation package boundaries use exact internal friends" to `developer-facing-surface`, but
the canonical capability does not contain it and harmonize has neither that delta nor an explicit
sync task for it.

### P1-4 — The proposed Section 7B replacement still has unresolved contract decisions

Retargeting harmonize mechanically to the current reshape wording would not yet make the combined
plan approvable:

- Ephemeral definitions retain descriptor-based `Wait`, but durable-only ingress replaces
  `IWorkflowEventClient` without defining an ephemeral event-delivery route.
- Start-or-deliver lacks the attempted definition fingerprint required by the existing public
  `StartIdempotencyConflict`, while callback-only ingress deliberately has no definition catalog from
  which to obtain it.
- Definition-owning pumps use process-local staged catalogs, but no exact definition-aware claim
  partition or provider-wide catalog-completeness rule prevents one host from claiming another
  definition's work and poisoning it.
- `FanoutLimitExceeded` has no normative source, unit, configuration, or provider-independent
  threshold for the limit.

These decisions belong in the upstream reshape amendment and its missing
`event-routing-and-waits`/`durable-persistence-and-outbox` deltas before harmonize propagates them.

### P1-5 — The coordinated documentation target rewrites a frozen historical file

The staged move of `docs/end-to-end-plan.md` to
`docs/archive/plans/end-to-end-plan-pre-v1.md` is byte-identical, but the moved archive file then has
an unstaged 12-line rewrite replacing historical `Statistics()` and
`AddOrcaCoreOpenTelemetry(...)` guidance. The archive contract says historical content is preserved,
not modernized. Restore the moved file byte-for-byte and keep corrected guidance only in the new
active plan. This is especially important because harmonize tasks describe the documentation
restructure as completed evidence.

### P2-1 — The OpenSpec artifact set is incomplete

`openspec status --change harmonize-downstream-capability-specs --json` reports `isComplete: false`;
`design` is `ready` and `design.md` does not exist. The change coordinates six capabilities, two
normative trees, friend-boundary architecture, synchronization ordering, and documentation policy.
That is not a case where the missing design can safely be inferred from a short proposal. Complete
the design before requesting approval of the whole change.

### P2-2 — Checked provenance and the post-restructure task ledger are stale

The frozen cross-capability consistency record supports the original canonical drift, but it does
not contain the later Decision-22/friend-graph evidence now claimed by checked task `1.1`. New
evidence must be recorded without editing that historical review.

At least tasks `6.4`, `7.1`, `7.2`, `7.3`, and `7.5` have been materially performed in the current
documentation tree but remain open; `7.2` and `7.3` still name paths now moved under
`docs/archive/plans/`; and `3.2` calls prototype retention undecided after the proposal and delta
already select retention. Reconcile wording and checkboxes against the reorganized tree before the
next freeze.

## Validation reproduced

- `openspec.cmd validate harmonize-downstream-capability-specs --strict` — passed.
- `openspec.cmd validate reshape-developer-facing-interfaces --strict` — passed.
- `openspec.cmd validate --all --strict` — 18 passed, 0 failed.
- All `MODIFIED`/`REMOVED` requirement headings in the six harmonize deltas exactly match the
  unchanged canonical headings; every removal carries `Reason` and `Migration` text.
- Canonical capability inventory — 14 directories, each with `spec.md`.
- Harmonize task accounting — 31 total, 4 checked, 27 open, 0 duplicate IDs.
- Relative links inside the eight harmonize artifacts — 0 missing targets.
- Scoped `git diff --check` — exit 0; line-ending conversion warnings only.

Strict OpenSpec validation proves structural validity. It does not reconcile two changes that demand
opposite semantics or prove the claimed approval provenance.

## Required sequence before re-review

1. Resolve and independently approve the Section 7B event, dispatch, catalog/claim, fanout-limit,
   and typed-boundary decisions in reshape; add its missing affected-capability deltas.
2. Retarget harmonize to the resulting non-overlapping residual synchronization work. Remove the
   obsolete event/redelivery and raw-dispatch ownership, update the exact friend graph, and include
   every still-unsynchronized canonical capability.
3. Add `design.md`, reopen and re-run tasks `1.1` through `1.3`, and reconcile the task ledger with the
   reorganized documentation tree.
4. Restore the archived pre-v1 plan byte-for-byte, keep current guidance in the active plan, strict-
   validate all active changes, and freeze a new exact target.
5. Obtain a fresh independent approval. Only then may task `1.4` be checked and canonical
   synchronization begin.

No canonical spec was synchronized, no task checkbox was changed, and no commit was created.
