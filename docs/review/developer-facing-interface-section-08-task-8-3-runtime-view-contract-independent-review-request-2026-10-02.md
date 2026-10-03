# Independent review — proposed DAG hosting runtime-view contract

Date: 2026-10-02
Change: `admit-dag-hosting-runtime-view`
Task: 1.1 prepared contract, before Task 1.2 independent approval
Base: `2a09b452af067fbd501215a572712ff08cf2bbc9`
Manifest: [developer-facing-interface-section-08-task-8-3-runtime-view-contract-dirty-manifest-2026-10-02.txt](developer-facing-interface-section-08-task-8-3-runtime-view-contract-dirty-manifest-2026-10-02.txt)

## Authority and completed process chain

Review this single uncommitted contract target. An APPROVE authorizes its exact checkpoint;
it does not authorize canonical synchronization, registry promotion, a friend attribute,
codec access in Dag, Task 8.3 source, or early completion of reshape 8.4/8.5. Runtime-view
rows remain Proposed, two operations remain pending, and semantic approval remains false.
Task 1.1 is checked as prepared; 1.2–3.2 remain open.

The process-only chain is complete:

- checkpoint `dc8095c5536316cb772c985641e45179fa3c93b5`, tree `d8f7d84fe751c323c11e2190b7c03a39f1d8cc5e`, parent `6d49716384b50e2cbaf503fcd782b63c6384bc1e`;
- sole-child evidence `0f4fafa3c3b3acfdb2c39227bba53f094782bd13`, adding the process APPROVE byte-exact, cataloging it and clearing the active freeze;
- checkbox-only activation `2a09b452af067fbd501215a572712ff08cf2bbc9`, changing Task 0.2 only.

Process verdict: `docs/review/developer-facing-interface-section-08-task-8-3-runtime-view-successor-process-independent-review-verdict-2026-09-30.md`,
11,549 bytes, SHA-256 `bc72cd0eca94e77a1c09744e18aea6bb0133cfe5429ddf5a50fdfb4e0c70cd4c`.
The evidence state passed Infrastructure 240/240. The new guard binds this approval to
its actual addition and single-parent direct-child evidence, not a later containing commit.

## Freeze and reproducible recipes

The manifest includes this request, itself, both history/freeze fixtures and every semantic
target path. It preserves raw `git status --porcelain=v1 --untracked-files=all --no-renames`
order and two-character statuses. Its path set is independently filtered through
`git diff --name-only HEAD` plus `git ls-files --others --exclude-standard`; content-identical
phantom modifications are not admitted. The NUL-form status must describe the same entries.
Main has zero staged paths.

- manifest paths: 26 (22 modified, 4 untracked)
- manifest bytes: 1,926
- manifest SHA-256: `604d1d3db38fe2ff3a842f3297ea1aae373adc275e3b07b9380a11ff7d8e286f`
- raw NUL-form porcelain bytes: 1,926
- raw NUL-form porcelain SHA-256: `c6b5d14853fe171cef898e330561e0ca9520520f2df0e0c726fb140b107d6377`

- semantic record rows: 22
- semantic record bytes: 3,011
- semantic record SHA-256: `ee4d543751b438da0926efdff92bccca59846f1178a5d80108f9592165c564e0`

Semantic recipe: take manifest paths except every `docs/review/` path and these two exact fixtures:
`tests/OrcaCore.DeveloperSurface.Guards/Fixtures/immutable-document-history.json` and
`tests/OrcaCore.DeveloperSurface.Guards/Fixtures/review-manifest-provenance.json`.
Read raw file bytes; render `path<TAB>byte length<TAB>lowercase SHA-256`, ordinal-sort the rendered
strings, join with LF and exactly one final LF, and hash UTF-8 without BOM. Do not normalize file
line endings or prepend status to this semantic recipe.

The stronger active-freeze recipe includes every target path except the exact self-referential
`review-manifest-provenance.json` fixture. Render `XY<TAB>path<TAB>raw byte length<TAB>lowercase SHA-256`,
ordinal-sort rendered strings, LF-join with one final LF and hash UTF-8. Its final value is pinned
in `activeFreeze` after this request and the immutable-history catalog are complete. Publish that
anchor and the simulated tree outside this self-inclusive request to avoid circular hashes.
Stage exactly the manifest paths in a disposable copy to reproduce the tree; do not stage main
during review. Verify every staged blob equals the raw frozen bytes.

## Contract and document disposition to challenge

1. **Two grants, distinct authorities.** Current eight compiled friends are unchanged.
   `OrcaCore -> OrcaCore.Dag` remains the approved authoring-only six-member grant.
   `OrcaCore.Dag -> OrcaCore.Dag.Hosting` is a proposed ninth grant, limited to an internal
   runtime view. `OrcaCore.Durable.Hosting -> OrcaCore.Dag.Hosting` remains the sole durable
   child bridge. No new package edge, public metadata/factory, codec member, reflection,
   child authority or test friend is admitted.
2. **Exact view.** Design §6 and `artifacts/task-1-1-runtime-view-contract-2026-10-02.md`
   define three internal type families and fifteen consumed method/getter signatures: one
   plan view accessor, one node-list getter, one evaluator, eight descriptor getters and
   four result getters. No constructor, setter, draft, raw plan, delegate, extra internal
   type or overload is allowed. Future metadata guards cover type references as well as
   members, including signatures, generic arguments, inheritance, interfaces and attributes.
   The exact signature block and complete disposition artifact are guard-source pinned.
3. **Successful null is a proposed normative clarification.** Doc 17 §17.2.6 explicitly
   distinguishes a present successful null from a missing output: reference/nullable-value
   types may return null; nonnullable-value null and wrong/missing/foreign/non-direct access
   remain invalid. Mapped input follows the same rule. The current implementation rejects
   all null outputs and is not silently declared compliant. A later independently reviewed
   source slice must implement/test this; no public signature changes here.
