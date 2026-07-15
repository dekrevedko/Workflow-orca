# Developer-Facing Interface Post-Fiber Review Remediation

Date: 2026-07-14

Reviewed change: `reshape-developer-facing-interfaces`

Related change: `add-runtime-concurrency-limits`

Review: [developer-facing-interface-post-fiber-openspec-review-2026-07-14.md](developer-facing-interface-post-fiber-openspec-review-2026-07-14.md)

## Outcome

All **1 P1, 5 P2, and 5 P3** review findings were addressed in the documentation and task
graph. The changes remain documentation-only; no application source, test implementation, or
sample implementation was modified.

This is a greenfield API with no API clients or external compatibility contract. Existing
repository paths are provisional implementation choices. The target API is implemented
directly, and every inferior parallel path is deleted without aliases or shims.

After remediation:

- `reshape-developer-facing-interfaces`: **15 of 111 tasks complete**;
- `add-runtime-concurrency-limits`: **7 of 15 tasks complete**;
- both changes pass strict OpenSpec validation;
- every MODIFIED requirement heading matches its canonical baseline verbatim;
- the remaining tasks describe source implementation rather than compatibility work.

## Finding disposition

| Finding | Disposition |
|---|---|
| R-01 - workflow-contracts MODIFIED heading mismatch | Restored the exact canonical heading `Host-facing execution hints remain optional and declarative`; the new mode-guaranteed body remains. |
| R-02 - nested/root capability contradiction | Changed the fixture to absence preservation and enumerated the exact initial nested capability set for both modes in matrix 17.2. |
| R-03 - stale promoted fiber gate | Added a MODIFIED replacement for `Developer-surface reconciliation gates implementation` and removed the duplicate added gate. |
| R-04 - concurrency requirement classification | Moved the two new governance requirements under ADDED, removed the redundant state-driven delta/capability listing, and retained the already-promoted composition rule as accepted baseline. |
| R-05 - canonical amendments deferred | Added an explicit section 4-8 canonical-amendment map and prerequisite tasks `4.0` through `8.0`; narrative documentation no longer owns canonical updates. |
| R-06 - package fixtures before packages | Section 3 now defines fixture harnesses/assertions only; tasks 6.2 and 6.3 instantiate them after package creation. |
| R-07 - shared saga definition | Added distinct `EphemeralSagaDefinition<TState>` and `DurableSagaDefinition<TState>` contracts, signatures, requirements, and tasks over one internal representation. |
| R-08 - authoring options underspecified | Enumerated `MaxStructuredDepth`, `MaxActiveExecutionPaths`, `MaxBranchResultPayloadBytes`, serializer registry, state copier registry, and fingerprint contributors; turn/checkpoint limits moved to engine/hosting vocabulary. |
| R-09 - wait path and `WaitId` unspecified | Defined stable `AuthoredLocation` shared with compiler diagnostics and retained opaque application-visible `WaitId` without runtime ownership semantics. |
| R-10 - stale count and unexplained evidence edit | De-precised the public-type count and added an editorial note explaining the earlier eleven-to-nine correction. |
| R-11 - ephemeral state detachment unspecified | Required the ephemeral Adapter to use the registered serializer/deep-copy contract and return typed incompatibility instead of a live state reference. |

## Task-graph result

The source sequence is now:

1. current-source public/behavior guards;
2. mode-first definitions, nested builders, authoring options, and deletion of superseded
   parallel paths;
3. structural external-job and durable-lease nodes, followed by portable-result cleanup;
4. package/tier creation and real package-consumer/provider-author fixtures;
5. facade and management completion;
6. DAG, saga, hosting, and provider closure;
7. journeys, verification, and absence scans.

Each source section applies its mapped canonical requirements first. Package-dependent fixtures
are no longer ordered before package creation.

## Validation

- `openspec validate reshape-developer-facing-interfaces --strict`: valid.
- `openspec validate add-runtime-concurrency-limits --strict`: valid.
- Scripted MODIFIED-heading comparison against `openspec/specs/`: all headings match.
- Markdown relative-link validation: passed for the review/change artifact set.
- `git diff --check`: clean apart from informational line-ending warnings.

The first source-safe slice remains task 3.1 together with tasks 3.4-3.11. Tasks 3.2 and 3.3
may define their harnesses, but their package-backed projects are created only by tasks 6.2 and
6.3.
