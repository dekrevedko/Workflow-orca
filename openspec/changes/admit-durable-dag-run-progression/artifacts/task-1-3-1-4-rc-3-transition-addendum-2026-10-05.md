# RC-3 canonical transition addendum

Date: 2026-10-05. Prepared for independent 1.3/1.4 review, not source approval.

## Immutable approval and scope

RC-3 was approved at checkpoint e6802a348b0180d5eaad3be99b32e3204c5c25a1,
tree d367ced646461fb78c8c85dea5bf1fb142e640f7, with sole direct-child evidence
578593efd3709a5e9a14582d572eb4275324de3e and 1.2-only activation d59f8307977dc71d1b77444b9711a0468c65f93d.
The [RC-3 verdict](../../../../docs/review/developer-facing-interface-section-08-task-8-4-dag-run-contract-rc-3-remediation-independent-review-verdict-2026-10-05.md) remains
byte-exact at b7c9d54dcfdbcc8fd3ec2ef99c43fb313e9b400a038b6da7f13b969b2feead4c.
The approved contract remains f8a1a7f6d69dc337854dd7e6dc9bbe59a2e7c3a43329635cf1b57c9f8280d665;
its API catalog and standalone signatures are not edited. The 365 additive API
keys (7/193/165) do not change. This addendum is bound alongside that packet and
verdict. The forthcoming transition verdict separately approves this addendum,
the one-word refinement and exact canonical/registry target; RC-3 alone does not.

## P3-1: mandatory progress, one-word delta refinement

Only the durable-runtime successor changes since e6802a3: MAY becomes SHALL in
"A newly Ready lower ordinal SHALL invalidate one higher speculative receipt".
This implements RC-3 section 4.2's mandatory discard and DG-P22 without changing
the recipe, signatures, bounds or exception policy. The old approved block hash
is retained in the Proposed history; the new hash is bound by ApprovedPending.
All other successor bytes remain identical to the RC-3 checkpoint. Canonical is
mechanically replaced from these four full delta blocks, never hand-edited.

## P3-3: safe continuous-discovery cost

A host MAY skip loading a run checkpoint on a discovery pass ONLY if the observed
StreamVersion equals its last validated version AND either it already holds an
evaluation or retained result for the authoritative lowest Ready node, or the
validated local free child window F is zero. The cache includes that lowest Ready
identity and the local host-owned ceiling/admitted count; version alone is NEVER
sufficient. Absent cache, changed version, changed local ceiling, lower-node
invalidation, loss/completion of local work, or a Ready node with F>0 and no matching
local work requires checkpoint load/reconsideration. A received wake also triggers
that reconsideration. A host dying after wake acknowledgement leaves other hosts
with no matching local evaluation/result: they MUST reload that unchanged version
and recover Ready work. DG-P24 must test this exact unchanged-version death case.

Each pass still pages all nonterminal run heads, wraps, and starts the next pass
within five seconds of completing the previous one. Cost per host/pass is O(run
heads) discovery rows plus checkpoint loads only for non-skippable runs; there is
no constant run-count-independent bound and no claim of zero I/O. The cache never
supplies durable authority, never suppresses subscription/version rechecks, and
never polls terminal waits. This refines section 6.3 without altering discovery
signatures, protocol fields or the lease/receipt contract.

Section 10's doc 16 disposition is superseded ONLY as follows: "Startup and
continuous wrapping discovery recover Ready work, including unchanged-version
orphaned wakes; safe local-work/free-window checks may skip checkpoint reads,
never StreamVersion alone. Terminal waits subscribe/recheck, not poll."

## P3-2: source-review obligation, not catalog expansion

Task 2.3 must define evaluation-lane telemetry by active and retired state,
including cancelled runs with stuck mappers, and specify whether idle dedicated
workers are reused. Its separately reviewed source target must prove the gauge
and lifecycle accounting and disclose any required API/signature additions then.
No gauge member, public option or API catalog key is added by this transition.

## P3-4: documentation-pin maintenance

Because the status refresh edits Task 7.3's reconciliation artifact, remove its
duplicate RC-3 scheduling refresh section and refresh its affected source rows
and whole-artifact pin in this same target. No frozen review record is edited.

## Registry and lifecycle rules

Retain four Proposed rows as immutable RC-3-approved hash history. Add exactly
four ApprovedPending rows with the new canonical hashes and four hash-matched
reshape predecessor supersessions; preserve all unrelated rows. Bind RC-3's
checkpoint/tree/verdict/addition evidence and this addendum. Task 1.3 is checked
inside the target; 1.4 stays open for independent review/checkpoint and only its
checkbox is exempt for later activation. Tasks 2.1–3.2 and reshape 8.4/8.5 stay open.

One active OR one ISO-dated archived owner resolves; zero, undated archive or
multiple owners fail closed. Archival is a separate reviewed move/provenance
refreeze, never implicit removal of supersession/history or requirement identities.
Withdrawal is a separately reviewed registry/source-pin disposition retaining all
packets: remove the four live approval rows and owning change together and restore
measured provenance. No withdrawal is admitted by deleting fixture rows/directory
in this ApprovedPending target. Each rule has a separate negative control.

## Crosswalk disposition

The primary mappings for these two capabilities are docs 06, 10 and 16; all are
amended for approved/source-pending status. Reverse audit also amends docs 03, 04,
05, 08, 12 and 17 plus root/binding guides. Doc 11 is explicitly excluded: NF
package/nonfunctional obligations and the distinct-limit taxonomy are unchanged;
the safe discovery refinement respects NF-030 without creating a fifth heading.
The public-authoring companion is excluded: no public application signature changes.
No source, friend, compiled API baseline or test disposition changes are authorized.