4. **Codec ownership stays runtime-side.** The durable bridge decodes successful committed
   direct-dependency outputs before evaluation. Dag returns typed input plus its declared
   type, or `DAG_INPUT_MAPPING_INVALID`, before commit/start. The durable bridge alone
   normalizes fixed-codec input, hashes committed bytes, commits once and starts/reattaches
   children. Resultless dependencies have no output entry. Build/TryBuild never invoke mappers.
5. **Full reconciliation, not a partial friend list.** The disposition artifact accounts for
   CLAUDE, docs 03/08/10/17, Decision 22, architecture, overview and the Task 8.0 map. All say
   ninth proposed, not approved/compiled. Doc 11 NF-002 is explicitly excluded; existing
   workflow-authoring opacity and durable-runtime/workflow-contracts codec requirements are
   unchanged. Historical reshape design and older dated decisions are explicitly history.
   A reverse sweep of active numbered/implementation/root guides finds no contradictory
   public-only DAG or incomplete current friend-list claim.
6. **Ledger-only handoff and future lifecycle.** Only open reshape 8.3–8.5 task text changes,
   with a dated re-sequencing note; the corresponding map rows move codec ownership to
   8.4/8.5. Historical expected-red handoff data is disclosed and not relabelled. No completed
   reshape task or earlier dated decision is rewritten. Authoring Task 3.2's current stale
   completion prose is corrected without changing its immutable closeout artifact.
   Supersession, archive resolution and Proposed withdrawal are rules for the later atomic
   1.3/1.4 target, not newly implemented lifecycle shortcuts. That target must exact-hash
   supersede both reshape and authoring predecessors while retaining their blocks/evidence.
7. **Provenance is honest and preserved.** Reproduction gives 180 rows / 47,327 bytes /
   `e7c608adb01973b855ae0fa9d3b011c0396a48af7cc84b9dc54ead05cc0e5521`:
   173 synchronized, 2 superseded reshape predecessors, 2 pending modifications and
   3 bootstrap-only. The old process artifact is unchanged in the five-entry permanent
   superseded catalog, digest `6991bed8d9783c355bc888af9b4c3b2e4f92076b5c1d36ba847feb0b6b30a277`.
   Canonical 14-directory/preamble records and older owning deltas are unchanged. Active
   directories remain 20 / 1,645 bytes / `dfabdc307ab5c49d6bc15c140b7778a5dce0512faf601c83645574fa0e770d62`.
8. **Pins reflect only intentional edits.** Task 7.3 rows 1/6/7/12/14/18 are refreshed;
   the other sixteen source rows are unchanged. The artifact digest is
   `029a6e0cfda2bb04efa7899f625150d92da67cc76ffffe561d60e5b014b9049c`;
   the Task 8.0 map digest is `e610e21819e61aa44a2b445a3271575049a76cefa2943e2c65ee91c5a507fbb2`.
   No src, canonical spec, API baseline, package-source fixture or declaration crosswalk changes.

## Validation and isolated controls

Author validation: Debug and Release `-warnaserror --no-incremental`, both 0 warnings/errors;
Core 350, Ephemeral 79, Durable 99, Acceptance 37, Hosting 24, ProviderCertification 96,
PostgreSQL 101, SQL Server 72, Integration 11; Infrastructure 240/240 and focused corpus
guards 15/15. Report the same fourteen ExpectedRed failures separately, not as a green suite.
Exact package feed: twelve packages; eight green consumers and the one documented expected-red
`dag-hosting` consumer. OpenSpec strict: 20 passed / 0 failed. The final disposable staged and
committed rehearsal must also be clean and pass Infrastructure before handoff.

Twelve controls were run in the guarded disposable copy against a green synchronization control:

| Control | Mutation | Intended red check |
|---|---|---|
| C1 | Change an evaluator-result signature in design | Signature-block source pin |
| C2 | Reverse successful-null rule in disposition artifact | Whole-artifact source pin |
| C3 | Broaden proposed descriptor/result access | Proposed block source pin |
| C4 | Fabricate ApprovedPending | Exact Proposed registry pair |
| C5 | Close 1.2 early | Required open task state |
| C6 | Substitute activation for process evidence, rebuild guard | Single-parent direct-child evidence |
| C7 | Hide both pending operations | Recomputed actual pending set |
| C8 | Claim semantic approval true | Artifact/derived semantic consistency |
| C9 | Edit retained process provenance artifact | Permanent historical artifact pin |
| C10 | Claim successful-null already approved in doc 17 | Explicit proposal-status clause |
| C11 | Hand-sync proposed canonical block early | Authoring predecessor must stay synchronized |
| C12 | Edit completed authoring delta | Exact predecessor hash |

Every control is red at its intended check; final restored control is green and all twenty-two
semantic target files match main byte-for-byte. The first C3 harness attempt stopped before
mutation because its text anchor was absent; the corrected probe above ran and was red.

## Independent verdict and sequencing

Challenge the signatures, null/public-behavior decision, codec ownership, document coverage,
process evidence and future lifecycle together. Write one immutable dated verdict after review,
without editing the frozen target, prior packets or any canonical/source file. Writing the verdict
will create an extra path beyond this freeze; it belongs in the subsequent evidence commit.

On APPROVE: checkpoint exactly the manifest paths, then add/catalog the verdict as the checkpoint's
sole-child evidence and clear the freeze, then activate Task 1.2 only. No documentation refresh in
activation. Atomic 1.3/1.4 is the next independently reviewed target. Task 8.3 source remains blocked.
