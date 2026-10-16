# Independent review — atomic DAG runtime-view canonical/registry transition

Date: 2026-10-03
Change: `admit-dag-hosting-runtime-view`, combined Tasks 1.3/1.4
Base activation: `64d97c644475f6dfbe183540bf546c7ab48eab46`
Manifest: [developer-facing-interface-section-08-task-8-3-runtime-view-atomic-canonical-transition-dirty-manifest-2026-10-03.txt](developer-facing-interface-section-08-task-8-3-runtime-view-atomic-canonical-transition-dirty-manifest-2026-10-03.txt)

## Bounded authority and approved chain

Review this single uncommitted canonical/process target. The independently approved
RV-remediation contract is checkpoint `43d869fc29e7daa3ec567d4602960f458eb98492`,
tree `5c9d63fe0b30ea4465054ac01c986a8b80707e62`, parent
`2a09b452af067fbd501215a572712ff08cf2bbc9`. Its only-child evidence commit
`1ea7f44b5a32d058e04b913f387317c21747add5` adds the verdict byte-exact:
10,608 bytes, SHA-256 `5ff6b3322d3071149fefe583fd74fba35afa5d28ea6a81ca50637bcdb029b457`.
Activation/base above changes Task 1.2's checkbox only. All previous requests, manifests,
REJECT/APPROVE verdicts and dated contract/provenance records are unchanged.

APPROVE authorizes only this exact checkpoint. It is not the ninth friend attribute,
runtime-view implementation, codec access, child-start authority, public API growth,
Task 8.3 source completion, Task 8.4/8.5 bridge approval, archival or withdrawal.
Current compiled friends remain eight. Task 1.3 is prepared complete; 1.4 stays open
for this independent checkpoint gate and is deliberately unpinned so its subsequent
checkbox-only activation needs no guard edit. Source tasks 2.1–3.2 remain open and pinned.
No forward implementation starts while this target is awaiting independent review.

## Self-inclusive freeze and exact recipes

The manifest includes this request, itself, both admission/freeze fixtures, all semantic
files and both new artifacts. Preserve raw `git status --porcelain=v1 --untracked-files=all --no-renames`
order. Intersect tracked paths with `git diff --name-only HEAD` and include untracked
files; content-identical status phantoms are forbidden. The main index is empty.

- manifest paths: 25 (21 modified, 4 untracked)
- manifest bytes: 1,842
- manifest SHA-256: `dc96f1d0b4a177fe5f1ac90c4224ca352d4f5904bb2a7b5712b69ab1e33e2c9b`
- semantic record rows: 21
- semantic record bytes: 2,823
- semantic record SHA-256: `e84da5f605a0e94b19eb97c271583aa0c92384a51a9c7ec7346c9deaaae83493`

Semantic recipe: omit every `docs/review/` path and exactly the two fixture paths
`immutable-document-history.json` and `review-manifest-provenance.json` in
`tests/OrcaCore.DeveloperSurface.Guards/Fixtures/`. Read raw file bytes, render
`path<TAB>raw byte length<TAB>lowercase SHA-256`, ordinal-sort the complete rows,
LF-join with one terminal LF, hash UTF-8 without BOM. Do not normalize file bytes.

The stronger active record includes all manifest paths except the exact self-referential
review-manifest fixture. Render `XY<TAB>path<TAB>raw byte length<TAB>lowercase SHA-256`,
ordinal-sort, LF-join with one final LF and hash UTF-8. Its byte length/hash is pinned in
the active-freeze fixture after this request and immutable catalog are final.
The final active record and simulated tree are published in the handoff, outside these
self-inclusive bytes, to avoid circular hashes. Rehearse exactly the manifest in a guarded
disposable copy and compare every blob to raw bytes; do not stage or commit main during review.

## Claims to challenge

1. Exactly two canonical requirement blocks change, under verbatim headings:
   - developer-facing-surface: Implementation package boundaries use exact internal friends;
   - repository-foundation: Dependency direction remains one-way.
   Both equal their already-approved unchanged deltas byte-for-byte. Every preamble,
   unrelated requirement/scenario, requirement order and other canonical file is unchanged.
   Current hashes are `3a848931f75b44c6cf2edf97b564c6e213b23d90720701a31a4a45394cbf93ab`
   and `f0dbc156d044536a83d73934dfcb6eaac5f974069b1a5c555433f2a4b56a1ab8`.
2. Schema 7 preserves both Proposed arrays and completed authoring approval/source evidence
   as immutable history. Two new ApprovedPending rows bind the real contract checkpoint,
   tree, direct-child evidence, added byte-exact verdict with one final APPROVE line,
   and completed 1.2/1.3 tasks. Four exact predecessor identities (reshape and authoring,
   for both headings) are superseded only at their old hashes and newest canonical hashes.
   No broader drift waiver, third heading, fourth owner or rewritten predecessor is admitted.
   Historical owners resolve through exactly one active or dated archived change. Actual
   archival still requires its own reviewed inventory/path refreeze; no archival is performed.
