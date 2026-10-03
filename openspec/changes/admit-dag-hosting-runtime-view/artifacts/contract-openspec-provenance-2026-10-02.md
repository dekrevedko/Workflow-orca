# Proposed DAG hosting runtime-view contract provenance

Date: 2026-10-02

This is the Task 1.1 contract-review target for `admit-dag-hosting-runtime-view`.
The process gate is checkpointed at `dc8095c5536316cb772c985641e45179fa3c93b5`,
its sole-child approval evidence is `0f4fafa3c3b3acfdb2c39227bba53f094782bd13`,
and its checkbox-only activation is `2a09b452af067fbd501215a572712ff08cf2bbc9`.
The registered process verdict is 11,549 bytes, SHA-256
`bc72cd0eca94e77a1c09744e18aea6bb0133cfe5429ddf5a50fdfb4e0c70cd4c`.
That approval does not approve this contract or authorize source implementation.

## Requirement ownership

The two exact ownership chains remain
`reshape-developer-facing-interfaces -> admit-dag-authoring-friend-boundary -> admit-dag-hosting-runtime-view`.
Both completed authoring blocks remain canonical and byte-unchanged. The two runtime-view
rows remain `Proposed`; their revised full MODIFIED blocks are pending, not synchronized.
No fourth owner is admitted. Neither canonical specs nor product source changes here.

| Capability | Requirement | Completed predecessor / unchanged canonical SHA-256 | Proposed runtime-view block SHA-256 |
|---|---|---|---|
| `developer-facing-surface` | Implementation package boundaries use exact internal friends | `bed102a2e4c98598b30cbb741c956a236f2050d27d88e6fc567b915f5b260793` | `48217f568968e2c8b96edf99c080470eabe9a46e59a93e5c962977918efceb2b` |
| `repository-foundation` | Dependency direction remains one-way | `bbae0c226c6824570650d1f6f980e3b74e6c35cb196784581eef20e6e3563ce9` | `7f637ce36ef4ff035c2837e138908326e032bdbb83963c9d4e564b17747f5c08` |

## Reproduction

Enumerate every non-archived `openspec/changes/*/specs/*/spec.md` requirement.
Normalize blocks to LF, strip trailing empty lines, and stop at the next requirement or
`## ` heading. Only the two exact reshape predecessors classify as
`SupersededByApprovedSuccessor`, through their pinned historical hashes and completed
authoring successor. The proposed runtime-view blocks classify as `PendingModification`.

Render `change<TAB>capability<TAB>capability-kind<TAB>operation<TAB>requirement<TAB>state<TAB>delta-block-sha256<TAB>canonical-block-sha256-or-dash`,
sort rendered lines ordinally, join with LF and one final LF, and hash UTF-8 without a BOM.

- record rows: 180
- record bytes: 47,327
- record SHA-256: `e7c608adb01973b855ae0fa9d3b011c0396a48af7cc84b9dc54ead05cc0e5521`
- synchronized operations: 173
- pending canonical operations: 2
- declared new-capability requirements outside the canonical set: 3
- duplicate `(capability, requirement)` owners: 2 exact three-owner chains; 0 unregistered chains
- semantic approval eligible: no

| State | Count |
|---|---:|
| `Synchronized` | 173 |
| `SupersededByApprovedSuccessor` | 2 |
| `PendingAddition` | 0 |
| `PendingAddedCanonicalConflict` | 0 |
| `PendingModification` | 2 |
| `PendingRemoval` | 0 |
| `NewCapabilityOutsideCanonical` | 3 |

The preceding process record is retained unedited at
`openspec/changes/admit-dag-hosting-runtime-view/artifacts/process-openspec-provenance-2026-09-30.md`,
normalized SHA-256 `91c0cba8e867c03f41d8ec26c0aec7a194fdf36e5302b37d9541492ad58ae0c1`.
The permanent superseded catalog now holds five entries. Render `path<TAB>normalized-sha256`,
ordinal-sort, LF-join with one final LF: 875 bytes,
SHA-256 `6991bed8d9783c355bc888af9b4c3b2e4f92076b5c1d36ba847feb0b6b30a277`.
The prior 178-row authoring-closeout record remains 46,784 bytes /
`0dd47120d11d2442fccc217902386dd36d4bbf57b7fb476bd7c670dbbbaa5257`.

## Canonical-capability inventory

Render repository-relative `openspec/specs/*/` directory paths with forward slashes and one
trailing slash, ordinal-sort, LF-join with one final LF.

- canonical capability directories: 14
- record bytes: 547
- SHA-256: `7165dac4e1a57022a7890b421f522bf4152f6d5ddc159a41539ce2ef0d18ec9f`

Canonical preambles remain 14 / 1,233 bytes /
`595528c6a7ba56dd5648e7fc12ac6bc2af9e9bf6fa3fbf853b86fffdd7cecd3c`.
All canonical files are unchanged.

## Active capability-directory inventory

Render repository-relative active `openspec/changes/*/specs/*/` directory paths with forward
slashes and one trailing slash, ordinal-sort, LF-join with one final LF.

- capability directories: 20
- record bytes: 1,645
- SHA-256: `dfabdc307ab5c49d6bc15c140b7778a5dce0512faf601c83645574fa0e770d62`
- directories lacking `spec.md`: 0

## Pending operations and proposed contract

| Canonical capability | Pending | Turns green |
|---|---:|---|
| `developer-facing-surface` | 1 | admit-dag-hosting-runtime-view task 1.3 |
| `repository-foundation` | 1 | admit-dag-hosting-runtime-view task 1.3 |

The separately reviewed contract is required before atomic Tasks 1.3/1.4 can synchronize
canonical and implement the registry transition. Supersession, archival and withdrawal rules
are documented here, not implemented in the current guard. Task 1.1 is prepared; 1.2 through
3.2 remain open. Product source, friend attributes and public APIs are unchanged.

The proposal names the two DAG-related grants separately and defines exactly three internal
runtime-view types and fifteen method/getter signatures. It explicitly proposes successful-null
behavior in doc 17 and assigns dependency-output decoding, fixed detached codec normalization,
mapped-input fingerprinting and once-only commit to the durable bridge (reshape 8.4/8.5).
Reshape 8.3 receives already decoded successful outputs and hands typed input plus its declared
type to that bridge; no authoring codec access or test friend is proposed. The full document
disposition and signatures are recorded in `task-1-1-runtime-view-contract-2026-10-02.md`.
