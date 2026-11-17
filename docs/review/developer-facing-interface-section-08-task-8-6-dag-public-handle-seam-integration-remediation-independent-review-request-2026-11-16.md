# Task 8.6 integration and findings review request

Date: 2026-11-16. Target worktree: X:/ocv302. Branch: draft/admit-dag-public-handle-seam.

## Exact scope and authority

The approved 130-path implementation is committed as b9dcdef7e611da45d7dc4f75946ee192fc83c37e, tree 8ef5842bb8c7952f30f0f4184e97baf45ba986c0, on da1a4729ea2cdb9474eb219eb507760574f83dcb. Its only direct evidence child is 598c6d4fb61f7883e25f79f03001aa620dda0bf7. That child adds/catalogues the exact implementation verdict and clears the old active freeze, with no activation.

This is a separate, uncommitted merge/remediation target on HEAD 598c6d4fb61f7883e25f79f03001aa620dda0bf7. MERGE_HEAD is ba2b057d7898f516845e90323b4288fb0c1744d8. The real index contains resolved merge entries; later remediation is also unstaged. A copied index projects the complete target without changing that real index. There are no unresolved merge entries. Do not reset or abort the pending merge when reviewing it.

The prior reviewer authored RV-1 through RV-4 and excluded independent contract approval. That limitation is retained. The five delta blocks and public signatures are unchanged. Owner-approved RV-4 and recorded combined-delivery direction remain the contract/process basis.

## Findings addressed

- F1: canonical corruption fixtures prove byte-identical framing before changing either the value key relative to its own graph, or JSON bytes with a valid fingerprint. Exact new integrity exceptions are required on reopen and start-key replay. A real Ready node is observed through scheduler rejection; no mapper or tail mutation occurs, and restored storage lets that same node finish. Direct mapping-reader and poison-reconciliation assertions cover their boundaries. One production mutation removing only the owner integrity check is killed by six cases across all three providers; database failures specifically identify corrupt replay.
- F2: integrates the named main tip, including its canonical shutdown requirement and nine Ephemeral source files. Scope guards verify those nine exact mainline blobs plus the fifteen facade files, never a broad directory exclusion. Three probes (incoming source drift, facade source drift, unlisted source) are killed. Both parent provenance histories remain exact. The full canonical tree is main plus exactly the five reviewed facade blocks.
- F3: missing or incompatible installed child configuration throws public InvalidOperationException before a start effect or key binding. Tests cover missing and mismatched installations. True storage-integrity errors retain InvalidDataException. No additional union variant or public signature is added.
- F4: doc 17 and design explicitly state fail-closed behavior for corrupt stored run input across reads, waits, cancellation, scheduler/notification reads and internal poison reconciliation, distinct from a valid faulted checkpoint.
- F5: corrects the mapper-delegate wording and removes the obsolete reflection comment.

Tasks 0.2, 1.2 and 2.7 record the actual implementation approval/checkpoint/evidence with its scope limitation. Task 3.2 and the five Proposed rows remain pending; this candidate does not claim approval for its own merge or activate Complete before that review.

## Evidence and review questions

Read artifacts/integration-remediation-validation-2026-11-16.json, integration-remediation-self-review-2026-11-16.md, integration-byte-verification-2026-11-16.json, integration-candidate-openspec-provenance-2026-11-16.md and implementation-review-record-2026-11-16.{json,md} under openspec/changes/admit-dag-public-handle-seam/. Raw build/test/mutation logs and TRX results are retained in X:/dag-seam-followup-20261116/.

Review whether F1 reaches the new integrity checks (especially replay), the real-node leg proves no mapping/effect, and the mutation fails for the intended reason. Review all nine conflict resolutions and verify both parents' canonical/source/evidence changes remain. Check the exact pending-merge ancestry and source exclusions, F3 exception boundary, F4 wording, the unchanged 3-type/17-member seam, and the actual approval scope recorded for the task checkboxes.

The initial provider evidence timeout was a null-logger test setup defect, corrected before passing runs. The initial infrastructure pass exposed a text-encoding mistake and missing provenance claims; those are corrected, not waived. Existing JsonNode corruption tests remain under a narrower name. ExpectedRed obligations are reported separately and earn no completion credit. Strict OpenSpec proves structure only.

## Checkpoint and activation boundary

Approval of this target may authorize its exact two-parent merge checkpoint and a direct child that catalogues the new verdict and clears its freeze. Complete activation must bind real approval/checkpoint/evidence, retaining both historical chains. No future identifiers or approvals are invented here. The original implementation checkpoint/evidence chain is immutable and contains no activation.

The self-inclusive raw-status manifest is `docs/review/developer-facing-interface-section-08-task-8-6-dag-public-handle-seam-integration-remediation-dirty-manifest-2026-11-16.txt`. The handoff reports its raw/content hashes and copied-index tree outside this document to avoid a self-referential digest. All earlier packets are unchanged.
