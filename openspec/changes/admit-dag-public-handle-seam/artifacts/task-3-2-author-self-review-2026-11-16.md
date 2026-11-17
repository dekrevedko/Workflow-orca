# Task 3.2 author self-review

Date: 2026-11-16. This is a fresh adversarial review of each major step by the implementing agent, not an independent verdict or contract approval. The separate implementation and integration/remediation verdicts remain the authority for their exact reviewed trees. The owner directs preparation of this Complete target; it remains uncommitted for the next review.

## Reviewed checkpoint and evidence step

Verified the NUL-porcelain projection against all 89 manifest paths before staging. The merge is `1e960fc4bf67ad6d1a8c053602d4a58964b47d79`, with ordered parents `598c6d4fb61f7883e25f79f03001aa620dda0bf7` and `ba2b057d7898f516845e90323b4288fb0c1744d8`, and tree `ff4711dca44f7a56abd53bf0854c10332831e814`. Its only direct evidence child is `94e9aca5632bd77d0c6ecde9d7fe689bcdee18d1`: precisely the verdict and two catalogue/freeze fixtures. The verdict is 12,688 LF bytes, SHA-256 `e9c5feddb651cbb570d289027b6c43b27eebc08b92dec2cd907a784468366074`. Both commits are pushed. Neither activates Task 3.2.

## F1 test-harness step

Reproduced the Core guard failure before editing. Removed the redundant provider-certification snapshot poll, signalled actual child admission in the process probe, and bounded the test-side lease request by four acquisitions separated by the provider's five-second lease boundary. The five-second TimeProvider delay represents expiry, not a 25 ms snapshot loop. A fourth unsuccessful acquire fails the test. Cleanup releases the granted fence or cancels the queued request with a separate bounded token. No repository guard exemption was introduced.

Reviewed the test-side lease API and five-second lease/fifteen-second turn assumptions. Fresh PostgreSQL and SQL Server fault and four-process restart cases pass. A provider-specific filter selected zero in-memory cases; no credit is claimed for it. All six CI unit projects, including Core and ProviderCertification, pass. This bounded helper is test infrastructure, not a new product retry policy.

## F2 and F3 documentation step

Replaced stale pending-review text in CLAUDE and both doc 17 status blocks with the actual review chains. Moved the facade paragraph after the runtime-view approval sentence so the latter cannot be read as facade contract approval. Corrected the integration smoke filter to the compiled CurrentSurface group. Child binding failures are explicitly host-configuration behavior outside the five seam requirements, the second route offered by the reviewer; there is no fourth closed-result variant or canonical amendment.

## F4 historical-guard and Complete step

The former live candidate helper now reads the exact implementation and merge blobs and verifies both real direct-child verdict commits. It retains its existing source filename to avoid unrelated file/catalogue churn. Historical source scope no longer depends on a diff from da1a4729 to the current worktree. Historical document projections use committed bytes; the maintained Task 7.3 table hashes current documents directly. The old candidate fixture remains immutable history.

The five rows are Complete with exact predecessor/proposed hashes and owner chains. The Complete record binds both reviewed chains and the owner-direction artifact. The ten predecessor operations become SupersededByApprovedSuccessor. Other Section 8 rows, canonical content and public API remain unchanged. The corpus provenance is measured from the compiled guard, not inferred from an earlier gate.

Self-review found that replacing historical document readers could bypass two live shutdown-document checks. Added an explicit current check for all three registered shutdown-sync paths. Reverting a document to its pre-sync bytes now fails that check. Focused validation also found one stale CLAUDE current-status block digest; corrected the digest after reviewing the changed block.

The full infrastructure lane exposed two stale schema-25 assertions and the task ledger's old 132-complete count. Updated them to schema 26 and 133 complete / 26 pending while preserving exact predecessor comparisons. It also exposed a history-query defect: Git log without merge diffs did not see the review packet first introduced by the approved merge. Both addition queries now include merge diffs and deduplicate commits before comparing immutable bytes. The actual merge packet reproduced this failure before the fix; it must pass as committed history without appearing in the new dirty manifest.

Five negative probes were killed: reverting a Complete stage, forging a reviewed checkpoint, rolling back a shutdown document, changing the canonical friend count, and adding an unlisted owner. A positive probe added a temporary product source file and changed an existing source file; the historical scope guard accepted them, proving the obsolete draft freeze is retired. All probe changes were restored. Current canonical, API, boundary, maintained-document and active-target freeze checks still enforce their own scopes.

## Validation and remaining boundary

Reproduction commands, counters, log/TRX hashes, known initial failures and qualifications are recorded in `task-3-2-activation-validation-2026-11-16.json`. Full CI unit list: Core 390, Ephemeral 414, Durable 113, Acceptance 37, Hosting 79, ProviderCertification 116 (1,149 total); Integration 11. These are local runs of the full CI unit list, not a hosted CI or coverage claim. Real-provider activation reruns cover the changed fault/restart helper. Full 123/94 provider results belong to the reviewed merge and are explicitly inherited. No product code or public signature changed in activation.

The new request and self-inclusive dirty manifest define the next review target. Its five Complete rows and task checkboxes describe this prepared worktree only. It is not committed or pushed, and no independent activation verdict is manufactured. Archival and other Section 8 activation are outside this target.
