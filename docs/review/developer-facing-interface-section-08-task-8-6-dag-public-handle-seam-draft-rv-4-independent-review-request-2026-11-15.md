# Independent review: DAG public handle seam, draft contract RV-4 focused correction (pre-gate)

Date: 2026-11-15. Parent: da1a4729ea2cdb9474eb219eb507760574f83dcb. Parent tree: d59fbdb4cd1183de9c85a0431b62f5c518442791.
Branch: draft/admit-dag-public-handle-seam. Review the [self-inclusive raw-order manifest](developer-facing-interface-section-08-task-8-6-dag-public-handle-seam-draft-rv-4-dirty-manifest-2026-11-15.txt) as one target.

RV-3 (projected tree `f53f2935285a3d22ea58d9bb22c8174328edf8fa`) resolved the RV-2 blocker. The review accepted D4 and `NotSupportedException` for a submitted DAG run input that fails the byte round-trip, and held Task 0.1 for a focused correction to the friends delta. This target is that correction. All four earlier packets are unchanged; the RV-3 manifest is still 39 rows / 4,326 B / `4c48b4f26e9f5d60e87c9498167cea84a3a17ce70587393db8af5a78b9d6e978`. RV-3's seven changed files are preserved byte-exact under `artifacts/superseded-rv-3-draft-2026-11-15/`.

Still a **design review of a draft contract before its process gate**. No product source, canonical spec, numbered document, guard, fixture or registry row changes.

## What changed since RV-3

Only the friends delta (`developer-facing-surface` "Implementation package boundaries use exact internal friends") and the prototype evidence record changed, plus notes in design §14, the proposal and tasks. The other four deltas are byte-identical to RV-3, and no signature changed.

| Correction requested | Change |
|---|---|
| Route each mismatch through the right result | The ownership clause now says the reopen flag signals a *reopened run* under another run-input contract and is used for reopen only. A start whose key owner has an equal definition triple and a different run-input contract key is reported as `DagStartResult.RunInputConflict`, never through the reopen flag. The cross-host scenario is narrowed to "a different run-input contract key". A new scenario covers an equal contract key with different validated input bytes, which returns `StartIdempotencyConflict`. |
| Name both key comparisons | The contract key recorded on a stored run-input value must equal the key of its own stored graph; a disagreement is corrupt storage with its existing integrity classification. A valid stored graph key that differs from the installed plan's key is a binding mismatch. The integrity check also applies to a start-key owner examined during replay (new scenario). Storage failures, integrity failures and caller cancellation propagate as exceptions from registration, start and reopen alongside the closed results. The "no exception from start" sentence is narrowed accordingly. |
| Narrow the prototype evidence claim | The evidence record now names the suites that passed under the prototype change and states that the changed shared normalizer covers the DAG run input and mapped node input, while child output is copied through a separate unchanged path. The claim about every DAG value is removed. |
| Make committed cancellation on an incompatible host explicit | New scenario: cancellation accepted through a compatible host's handle commits child-cancellation work that any durable host's pump may consume, including a host that skips mapping and admission for that run. |

## What to review

1. The ownership clause and the binding paragraph of the friends delta: is every mismatch now routed through exactly one result, with the reopen flag confined to reopen?
2. The two key comparisons and the exception-preservation sentence: are they precise enough to implement, and consistent with the parent's record validation?
3. The four new or changed scenarios (cross-host start, equal key with different input, corrupt owner during replay, cancellation through a compatible host): do they match the normative text?
4. The corrected evidence record: is the claim now limited to what the prototype runs established?
5. Byte-identity: reversing the eight listed clause edits and removing insertions should still restore all five parent blocks.

## Evidence and limits

- `openspec validate admit-dag-public-handle-seam --strict` reports the change valid (structural only). Each delta still shows exactly one changed canonical line. `repository-foundation` and `management-and-querying` deltas compare byte-identical to their RV-3 copies.
- No proof build, guard run or runtime lane was run for this target. No new prototype run was made; the evidence record's suite counts are from the earlier prototype runs.
- The prototype remains in-process and in-memory evidence; provider certification and a real process restart remain open source-task exits. The scenarios added here (corrupt owner during replay, equal key with different input, cross-host cancellation) were not exercised by the prototype.
- The two owner-chain guards remain red by design until Task 0.1.
- Not prepared: registry rows, guard edits, provenance refreeze, review-catalog fixture entries, document reconciliation.

## Freeze and disposition

- Manifest: UTF-8 without BOM, LF, final LF; byte-equivalent to raw `git status --porcelain=v1 --untracked-files=all --no-renames` and its NUL form converted to LF. It includes itself, this request, and the eight unchanged files of the four earlier packets.
- Semantic record: exclude `docs/review/**`. Each row is path<TAB>raw byte count<TAB>lowercase SHA-256, ordinal-sorted and LF-joined with one final LF: 38 rows / 6,632 B / 8f7904186a8e410e93b8ae9551037a10fe8872838e360cd24854ccb86f3e4f0e.
- Active record: XY<TAB>path<TAB>raw byte count<TAB>lowercase SHA-256, ordinal-sorted and LF-joined with one final LF, with no exclusion. Its anchor and the projected tree are in the handoff to avoid self-reference.
- Independent review only. Leave the target uncommitted. A verdict here activates no checkbox. If approved, the next target is Task 0.1, reviewed on its own.
