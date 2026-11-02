# Issue #16 contract — adversarial author self-review

Verdict: **APPROVE for independent contract review**, no blocking findings.
This is a separate author review, not external approval or permission to implement.
The target remains uncommitted, with canonical/source tasks open and approvals null.

## Frozen target

Base: 0a8e375fbcf5005a2a2f491cf24dfbbd9d64c580.
Branch: codex/issue-16-ephemeral-failure-settlement.
Manifest: contract-manifest-2026-10-29.txt, exactly **26 paths**, self-inclusive.
Candidate tree: cf054886e39c20d9294b3baac55e58fa19c6bbdf.
Content SHA-256: 995a02e57923cbad1073c42c066f7e3c13c1b2c45dcef0318bbb964d5aa026cb.
Porcelain SHA-256 before this excluded verdict:
1e1f69a9ce80ab2f2d55ff842952843df53868477fc597a0761d9136e51ea7d7.
No frozen file drifted. This verdict is excluded for a sole future direct child.

## Findings and decisions checked

- The original exception uses the existing inherited InnerException, by reference,
  without rethrow/reset, reconstruction or exposing a normalization wrapper. The
  new nullable ExceptionType getter serves detached inspection, not a second live
  carrier. Exactly that getter and the inherited base settlement method are proposed;
  no public constructor, setter, exception class, helper or configuration option.
- The existing WF-STEP-UNHANDLED code is reused for non-OrcaCore business-step
  failures; genuine OrcaCore codes remain. Root/returned/final-retry/branch/item,
  equal-error identity, late timeout/cancel and true-aggregate behavior are explicit.
  True aggregates retain ordered detached causes and no invented live aggregate.
  DI/initialization/merge/codec/lifecycle boundaries gain no live original promise.
- The live graph is a narrow explicit exception to ordinary detached queries. Its
  mutability/application references and retirement ownership are acknowledged.
  Engine-held references are cleared; caller-held exceptions cannot be revoked.
  No original object is put in Core/provider DTOs, payloads, persistence or events.
- A five-field failure reader cannot accept an extra type silently. The explicit
  closed version-2 seven-field amendment preserves exact legacy/null-type bytes,
  reads old data, rejects malformed/duplicate/version/type/provenance cases and
  makes no new-to-old binary compatibility claim. The existing codec ID and
  occurrence discriminator stay fixed by this proposal's explicit amendment.
  This exact compatibility decision still requires owner contract approval.
- Settlement requires terminality plus final physical handoffs, not an idle
  WhenSettled epoch or retirement eligibility. The observer owns no physical/host
  lease or payload pin and cannot block retirement or join itself as a new owner.
  Local cancellation, subscription races, retired receipts, host lifecycle,
  correct owner disposal and durable no-I/O rejection have defined outcomes.
- The resource recipe does not pretend a thrown unaccepted start provides a
  handle/proof, or that engine settlement joins fire-and-forget processes. It uses
  external/step ownership or completed host join when no handle exists. Observing
  one's own still-owned body would prevent its return; arbitrary client wait-cycle
  detection is not promised. Per-instance success never authorizes root DI disposal.
- Twenty individual source/consumer scenario rows cover the requested positive,
  negative, ownership, codec, shutdown and native controls. No runtime test or
  executable baseline is changed before approval. Source/native proofs remain
  future gates; the current green lane does not claim the proposed API exists.
- All 206 master provenance rows and all 27 predecessors are preserved. There are
  218 rows, nine pending modifications and three additions, zero semantic approval.
  The new exact nine-pair ownership registration is source-pinned, binds both
  owners and all predecessor/canonical/successor hashes, rejects third owners,
  and requires canonical/source tasks open. Old global approval records are exact.

## Validation

OpenSpec strict **26/26**; Release build **0 warnings / 0 errors**; full
Infrastructure **309/309**. Four semantic probes reject fake source approval,
wrong successor hash (both with the outer artifact hash repinned), and premature
canonical/source checkboxes. All inputs are restored and the control is **1/1**.
ReSharper 2026.2.3.1 on OpenSpecCorpusGuards.cs (Release, Any CPU, net10.0, no-build
after a compatible build) has **0 errors, 50 warnings, 43 notes**, exactly the
reviewed file baseline with no new rule/message instances. Raw SARIF is retained.

The initial validation exposed two temporary MSBuild-isolation failures and the
missing ownership-registration prerequisite. Both are resolved and recorded.
The probe report's hash-string truncation assertion was corrected without changing
the guard's correctly red result. No product failures are represented as green.

## Gate

Tasks 1.1–1.3 record preparation only. Task 1.4 requires independent review and
direct owner approval of this exact target, including the API and wire amendment,
before checkpoint/verdict commits. Canonical transition, source implementation,
push/PR, issue changes and release remain separately gated. The unrelated primary
DAG checkout and published preview.3 claims are preserved.
