# Task 8.2 authoring-source validation candidate

Date: 2026-09-28. Owner: `admit-dag-authoring-friend-boundary` tasks 2.1–2.5 and reshape Task 8.2. This is executable candidate evidence, not an independent approval or checkpoint.

## Product boundary

- `OrcaCore` adds exactly `InternalsVisibleTo("OrcaCore.Dag")`; `OrcaCore.Dag` still references only `OrcaCore`. `OrcaCore.Durable.Hosting -> OrcaCore.Dag.Hosting` remains the sole DAG-to-durable runtime bridge.
- The compiled-metadata guard rejects every non-public `OrcaCore` type reference, including type-only references in signatures, base classes, and interface implementations. Four permanent, independently compiled friend-assembly probes cover `typeof`, a field signature, an implemented internal interface, and an `is` check. It separately decodes member references to canonical signatures. Its six allowed internal members are the constructors of `AuthoredLocation`, `DefinitionFingerprint`, `Validation<T>`, `WorkflowDefinitionException`, and `WorkflowDiagnostic`, plus `DefinitionFingerprint.ComputeCanonicalHash(String):String`. An unlisted internal method or overload also turns the focused guard red; a public-only reference remains green.
- Core retains its original `orcacore-json-v1|` canonical input and calls the new shared UTF-8/SHA-256 operation. Existing fixed workflow digests remain unchanged. The DAG structural fingerprint incorporates authored ordinals, node IDs, child definition IDs, versions, fingerprints, and dependency ordinals; it does not hash mapper delegates or captured state.

## Authoring boundary

`Dag.Define`, typed node builders, and `TryBuild`/`Build` implement only authoring and validation. Validation accumulates the seven approved `DAG-AUTH-*` codes with the shared diagnostic ordering; `Build` throws the same ordered diagnostics. Each code has an isolated exact-single-code regression. Self-dependency reports only `DAG-AUTH-DEPENDENCY-002`, not a duplicate cycle diagnostic. Cycle detection uses an explicit frame stack, and valid 100,000-node chains pass in both dependency directions without call-stack growth. Node snapshots copy dependency arrays. Plan ownership uses an opaque per-builder token, not a persisted or externally forgeable ID. Input-mapping delegates are stored, never executed during authoring. Runtime child-start, mapping execution, typed registration, and DAG hosting remain later reshape tasks.

The approved DAG structural fingerprint does not include the plan's own `TRunInput` type; it includes each child definition's identity, version, and fingerprint. Task 8.6 registration must decide whether to key registration conflicts by the plan fingerprint plus its run-input type, rather than silently treating distinct generic plans as equivalent. `OutputOf`'s treatment of a successful null output belongs to Task 8.3; this authoring slice makes no runtime-null claim.

## Evidence and intentional reds

| Lane | Candidate result |
|---|---:|
| Debug and Release solution builds, `-warnaserror --no-incremental --no-restore` | 0 warnings, 0 errors |
| Core / Ephemeral / Durable / Acceptance / Hosting / ProviderCertification | 350 / 79 / 99 / 37 / 24 / 96 passed |
| PostgreSQL / SQL Server / Integration | 101 / 72 / 11 passed |
| `Disposition=Infrastructure` | 240 passed, 0 failed |
| Green package consumers | 8 passed |
| Package consumer `ExpectedRed` | 1 `dag-hosting` failure, on the still-absent runtime registry/snapshot symbols |
| Guard `Disposition=ExpectedRed` | 14 intentional failures, 0 passed |
| OpenSpec `validate --all --strict` | 19 passed |

The public API baseline changes only for `OrcaCore.Dag`. Package source-provenance changes only for `OrcaCore`, `OrcaCore.Core`, and `OrcaCore.Dag`. The Section 7R crosswalk adds two active guard source files with five `[Fact]` and two `[Theory]` declarations: 339 physical files / 1,402 physical declarations, 191 active files / 714 active declarations, and 866 retired coordinates unchanged. The DAG-hosting compile fixture now expects only its remaining runtime symbols to be absent; it no longer treats implemented authoring symbols as missing. Active friend-graph documentation, six Task 7.3 rows and digest, and the Task 8.0 map pin move in this same candidate. The rejected first Task 8.2 source freeze remains immutable and cataloged separately. All implementation tasks remain open until independent approval.
