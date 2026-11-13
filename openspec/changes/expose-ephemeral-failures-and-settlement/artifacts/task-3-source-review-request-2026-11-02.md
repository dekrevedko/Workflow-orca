# Issue 16 task 3 local source review request — 2026-11-02

Base/HEAD: 5960f0b5e920e2a53b34e38290d20c6e545fbf2a.
Branch: codex/issue-16-ephemeral-failure-settlement.
Canonical checkpoint: ecd188a4b4ee4d45ecdaddc7112523e20f0cc1b1,
tree ae97c26e5c088c3c5430d9fd6644379984d30922; its sole verdict-only child is
5960f0b. The exact 54-path transition was checked for drift before committing.

The source candidate implements the two approved public members, original business
exception identity, ephemeral-only detached type/code capture, strict legacy/v2
codec, final physical-settlement receipt and admission seal. Its source record
binds the real canonical chain while preserving null source approval. Only 2.1/2.2
are newly checked; all task 3 boxes remain open. There are 18 product paths, with
no durable/provider implementation, public constructor, option, hook, friend edge
or package dependency change. Matching compiled API, compile/package and metadata
baselines move with source. Old canonical, numbered and approval artifacts remain
unchanged.

Checks: OpenSpec 26/26; Release warnings-as-errors 0 warnings/0 errors;
six unit projects 1,043/1,043 (372 Core, 405 ephemeral, 103 durable, 37 acceptance,
24 hosting, 102 certification); Infrastructure 314/314; SQL Server 75/75;
PostgreSQL 104/104; integration 11/11; benchmark smoke and combined native/release
negative gates 15/15. Complete CI-style engine coverage is 79.2004%
(16,046/20,260), including Infrastructure and the final expanded ephemeral suite.
Twenty consecutive paired unfiltered dotnet test runs pass 405/405 each.

Twenty-two focused source mutations are killed; zero survivors, timeouts, compile
errors, uncovered selections or harness errors in the final campaign. Exact final
production hashes still match; four later test-only root/recovery cases strengthen
the same controls. Earlier failed/setup attempts are preserved and excluded.
ReSharper 2026.2.3.1 runs with Release/Any CPU/net10.0, reports zero errors and
retains scoped SARIF plus every changed-line warning disposition. The broader
report has 492 warnings/200 notes; the final supplemental test/consumer report has
25 warnings/45 notes. These are per-report counts, not a warning-free claim.

Fresh public PackageReference consumers: 84/84 JIT cases, all original journeys
and groups, generated/legacy type parity and resource recipes with/without a
handle and an explicitly unjoined application child. A separate seven-product-
package durable consumer passes output, unsupported settlement, pre-cancellation
and disposed-owner controls. Fresh caches, explicit feeds, package SHA-512s and
actual DLL identities are checked. Durable failure bytes match the base witness.

**Qualification hold:** no actual six-cell native jobs or aggregate exist for
this new source. The Windows 11 dirty-source preflight deliberately exits with
Unqualified native host. Protocol 4 preserves the old corpus, adds FS witnesses
and requires 392 cases across Windows Server 2025/Ubuntu 24.04 JIT/trimmed/AOT.
The owner's no-push instruction remains in effect. No new native support or
publication claim follows from local green checks. External independent approval
and complete task 3 checkpoint authority remain pending.

Provenance remains 218 rows, zero pending and 28 untouched predecessors; its
record hash remains f6e5ed0ad28a597fbbea17a62a10c9cfd6e05f580e0051f62f51d558d7a24783.
The exact self-inclusive path list is task-3-source-manifest-2026-11-02.txt.
Candidate tree, per-file/content/status hashes and cleanup receipt are in
`.artifacts/publishing-validation/issue-16-source-2026-11-02/`.
The adjacent author report is adversarial self-review, not external approval.
Keep this candidate uncommitted. No push, PR, issue edit, merge, archive or release.