3. Provenance re-derives from the corpus: 180 rows / 47,347 bytes /
   `40d4d8c0b9144ea087d7b36d33df35d4736942ae615f50126f7ba136b20da8c1`:
   173 synchronized, four superseded predecessors, zero pending, three bootstrap-only.
   Semantic approval is eligible for this canonical target, not an implementation approval.
   Seven permanently superseded provenance artifacts render 1,222 bytes /
   `a4c8f08e730b3f0b9482dbf528af02dd33914f8986cdd509963899b169564739`.
   Canonical inventory is 14 / 547 / `7165dac4e1a57022a7890b421f522bf4152f6d5ddc159a41539ce2ef0d18ec9f`;
   preambles 14 / 1,233 / `595528c6a7ba56dd5648e7fc12ac6bc2af9e9bf6fa3fbf853b86fffdd7cecd3c`;
   active directories 20 / 1,645 / `dfabdc307ab5c49d6bc15c140b7778a5dce0512faf601c83645574fa0e770d62`.
4. Both new non-blocking observations are addressed explicitly:
   - The original rejected contract artifact is permanently superseded, unchanged, at
     `5527189563e0f39eccbb9e56bc902d2e1e4cc9e2695e0db929cc5e4b03a7dc12`.
     The approved RV-remediation artifact is separately retained at
     `2dec007df0e06740373fdc4c4d0cd69066d0d08cda417f141a3897a4b3824398`.
     Change-relative paths, exact fixture lists and independent guard-source hashes protect
     both independently of the temporary freeze and routine current-match refresh.
   - The transition confirms eager bridge decoding of every successful direct resultful
     output, including unused outputs: ordinary decode failure is DAG_INPUT_MAPPING_INVALID
     before mapper/commit/start. The unchanged durable-runtime and immutable-authoring
     owning blocks are independently hash-pinned, not silently amended. Reshape 8.4 must
     confirm this in its reviewed bridge contract and 8.5 must implement/test it. Codec,
     fingerprints, committed input and child work remain on the durable bridge.
     Only OutOfMemoryException exclusion is in-process testable; stack-overflow and
     access-violation exclusions are process-integrity policy, not catch/test claims.
5. Numbered/binding/guide docs say independently approved and canonical, not compiled.
   All 22 Task 7.3 rows/digest, numbered contract/status hashes, reshape handoff and Task 8.0
   map are refreshed together. Dated Decision 22 entries are unchanged; a new dated entry is
   appended. No completed reshape task is reworded. No src/**, friend, package edge, codec,
   public API baseline, counted test declaration or crosswalk fixture changes.

## Author verification and mutation controls

Debug/Release non-incremental warn-as-error builds: zero warnings/errors. Core 350,
Ephemeral 79, Durable 99, Acceptance 37, Hosting 24, ProviderCertification 96;
real PostgreSQL 101, SQL Server 72, Integration 11. Infrastructure 240/240; exactly
fourteen documented ExpectedRed failures, no unexpected reds. OpenSpec strict 20/20.
Twelve fresh exact packages; eight green consumers and dag-hosting as the one expected red.
Canonical/ownership focused controls pass 2/2 in both configurations. The final frozen and
committed-state rehearsal results are reported separately in the handoff.

Probes were confined to a HEAD/root-verified disposable worktree, with no active freeze
masking the canonical check. Restore all target files byte-exact and rebuild mutated guards.

| Control | Mutation | Result |
|---|---|---|
| A1 | Check 1.4 only, no guard edit | GREEN, focused 2/2 |
| C1 | Coherently repoint fixture + guard evidence to activation | RED, direct-child parent |
| C2 | Coherently repoint reviewed checkpoint to activation | RED, evidence parent |
| C3 | Forge current Complete stage | RED, exact ApprovedPending rows |
| C4 | Edit original superseded contract | RED, permanent source hash |
| C5/C6 | Edit authoring/reshape registered predecessor | RED, old block hashes |
| C7 | Drop a superseded row | RED, supersession state |
| C8 | Alter unrelated canonical requirement | RED, recomputed record |
| C9/C14 | Reopen contract approval/canonical sync | RED, exact task state |
| C10 | Close source task 2.1 | RED, source stays open |
| C11 | Weaken eager-decode transition artifact | RED, independent whole-file hash |
| C12 | Forge semantic approval flag | RED, live artifact agreement |
| C13 | Claim numbered runtime view is compiled | RED, durable numbered block hash |

The first C6 construction edited a different trailing block and correctly hit the generic
record check; it was rerun inside the registered predecessor and hit its exact historical
hash. C11 initially used a nonmatching text anchor and changed nothing. Before resuming,
every disposable target path was verified byte-identical to main. No historical material
or main working file was changed by either harness issue.

## Verdict and sequence

Challenge approval ancestry/addition, the exact four supersessions, historical preservation,
canonical-only scope, runtime-owner consistency and activation behavior independently.
Write a new immutable verdict without editing this target. It is an extra path for the later
evidence commit, not part of this freeze. APPROVE authorizes exact manifest checkpoint,
then only-child evidence adding/cataloguing that verdict and clearing the freeze,
then activation checking 1.4 only. The source/member-reference target comes afterwards
under its own review; no friend attribute or implementation starts while this review is pending.
