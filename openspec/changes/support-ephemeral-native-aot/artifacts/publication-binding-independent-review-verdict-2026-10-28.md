# Issue #13 publication binding — self-review, 2026-10-28

**Verdict: no blocking findings; ready for checkpoint/publication authorization.**

This is an adversarial author self-review, not external approval. The user approved
the preceding exact corrected target with “Continue”. This new binding target is
uncommitted; its review does not authorize the issue-branch push or PR. Merge,
issue closure and release remain separate gates. Exclude this verdict from the
checkpoint manifest so it can be the sole direct child after approval.

The committed chain reproduces: d8c55f3 has parent 1ad2a59, exactly 216 changed
paths and tree 8a1b648c5a1f75ed196d96a9d091b8554b511634. Its sole direct child
25fa36c changes only the 6,416-byte review verdict, SHA-256
6d6edefc7e96d1f80e3a8410a72f8315b220c02a49bf32920f9bcf28c96cd856.
No approved target/verdict bytes were edited while checkpointing.

Schema 23 binds current source/native approval through the new DirectUserApproval
record, with the real user quote and Git chain. The author review is explicitly
not the approval authority. Both top-level evidence fields and all four capability
records point to that binding. Earlier registry records and the frozen pending-
review execution receipt remain unchanged; their old null approval fields are
historical and do not override or grant the current approval.

The guard resolves checkpoint parent/tree, the sole verdict child and its exact
committed bytes, the 216-path manifest, ancestor relation, executed record, source/
harness hashes and native index through Git. Failed Git lookups fail the check.
The old scope and six-cell assertions are preserved, and incomplete/pending native
execution no longer has a passing approval branch. Publication/merge/release and
broader-capability flags remain false in the approval record. Tasks 3.1/3.2 stay
open; no checkbox changed.

Three semantic probes recomputed matching approval/registry pins in the disposable
copy, then supplied a wrong tree, a nonexistent Git object and altered verdict
bytes with a matching new local hash. All compiled and failed at the intended
Git assertions. Original bytes were restored, rebuilt and the control passed.
These probes establish more than rejection by the first hash pin.

Living guides, README, numbered status notes, proposal/design and task notes now
name the corrected approved source and 384-case native run. The stale doc-13
statement that task 2 was uncommitted is removed. Only three living document hashes
and their owner artifact pin refresh mechanically. The exact API/declaration block
remains unchanged. Old dated review/native artifacts and preview.2's unsupported
assessment remain intact; no package version or broad compatibility claim is added.
The clean PR body references #13, the actual run, scope/limits and validation, with
no internal review-material paragraph or automatic issue-closing instruction.

No production source, canonical/delta requirement, native consumer/harness or API/
compile/dependency/package-provenance baseline changed. All 360 native source/build
inputs and 20 harness identities still match. The approved six-cell 384/384 and
runtime unit/provider/stress/coverage evidence remains applicable; it is not falsely
reported as freshly rerun in this process/docs target.

Fresh validation: Release warnings-as-errors 0/0, exact local feed 12 packages,
OpenSpec 25/25 and Infrastructure 309/309. The first Infrastructure attempt passed
308 and caught a changing untracked ReSharper cache in the freeze-projection test.
Moving validation output to the existing ignored directory resolved this setup
race; the guard was not loosened. ReSharper 2026.2.3.1, Release / Any CPU / net10.0,
inspected the four changed guards using compatible fresh build outputs: zero
errors, 53 warnings and 46 notes, with no new rule/message findings versus the
retained reviewed guard baseline. Raw SARIF and triage are retained.

GitHub master is still 3b7f98a, the issue branch base; no integration merge is
needed at this review snapshot. Issue #13 is open and no issue-branch PR exists.
The issue branch has not been pushed. Recheck remote state before an authorized
publication, require PR CI, and merge only with an authorized merge commit.
