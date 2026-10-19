# Issue #1 canonical approval binding — 2026-10-17

This small binding target is based on `3eee5165e94ef91a0a1ef6eb4ec4b80ae1ea528f`.
It records the independently approved and checkpointed task 2.1 transition. The binding
target itself remains uncommitted and requires its own independent review and checkpoint
before source task 3.1 may start. A green process lane is not that approval.

## Actual transition evidence

| Evidence | Exact value |
|---|---|
| Canonical checkpoint | `d1d3cf669e966a567155205715ebc53c398875d5` |
| Sole parent | `f1eb86e31306d0aab88d8f84d38c925ef2eaf4c7` |
| Approved checkpoint tree | `83a08eaad4511500f482b7beace3a9455678a3ff` |
| Verdict-only direct child | `3eee5165e94ef91a0a1ef6eb4ec4b80ae1ea528f` |
| Verdict path | `openspec/changes/add-ephemeral-terminal-retention/artifacts/task-2-1-transition-independent-review-verdict-2026-10-16.md` |
| Raw verdict bytes | 14,826 |
| Raw verdict SHA-256 | `67abed678ba58bd72411b79b5ab4276299689197f067123a84acf6f7a19d747a` |

The validator checks these source-pinned values against Git: checkpoint sole parent and
tree, the child's sole parent and exactly one added path, the committed verdict blob
against current raw bytes, its SHA-256, and its APPROVE heading. The verdict child must be
an ancestor of HEAD. Missing objects fail; CI's full-history checkout remains required.
The earlier contract checkpoint/verdict and registry-artifact checks stay unchanged.

## Registry and task state

Both `ephemeral-retention-developer-facing-surface` (one requirement) and
`ephemeral-retention-management-and-querying` (four requirements) move to
`CanonicalApprovedSourcePending`. Their `canonicalApprovalEvidencePath` fields name the
actual transition verdict above and share the exact Git evidence object. Their source
approval fields remain null, and every source/qualification task 3.1–5.3 stays open.
Only the checkboxes for 2.1 and 2.2 change in tasks.md; their text and all other boxes stay
byte-exact. These proposed checked states do not bypass this binding target's review gate.

Schema 13 records this named transition; it does not offer a generic approval override or
weaken completed-amendment or DAG predecessor rules. The existing guard still checks both
closed identities, requirement counts, matching canonical/delta blocks, exact normative
owners, acceptance paths and task references. The binding artifact's digest is pinned by
the fixture and independently by guard source.

This dated record supersedes only the pending-review/checkpoint status claims in the
task-2-1 amendment-registry and provenance artifacts. Both remain immutable historical
evidence at their original pinned hashes. The requirement provenance fixture, current
provenance artifact, 189-row TSV, whole-canonical inventory and predecessor catalog do not
change: 178 synchronized, 8 superseded, 3 planning-only outside canonical, zero pending;
49,879 bytes / `0f56a33d2aa17e6848ac8f3bf4769ec1a8604d0dadb25a7f9f9b8092a673f459`.

The reviewer reports strict OpenSpec 22/22, Release 0 warnings/0 errors and Infrastructure
247/247 at committed base 3eee516 in a fresh E:/ocv195 copy, since removed. That is
reviewer-provided evidence; this target has its own validation and freeze record.

## Remaining gates and seven carried P3s

The full transition verdict remains the authority for these non-blocking items. This
binding target changes none of their affected product/docs/ledger paths:

1. Relabel ledger row 209 in the next target that touches the ledger; preserve the reviewed
   499-row ledger here.
2. Guard doc 17's section 17.4 host-options/policy declarations no later than slice 4.2.
3. Integrate master only through a separately reviewed merge target and a merge commit,
   preserving history; resolve the Task 7.3 row/digest and production-readiness conflicts.
4. Remove the exact ownership-row mapping when slice 4.2 updates the executable contract.
5. Resolve the DAG guard's active-path coupling before archival or tripwire replacement.
6. Assign source/final-qualification ownership for updating pending guide/status prose.
7. Update the developer guide's aging draft wording at a later reviewed gate.

After this binding target is independently approved and checkpointed, task 3.1 may be
prepared under its own source review/checkpoint gates. No product source, test-first slice,
API/compile/package baseline, canonical/delta/spec or guide change, integration, push,
issue change or other issue work is authorized by this draft.
