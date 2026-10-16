# Independent review request — DAG friend contract YYY-1 remediation

Date: 2026-09-27
Base: `5adddc3ba0ca7e0f70ee1f3b7317e69c1df7e76a`
Manifest: `developer-facing-interface-section-08-task-8-2-dag-friend-contract-yyy-1-remediation-dirty-manifest-2026-09-27.txt`
Prior verdict: `developer-facing-interface-section-08-task-8-2-dag-friend-contract-independent-review-verdict-2026-09-27.md`, 9,160 bytes, SHA-256 `55762351d1e516dabb8d0863fb51ebb7c36795fdcb731cff0f079e391e1da1f3` — `REJECT` on YYY-1.

Review the same narrow `OrcaCore -> OrcaCore.Dag` *authoring-only* friend contract, now with its
complete document disposition. The five existing internal constructors, one future shared hash
operation, one-way package graph, compiled-member allowlist, and sole DAG-to-durable runtime
bridge are unchanged. Both full `MODIFIED` successor blocks are byte-identical to the previous
freeze and remain semantic inputs even though they are committed at the base.

## YYY-1 remediation to inspect

| Active document | Disposition |
| --- | --- |
| `docs/specs/03-domain-model-and-glossary.md` | Replace public-only DAG dependency wording with sole `OrcaCore` package reference, public workflow references, and the explicitly proposed internal authoring friend. |
| `docs/specs/08-requirements-composition.md` CP-020 | Make the same distinction, preserving the separate DAG package and the permitted outward `OrcaCore.Dag.Hosting -> OrcaCore.Dag` dependency. |
| `docs/specs/10-provider-model-and-extensibility.md` PR-005 | Make the same distinction without creating a reverse package edge or weakening companion isolation. |
| `docs/specs/11-non-functional-requirements.md` | Explicitly excluded in design: NF-002 governs package dependency closure only; it does not prohibit an internal friend and remains true unchanged. |
| `docs/implementation/00-stack-decisions.md` Decision 22 | Enumerate the seven existing product friends exactly and describe the proposed eighth as not yet compiled or approved. |
| `docs/implementation/01-solution-architecture.md` | Correct the public-only sentence and both exact-friend statements; retain the sole DAG-to-durable runtime bridge. |
| `docs/project-technical-overview.md` | Correct the exact seven-friend list and distinguish proposed authoring access from runtime child-start access. |

The OpenSpec proposal, design, and task 1.1 now name this full disposition. Task 7.3's
documentation artifact refreshes *five* rows: 6, 7, 12, 14, and 18. The source-level artifact
digest is refreshed in the same target. Document 08 is outside the 22-source Task 7.3 list;
document 11 is unchanged. The prior `CLAUDE.md`, document 17, and Task 8.0 map edits and their
pins are unchanged from the rejected target.

The old request and manifest remain byte-exact. The REJECT verdict is cataloged as immutable
append-only review evidence, alongside this new request and manifest. Nothing under `src/**`
or canonical `openspec/specs/**` changes. The compiled friend set still has seven product edges;
task 1.2 stays open. Tasks 1.3 and 1.4 still require one atomic, separately reviewed
canonical-sync/approved-pending-registry transition after contract approval.

## Verification requested

Recompute the raw Git-order manifest, all-file content record, base and simulated staged tree.
Inspect every changed paragraph and compare both successor blocks against the previous freeze.
Recompute the five LF-normalized Task 7.3 row hashes and the guard-source artifact digest.
Verify the active corpus has no remaining public-only DAG claim or incomplete exact friend
enumeration outside immutable review/archive history. Check doc 11's exclusion against NF-002.
Run non-incremental warning-as-error builds, `Disposition=Infrastructure`, the 14 intentional
expected-red scenarios separately, strict OpenSpec validation, and `git diff --check` including
the staged simulation. Probe the documentation guard by reverting one refreshed row or
artifact digest; it must fail.

If approved, checkpoint only this frozen target, then add the new APPROVE verdict byte-exact
in a separate evidence commit and activate only the review state. Do not add the friend attribute,
sync canonical specs, or implement Task 8.2 product source in this checkpoint.
