# Approved-pending DAG friend successor provenance

Date: 2026-09-27

This is the atomic Task 1.3/1.4 review target. It synchronizes exactly two independently
approved `MODIFIED` requirement blocks and advances their separate registry stage to
`ApprovedPending`. It does not add the `OrcaCore -> OrcaCore.Dag` compiled friend or authorize
Task 8.2 product source. The predecessor deltas and the earlier proposed-stage artifact remain
unchanged and permanently discoverable.

## Approval and transition identity

- Reviewed contract checkpoint: `89a2b475ebaeed82de2fd2edaf1f31b371bd71a4`.
- Its direct-child approval evidence commit: `bbac0977bffce25881a7f27f48b075cda7dc206f`.
- Immutable approval verdict: `docs/review/developer-facing-interface-section-08-task-8-2-dag-friend-contract-yyy-1-remediation-independent-review-verdict-2026-09-27.md`, 10,269 bytes, SHA-256 `398031734b5b98d3244e19e85f519c464aae80f2faf843e0c34d3b0c180cdf2f`.
- The old `Proposed` records remain in `post-gate-amendment-path.json`; two new
  `ApprovedPending` records bind the exact before/after blocks. The permanent Section 7B
  amendment entry is unchanged. Task 1.2 is complete; Tasks 1.3/1.4 form one frozen target;
  Task 2.1 and all Task 8.2 product work remain open.

| Capability | Requirement | Pre-sync canonical and retained predecessor delta SHA-256 | Approved successor/canonical SHA-256 |
|---|---|---|---|
| `developer-facing-surface` | Implementation package boundaries use exact internal friends | `e12c77e5d3a8eaf30dbe31a68ffe4baec3b105023dd5f1512d3f062917a312d0` | `bed102a2e4c98598b30cbb741c956a236f2050d27d88e6fc567b915f5b260793` |
| `repository-foundation` | Dependency direction remains one-way | `d0d512ea59ea8da595770b5437d7faa7565773c6cd8845850d2dea6010b54b29` | `bbae0c226c6824570650d1f6f980e3b74e6c35cb196784581eef20e6e3563ce9` |

Both canonical preambles and every other requirement block retain their prior bytes and order.
The first successor adds two scenarios; the second adds one. No deletion or heading rename occurs.
Numbered CP-020 and PR-005 now state the exhaustive DAG dependency exclusion as “No OrcaCore
package other than `OrcaCore.Dag.Hosting`,” closing the prior review's ambiguity without adding
a package edge. Reshape's older change-local Decision 22 wording remains dated rationale, not the
current binding friend enumeration.

## Reproduction

Enumerate every non-archived `openspec/changes/*/specs/*/spec.md` requirement. Normalize blocks
to LF, strip trailing empty lines, and stop at the next requirement or `##` heading. The two
registered predecessor rows classify as `SupersededByApprovedSuccessor` only when their exact
source-pinned historical block hashes remain and canonical matches the approved successor.
Render each row as
`change<TAB>capability<TAB>capability-kind<TAB>operation<TAB>requirement<TAB>state<TAB>delta-block-sha256<TAB>canonical-block-sha256-or-dash`,
sort ordinally, join with LF and one final LF, and hash UTF-8 without a BOM.

- record rows: 178
- record bytes: 46,784
- record SHA-256: `0dd47120d11d2442fccc217902386dd36d4bbf57b7fb476bd7c670dbbbaa5257`
- synchronized operations: 173
- pending canonical operations: 0
- declared new-capability requirements outside the canonical set: 3
- duplicate `(capability, requirement)` owners: 2 exact registered predecessor/successor pairs; 0 unregistered pairs
- semantic approval eligible: yes

| State | Count |
|---|---:|
| `Synchronized` | 173 |
| `SupersededByApprovedSuccessor` | 2 |
| `PendingAddition` | 0 |
| `PendingAddedCanonicalConflict` | 0 |
| `PendingModification` | 0 |
| `PendingRemoval` | 0 |
| `NewCapabilityOutsideCanonical` | 3 |

## Canonical-capability inventory

The 14 canonical capability directories remain unchanged, ordinal-sorted with `/` and one final LF.

- canonical capability directories: 14
- record bytes: 547
- SHA-256: `7165dac4e1a57022a7890b421f522bf4152f6d5ddc159a41539ce2ef0d18ec9f`

The 14 normalized canonical preambles remain unchanged: 1,233 record bytes and SHA-256
`595528c6a7ba56dd5648e7fc12ac6bc2af9e9bf6fa3fbf853b86fffdd7cecd3c`.

## Capability-directory inventory

Every active `openspec/changes/*/specs/*/` directory is rendered as a repository-relative
forward-slash path with one trailing slash, ordinal-sorted, LF-joined with one final LF.

- capability directories: 18
- record bytes: 1,488
- SHA-256: `da7c00f44cc9e9d14b6756e5572d8b461c56d5454e315c4bec357d9d51fe4e50`
- directories lacking `spec.md`: 0

## Pending operations and ownership

| Canonical capability | Pending | Turns green |
|---|---:|---|
| **Total** | **0** | **Tasks 1.3 and 1.4 are the joint reviewed transition** |

The successor blocks now match canonical verbatim. Their predecessor blocks are still present
under the reshape delta and retain the exact pre-sync canonical hashes above, but are no longer
misreported as unresolved canonical operations. An added or altered owner, altered approval path,
premature implementation task completion, or product-source friend grant remains outside this
review target and fails its owning gate. Structural OpenSpec validation alone is not approval of
Task 8.2 implementation.
