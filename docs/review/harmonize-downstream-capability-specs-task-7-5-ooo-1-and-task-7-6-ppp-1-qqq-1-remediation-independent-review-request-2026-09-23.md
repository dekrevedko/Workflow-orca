# Harmonization Task 7.5/7.6 PPP-1 and QQQ-1 remediation review request

**Date:** 2026-09-23
**Requested verdict:** `APPROVE` or `REJECT`
**Authorization requested:** one checkpoint for the combined Task 7.5 OOO-1 hardening,
Task 7.6 qualified-owner audit, and the reviewed PPP-1/QQQ-1 remediation.

Review the new frozen target on immutable base
`d6eee0d20d0e82135bc33caf2f8b25bdcd665772`. Do not edit, stage, or
commit it. The raw-order manifest is
`harmonize-downstream-capability-specs-task-7-5-ooo-1-and-task-7-6-ppp-1-qqq-1-remediation-dirty-manifest-2026-09-23.txt`.
The previous 13-path request and manifest remain byte-identical; their
2026-09-23 independent verdict is `REJECT`, not authorization. This request
does not authorize Task 7.7, harmonization exit, reshape Task 8.0, or Section 8.

## Original scope retained

Task 7.5 directly exercises all eight OOO-1 natural-language stale-claim forms.
Guard-source digests pin both the classifier catalog and the regression catalog;
historical replay remains 56 exact results and the current active corpus has
zero findings.

Task 7.6 retains the ordered 122-entry `ForbiddenPublicSymbols` catalog at
SHA-256 `302b74d5ac5d52ddb2586002454f1f6771c37896beb643499b854ae85645c3a4`.
Ten ephemeral management namespaces and two codec owners were corrected;
three invented identities were deleted. The reflection negative, fresh-package
probe, and deletion-ledger inventory use the same catalog. Source archives at
`ac46d99543daf85c0fa3234272997ba40f47f96b` and
`666bc1e6ec57eb055f3fecbb8f74a64ebe2e1ea9` establish historical ownership.

## Review remediation

1. **PPP-1:** `retired-public-symbols` again names `task:7.17`, its actual
   reshape removal owner. The deletion-ledger guard requires that exact task
   and its public-removal text. Harmonization Task 7.6 audits the catalog but
   does not become its unqualified machine-readable owner.
2. **QQQ-1 counts:** the Markdown companion has the exact JSON-derived counts
   for 24 families, 134 deleted paths, one compile exclusion, zero orphan
   roots, five retired package artifacts, and all four ordered symbol
   inventories (122/4/9/3). The existing companion guard now compares every
   ordered accounting row, so 999 or a missing fourth row fails.
3. **QQQ-1 member identity:** historical source is lexically masked before
   matching; non-enum members need declaration syntax inside their exact type
   body, and enum values use their own declaration form. A parameter name,
   comment, string, or sibling type cannot satisfy a member negative.
4. **QQQ-1 wording:** `.Management` existed only in removed `v3/` lineages,
   not this product lineage; the historical codec project was
   `OrcaCore.Abstractions.csproj`, whose successor assembly is `OrcaCore`.

The dated remediation artifact is
`openspec/changes/harmonize-downstream-capability-specs/artifacts/task-7-5-and-7-6-review-remediation-2026-09-23.md`.
The old request, manifest, and REJECT verdict are immutable, catalogued, and
registered as a rejected freeze. The Task 7.5 provenance entry records the
verdict as `Rejected`; no checkpoint or approval SHA is claimed.

## Load-bearing controls to reproduce

- Change the retired-symbol owner to `task:7.6` or `task:7.5`, or remove
  the named removal obligation from reshape Task 7.17: the ledger guard fails.
- Change one Markdown count to 999 or remove the fourth inventory row:
  the companion guard fails without changing JSON.
- Add `EphemeralWorkflowEngine::cancellationToken` as a forbidden member:
  historical resolution fails; the synthetic parameter/comment/string controls
  also fail if the declaration filter or lexical masking is removed.
- Restore a wrong namespace, wrong codec assembly, or invented projection
  identity: the exact historical-owner guard fails.
- Delete/narrow an OOO-1 classifier or direct regression phrase: Task 7.5 fails
  even after a coherent mutable-fixture refresh.
- Alter this target's task/design/audit/remediation decisions without updating
  the guard-owned pins: the owning guard fails.

## Validation and freeze

The target changes no `src/**`, canonical `openspec/specs/**`, package
manifest, or provider migration. Task 7.5 and Task 7.6 remain the only completed
tasks in scope; Task 7.7 stays open. Re-run Debug and Release non-incremental
`-warnaserror` builds, the focused owner/deletion/corpus guards, the
`Disposition=Infrastructure` and `Disposition=ExpectedRed` guard lanes,
fresh-package negatives and exact public API baselines, strict OpenSpec, and
`git diff --check`. Check the product/container counts against the approved
2026-09-22 packet (350/79/99/37/24/96; PostgreSQL 101, SQL Server 72,
Integration 11) or rerun them for independent confirmation.

Recompute the raw manifest and scoped content record from the final worktree,
then simulate a checkpoint using a copy of the index. Its path set must equal
the manifest exactly, including this request, the new manifest, the old verdict,
and the remediation artifact. Preserve the reviewed target unchanged while
writing any verdict in the dedicated review worktree; a verdict is a later
untracked record until the authorized checkpoint lands.
