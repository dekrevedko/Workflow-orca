# Task 4.2 OpenSpec change-to-canonical provenance record

Date: 2026-08-18

This artifact records the reproducible input relationship enforced by the checkpoint guard.
Structural OpenSpec validation remains distinct from semantic approval; after tasks 5.1 and 5.2
consumed every pending canonical operation, the strict provenance path is now eligible for semantic
approval.

## Record recipe

The guard enumerates every requirement in every non-archived
`openspec/changes/*/specs/*/spec.md` and compares it with the requirement of the same exact heading
in `openspec/specs/<capability>/spec.md`.

Each requirement block is read with line endings removed, trailing empty lines discarded, and the
remaining lines joined with LF. Both canonical and delta blocks terminate at the next requirement
heading or `##` section heading. Delta and canonical block hashes are lowercase SHA-256 over those
UTF-8 bytes. Each record is rendered as:

```text
<change>\t<capability>\t<capability-kind>\t<operation>\t<requirement>\t<state>\t<delta-block-sha256>\t<canonical-block-sha256-or-dash>
```

The complete rows are sorted by ordinal comparison of the rendered record, joined with LF plus one
final LF, and hashed as UTF-8 without a BOM.

- record rows: 176
- record bytes: 46,211
- record SHA-256: `4815ffd5c3e5e1289d5196c103a6cb8e801e5e442522643d5794637902df69d4`
- synchronized operations: 173
- pending canonical operations: 0
- declared new-capability requirements outside the canonical set: 3
- duplicate `(capability, requirement)` owners: 0
- semantic approval eligible: yes

State composition:

| State | Count |
|---|---:|
| `Synchronized` | 173 |
| `PendingAddition` | 0 |
| `PendingAddedCanonicalConflict` | 0 |
| `PendingModification` | 0 |
| `PendingRemoval` | 0 |
| `NewCapabilityOutsideCanonical` | 3 |

The three `NewCapabilityOutsideCanonical` rows are the requirements of the declared-new
`spec-driven-planning` bootstrap capability. They are recorded distinctly with the bootstrap
proposal as evidence and a planning-only disposition, so they do not conceal an omitted canonical
product capability.

The inverse case is also explicit: `reshape-developer-facing-interfaces` still declares
`developer-facing-surface` as `New` although an earlier approved synchronization already created
its canonical spec. All 18 delta requirements are recorded, the reshape proposal is cited as
evidence, and the permanent `reshape-section-7b-developer-facing-surface` record routes the required
post-gate amendment stages without treating the original synchronization as coverage. A
declared-new capability may therefore exist on either side of the canonical boundary, but neither
case is silently accepted.

Canonical completeness is independent of active-delta existence. The guard records every canonical
`openspec/specs/*/` directory even when no current change amends it. Normal archival may therefore
remove active provenance rows and require this reviewed fixture/artifact pair to be refrozen, but it
cannot become permanently invalid merely because a canonical capability no longer has an active
delta.

## Canonical-capability inventory

Every repository-relative `openspec/specs/*/` capability directory uses `/`, retains one trailing
`/`, is sorted by ordinal comparison, and is joined with LF plus one final LF before UTF-8 SHA-256.

- canonical capability directories: 14
- record bytes: 547
- SHA-256: `7165dac4e1a57022a7890b421f522bf4152f6d5ddc159a41539ce2ef0d18ec9f`
- directories lacking `spec.md`: 0

## Canonical-preamble inventory

For every canonical capability, the guard LF-normalizes `spec.md`, takes every byte before the
first line-start `### Requirement:` heading, and hashes that complete preamble. This includes
`## Purpose`, intervening headings, and blank-line structure. Records render as
`<capability>\t<normalized-preamble-sha256>`, sort by capability using ordinal comparison, and join
with LF plus one final LF before UTF-8 SHA-256.

- canonical preambles: 14
- record bytes: 1,233
- SHA-256: `595528c6a7ba56dd5648e7fc12ac6bc2af9e9bf6fa3fbf853b86fffdd7cecd3c`

The machine-readable fixture stores the 14 individual capability hashes so a changed `## Purpose`
reports the exact capability rather than only an aggregate-record mismatch. An approved preamble
revision therefore requires a reviewed fixture refreeze; an unrecorded edit cannot remain green
merely because all requirement blocks are unchanged.

