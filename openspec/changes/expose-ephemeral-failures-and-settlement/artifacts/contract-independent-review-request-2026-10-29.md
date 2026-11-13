# Issue #16 contract — independent review request

Base: 0a8e375fbcf5005a2a2f491cf24dfbbd9d64c580, GitHub-synced master.
Branch: codex/issue-16-ephemeral-failure-settlement.

Two exact application members are proposed: WorkflowFailure.ExceptionType and
WorkflowInstanceHandle.WaitForSettlementAsync. The existing wrapper's
InnerException carries the original ephemeral business-step exception; the
nullable string is only detached metadata. The closed legacy/v2 failure wire
amendment, single/aggregate/retry/late-failure rules, passive receipt, host/durable
boundaries and no-handle resource caveat are explicit, with twenty scenario rows.

Twelve pending canonical operations span five capabilities. Nine existing-owner
modifications require an exact source-pinned proposed registration; all prior
owners/hashes and global approval records remain intact. Approval fields stay null,
canonical/source tasks stay open and semantic approval stays false. Product source,
canonical/numbered text, companion and runtime API/compile/package baselines are
unchanged. The only tracked process edits are provenance fixture pins and the
OpenSpec guard's narrowly validated nine-pair proposal registration.

Fresh checks: OpenSpec 26/26; Release 0 warnings/errors; full Infrastructure 309/309;
four semantic mutation probes caught with restored control 1/1; ReSharper zero
errors, 50 existing warnings and 43 existing notes, no new findings. Raw SARIF and
compact validation evidence are retained. Temporary-probe isolation and the initial
missing-owner prerequisite are classified honestly in the validation record.

The manifest includes itself and every checkpoint path, excluding only
contract-independent-review-verdict-2026-10-29.md for a separate future child.
Exact candidate tree, status/content hashes and per-file bytes are in
`.artifacts/publishing-validation/issue-16-contract-2026-10-29/freeze.json`.
Raw SARIF retains CRLF; use Git's cr-at-eol whitespace handling without changing
approved report bytes. The author self-review is not external approval.

Tasks 1.1–1.3 record preparation only. Task 1.4 and all later gates remain open.
Keep the target uncommitted for independent contract review and explicit owner
approval. No canonical sync, source implementation, push, issue edit or new release
has been authorized by this preparation.
