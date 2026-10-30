# Preview.3 release preparation — review request

Base: 82811cd41aae577d8a1ae13258790d3da0c30d72 on
codex/release-0.1.0-preview.3. PR #14 is merged with two parents; issue #13 is
closed as completed. Master CI 40821352504 is green and local master matches
origin/master. The unrelated primary checkout is untouched.

This target completes issue #13's two publication/merge checkboxes with a pinned
Git-backed completion receipt, updates registry schema 24 without modifying
prior records, prepares preview.3 notes and adds the exact-release-feed native
gate. The native consumer accepts an explicit prerelease version; nine controls
cover mismatched/missing versions and actual consumer materialization. Existing
historical native evidence now checks its harness through approved Git snapshots;
current product-source hashes and all historical approval pins stay enforced.

Product source, public APIs, canonical/delta requirements, executable baselines,
provenance and prior verdict/evidence files are unchanged. The release plan keeps
preview.2 installation facts and native limitations truthful. No new release,
tag, registry push, branch push or PR is part of this local target.

Validation and limitations are recorded in validation.json and
package-validation.json; ReSharper's raw report is retained. The manifest is
self-inclusive, excludes only independent-review-verdict-2026-10-28.md for a
separate child, and covers all checkpoint paths. Exact tree, content/status
hashes and per-file bytes are in
`.artifacts/publishing-validation/release-0.1.0-preview.3/freeze.json`.

The author performs an adversarial self-review independently from the editing
pass. This is not external approval or authorization to publish. Approval of the
exact frozen target is needed before its checkpoint and publication steps.
