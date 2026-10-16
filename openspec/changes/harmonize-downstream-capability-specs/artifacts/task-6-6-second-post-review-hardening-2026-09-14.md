# Task 6.6 second post-review hardening — 2026-09-14

## Scope

This dated addendum closes independent-review observations EE-1, FF-1, and GG-1 without editing the
approved Task 6.6 remediation artifact or changing the future-capability registry membership.

## EE-1 — exact citation-only synchronization bytes

The prior remediation artifact accurately named both requirement owners and their semantic change,
but its Markdown table rendered each description as one code span and therefore omitted the literal
backticks surrounding the referenced path. The exact inserted fragments, including the inner
backticks, are:

```text
developer-facing-surface / Deferred capabilities are documented without public placeholders
 at `docs/specs/13-phasing-and-open-questions.md` §13.4 ("Future-capability registry")

saga-orchestration / Saga remains an explicit deferred capability
 at `docs/specs/13-phasing-and-open-questions.md` §13.4 ("Future-capability registry")
```

Each fragment was added byte-identically to the canonical requirement and its active reshape delta.
This addendum corrects only the earlier record's byte-exact presentation; it does not revise the
approved requirements, scenarios, registry classification, or synchronization history.

## FF-1 — provenance refresh discovery is exhaustive

The canonical-provenance guard now enumerates every top-level
`artifacts/*openspec-provenance-*.md` file. The discovered set must equal the current artifact named
by `openspec-provenance-checkpoint.json` plus the permanent guard-source superseded-artifact catalog.
A refresh that points the fixture at a successor without first cataloguing its predecessor therefore
fails even after routine current-match pins are refreshed.

## GG-1 — removed concepts are absent from all future-work prose

Identifier-boundary checks for `WaitLong` and `Yield` now scan the complete §13.4 region before the
removed-concepts subsection, including the registry preamble and deferred-capability table. The
removed subsection must still contain both tokens. Markdown spelling cannot reclassify either
removed concept as planned future work, while longer identifiers such as `WaitLongAsync` and
`YieldPolicy` remain outside the token match.

## Disposition

The previous remediation artifact remains immutable and guard-pinned. This addendum is separately
guard-pinned, the Task 6.6 ledger and design name all three closures, and the implementation remains
documentation/infrastructure-only with no `src/**` change.
