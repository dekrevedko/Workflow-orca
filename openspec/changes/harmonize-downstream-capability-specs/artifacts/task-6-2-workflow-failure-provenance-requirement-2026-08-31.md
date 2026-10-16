# Task 6.2 WorkflowFailure provenance requirement

Date: 2026-08-31

## Result

Task 6.2 adds `CR-014a Workflow failures retain authored and runtime occurrence provenance` to
`docs/specs/04-requirements-core-runtime.md`. The numbered requirement mirrors the synchronized
canonical and reshape `quality-and-verification` requirement
`Authoring lifecycle, fingerprint coverage, and failure provenance are executable`; it does not
introduce a new runtime or public-surface contract.

## Required provenance

| Contract dimension | Numbered requirement | Existing executable evidence |
|---|---|---|
| Authored identity | exactly one immutable `AuthoredLocation` | Core detached-graph tests plus active ephemeral and durable branch tests |
| Runtime occurrence | runtime-only closed `Root`, `Branch(AuthoredBranchId)`, `Item(index)` union | Core union test and branch-runtime assertions |
| Attachment | provenance is attached when the failure is created, never inferred or replaced | active engine branch failures retain the exact authored node and branch ID |
| Propagation | one failure is unchanged; aggregate ownership is new without collapsing causes | Core aggregate test |
| Ordering | fixed branches use authored order; dynamic items use item-index order | Core aggregate and active runtime outcome assertions |
| Fixed codec | versioned `root`/`branch`/`item` discriminator allowlist | Core round-trip and malformed/unknown rejection tests |
| Durable persistence | runtime output and checkpoint state commit together | compile-included durable runtime regression reads `DurableExecutionEnvelopeV2` |

The requirement is bound to executable tests through `Requirement=CR-014a`: eight Core tests,
one active ephemeral runtime test, and one active durable runtime test. CI runs all three projects
under that exact filter.

## Acceptance-criterion correction

`FailureProvenanceTests` previously carried `AC-022`, but canonical `AC-022` owns structural
fingerprint and opaque-code versioning honesty. Task 6.2 removes that false association and relocates
`AC-022` to:

- `Fingerprint_IsDeterministicAndChangesWithStructureAndOutcome`; and
- `Fingerprint_IgnoresCapturedOpaqueSelectorConfiguration`.

Task 6.3 still owns the new acceptance criteria for `CR-009a` and `CR-014a`.

## Executable guard

`Task62_CoreRuntimeDocumentsWorkflowFailureProvenanceAndExecutableEvidence` requires:

- one exact `CR-014a` numbered heading and every normative clause;
- byte-equivalent canonical and reshape requirement blocks;
- the closed union, detached graph, ordered aggregate, codec round-trip, and malformed-input tests;
- truthful `AC-022` ownership outside failure provenance;
- active ephemeral and durable runtime evidence with no matching `Compile Remove`; and
- one exact CI step running the Core, Ephemeral, and Durable requirement filters.

Adding a compile-removal entry for the durable regression makes the focused guard fail. The
project file was restored byte-for-byte after that mutation.

## Self-review correction

The first draft pointed at legacy source-only engine tests and attempted to reuse inaccessible
internal Core execution types from a new durable test. Build and filter discovery rejected that
shape. The final test instead uses the current public `Parallel`/`WhenAllOutcomes` authoring API,
the active durable runtime, its provider ports, and the persisted public envelope contract.

The broad Core lane also detected the uncovered `AC-022` catalog entry, leading to the truthful
relocation above before the freeze.

## Scope boundaries

- No `src/**` file changes.
- No public API or package-baseline change.
- No canonical OpenSpec capability change.
- Task 6.3 still owns acceptance-criterion creation and bidirectional mapping.
- Task 6.4 and later harmonization work remain open.
