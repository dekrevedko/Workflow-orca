# Task 6.6 post-review remediation — 2026-09-13

## Scope

This dated record closes independent-review findings BB-1, CC-1, and DD-1 without changing product
runtime semantics or the approved future-capability registry membership.

## BB-1 — superseded provenance artifacts remain permanently pinned

The canonical-provenance fixture points to the current Task 6.6 refresh artifact. Repointing that
single current-artifact field no longer removes protection from its predecessor. Guard source now
owns a permanent catalog of superseded provenance artifact paths and normalized SHA-256 values,
pins the catalog count and aggregate digest, requires every catalogued file to exist, and re-hashes
each file independently. The first catalog entry is:

| Artifact | LF-normalized SHA-256 |
|---|---|
| `openspec/changes/harmonize-downstream-capability-specs/artifacts/task-4-2-openspec-provenance-record-2026-08-18.md` | `9e8709096f8f3efcb8ea1ee040d1ec6f13e997a320eb6fbb7f724ec708ae2958` |

The one-entry catalog record is 180 bytes and hashes to
`a9836be8bb9f05876cf73f96c77756143a5b079bb1111db446721922869f7b12`. Future provenance refreshes
must move the previous current artifact into this permanent guard-source catalog before repointing
the fixture.

## CC-1 — removed-concept checks are spelling-independent

The deferred-versus-removed check now searches identifier tokens `WaitLong` and `Yield` with Unicode
letter, decimal-digit, and underscore boundaries. Markdown quoting or nearby prose such as
``Authored `Yield` `` cannot evade the deferred-table rejection, while longer unrelated identifiers
do not create false positives. Both tokens must remain absent from the deferred subsection and
present in the removed-concepts subsection.

## DD-1 — approval and exact synchronization disclosure

Task 6.6 authority entered the ledger in commit
`ad9414088f1843dae09ef8a5d10caa8aca413561` on 2026-08-05, before the canonical synchronization gate
closed. The citation-only implementation and canonical synchronization were nevertheless prepared
and frozen before the exact target obtained independent approval. The independent Task 6.6 verdict
approved that 22-path target; checkpoint `1d4dec01f3884ded0cb9ba0f14f40f2e67f6773a`, approval-evidence
commit `dd5fd6bb49470501cc67211aa6e4670893c151ee`, and activation commit
`665925d` preserve the actual order rather than presenting it as approval-first execution.

The exact synchronized requirement changes were:

| Capability / requirement | Exact citation-only change |
|---|---|
| `developer-facing-surface` / `Deferred capabilities are documented without public placeholders` | Appended `at docs/specs/13-phasing-and-open-questions.md §13.4 ("Future-capability registry")` to the future-registry reference. |
| `saga-orchestration` / `Saga remains an explicit deferred capability` | Appended the same exact path and section identity to the future-registry reference. |

Each change was applied byte-identically to canonical OpenSpec and its active reshape-owned delta.
No scenario, behavioral obligation, capability membership, or product surface changed. This record
is the repository-owned approval/synchronization disclosure; the immutable independent verdict
remains the authority for the exact reviewed bytes.

