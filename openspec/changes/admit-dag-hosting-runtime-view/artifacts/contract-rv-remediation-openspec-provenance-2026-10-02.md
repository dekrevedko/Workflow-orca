# Proposed DAG hosting runtime-view contract RV-remediation provenance

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
| `developer-facing-surface` | Implementation package boundaries use exact internal friends | `bed102a2e4c98598b30cbb741c956a236f2050d27d88e6fc567b915f5b260793` | `3a848931f75b44c6cf2edf97b564c6e213b23d90720701a31a4a45394cbf93ab` |
| `repository-foundation` | Dependency direction remains one-way | `bbae0c226c6824570650d1f6f980e3b74e6c35cb196784581eef20e6e3563ce9` | `f0dbc156d044536a83d73934dfcb6eaac5f974069b1a5c555433f2a4b56a1ab8` |

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
- record SHA-256: `79f1638357e3b1aa35aef28a4f7c45233acc135036d5f6c1b274e687d454b947`
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
The permanent superseded catalog now holds six entries. Render `path<TAB>normalized-sha256`,
ordinal-sort, LF-join with one final LF: 1,041 bytes,
SHA-256 `0eae8ac75f83fbfcba744f736a4acd8c71acb02e7718f50e95f47cf06df69c6c`.
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
disposition and signatures are recorded in `task-1-1-runtime-view-contract-rv-remediation-2026-10-02.md`.

## Rejected contract preserved; exact normative boundary repaired

The first contract request/manifest/REJECT remain byte-exact and catalogued. Its dated
`contract-openspec-provenance-2026-10-02.md` is retained unchanged at normalized SHA-256
`524053f5cf11e078aa2d6221c8105c51e040b9ba7cbcaedd85f3a9d90580e23e`, as the sixth permanent
superseded entry. Its old record is 180 / 47,327 bytes /
`e7c608adb01973b855ae0fa9d3b011c0396a48af7cc84b9dc54ead05cc0e5521`; no historical claim is rewritten.

Both newest normative deltas now contain every signature directly; doc 17 §17.2.6 contains
the identical proposed signature block, null policy and explicit mapper/decode failure rules.
Doc 08 points there and doc 17 §17.5 explicitly retains current eight plus proposed ninth.
The guard independently pins these numbered proposals and the open reshape 8.3–8.5 handoff.
Task 1.2 is left unpinned so legitimate checkbox-only activation stays green; the later
1.3/1.4 transition must bind that completion to actual approval evidence. All other
canonical/source tasks remain open. No canonical, source, friend, codec or public surface changes.