## Capability-directory inventory

The checkpoint guard also enumerates every active
`openspec/changes/*/specs/*/` capability directory. Each repository-relative path uses `/`, retains
one trailing `/`, is sorted by ordinal comparison, and is joined with LF plus one final LF before
UTF-8 SHA-256. This is the exact Decision 6 capability-inventory recipe.

- capability directories: 16
- record bytes: 1,321
- SHA-256: `5e8a9725979d702ff2f639fef587828958c92533139602ab13f1401d6df7ebd5`
- directories lacking `spec.md`: 0

## Pending operations and owners

| Canonical capability | Pending | Turns green |
|---|---:|---|
| **Total** | **0** | **tasks 5.1 and 5.2 complete** |

Task 5.1 consumed the prior 42 vocabulary-bearing operations across
`developer-facing-surface`, `durable-persistence-and-outbox`, `durable-runtime`,
`event-routing-and-waits`, `state-driven-runtime`, `workflow-authoring`, and
`workflow-contracts`. Task 5.2 consumed the remaining eight non-vocabulary operations across
`management-and-querying`, `quality-and-verification`, and `repository-foundation`. All 50
canonical requirement blocks now match the authoritative reshape deltas exactly.

The machine-readable companion is
`tests/OrcaCore.DeveloperSurface.Guards/Fixtures/openspec-provenance-checkpoint.json`. The guard
requires each named turns-green task to exist exactly once and remain open while its operations are
pending. Each task block must also name every assigned capability and its exact pending count, so a
syntactically open task cannot claim unrelated work. A change to an operation, block, canonical
counterpart, classification, or owner changes the record or pending table and fails the
infrastructure lane even though `openspec validate --all --strict` may remain green.

## Removal provenance

All `REMOVED` blocks must contain non-empty `**Reason**` and `**Migration**` lines. When the current
canonical heading is already absent because synchronization applied the removal, the fixture embeds
the exact normalized historical canonical requirement block and pins its SHA-256. The permanent
eleven-entry catalog retains source-commit metadata even after an owning change is archived, while the
currently active synchronized-removal set must be a subset of that catalog rather than equal to it.
The source commit is `ba2478e995023b0712c44705174c2b0e3262f213` with an exact canonical source
path. When that commit is available, the guard reproduces every embedded block from `git show`
without requiring it to be an ancestor of `HEAD`; a non-ancestor object after squash/rebase remains
useful corroboration.
Independently, a guard-source constant pins the complete eleven-entry source/path/identity/block-hash
catalog as `81c06519ae95846b697df5e895e6bcbc9792c3e36afbe441529b3add15008ebd`, so a fixture-local
block/hash rewrite is not self-attesting when integration history is squashed or rebased.
The eleventh entry preserves the authored-`Yield` canonical block removed by task 5.1. No pending
canonical removals remain.

## Checkpoint process correction

The independently approved task 4.2 target was frozen against base
`5fd4c6d1c5ec76beb3027c59189d6404595840fc` with seven worktree entries. Before checkpoint commit
`12de180cbf04c74234dd8160fbc77555542b97fa`, two reviewer observations were implemented without a
new freeze/review pass: the `Disposition=Infrastructure` CI lane was added, and historical removal
validation was converted from mandatory Git-history lookup to embedded squash-safe evidence. Those
post-approval edits changed all seven checkpoint paths and were therefore a process deviation even
though their content was subsequently verified sound. On 2026-08-19 the repository owner explicitly
approved commit `12de180cbf04c74234dd8160fbc77555542b97fa`; this dated disclosure preserves the provenance
chain instead of presenting the originally approved dirty target and the committed tree as
identical.

## CI execution

The primary CI job executes the complete must-be-green guard disposition with the exact filter
`Disposition=Infrastructure`. The 14 intentional expected-red behavior guards remain outside that
lane, so adding the guard project to CI does not normalize or ignore them.

## Artifact integrity

The machine-readable fixture pins the lowercase SHA-256 of this entire Markdown artifact after CRLF
and lone CR are normalized to LF. That makes every human-readable count, recipe, table, disposition,
and provenance statement part of the executable checkpoint rather than an unchecked narrative.
