# Independent review request — Task 8.2 typed DAG authoring source

Date: 2026-09-28. Base: `db5f3fb2b0733c06ae052536748f5a445bf52de7`.
Change: `admit-dag-authoring-friend-boundary` tasks 2.1–2.5 and reshape Task 8.2.
Manifest: `developer-facing-interface-section-08-task-8-2-dag-authoring-source-dirty-manifest-2026-09-28.txt`.
Raw-order manifest: 33 paths, 2,431 bytes, SHA-256
`aac89bfc5a83feddbce2105fe8243cfa1c5c2cd5099b400e4e2058caf05ed135`;
byte-identical to `git status --porcelain=v1 --untracked-files=all` with LF records.

The prerequisite chain is checkpoint `0a99461096cff6f5fc4b8906b735c9f33cb0666f`,
direct-child evidence `0837d7c63c6467c6af3f85ac6007a273e522639b`, and
activation `db5f3fb2b0733c06ae052536748f5a445bf52de7`. The independently approved
canonical/registry-transition verdict is
`docs/review/developer-facing-interface-section-08-task-8-2-dag-friend-atomic-canonical-transition-independent-review-verdict-2026-09-27.md`,
10,446 bytes, SHA-256 `950a77f78d288577f7a7178127150dc54937eaf6fc3a35b8d2451959c6d8ed80`.
This request seeks source approval only; no Task 8.2 source checkpoint has been made.

## Scope and claims to examine

The approved authoring-only `OrcaCore -> OrcaCore.Dag` friend is compiled, without any new
runtime friend or package edge. `DefinitionFingerprint.ComputeCanonicalHash` is the single
internal UTF-8/SHA-256 operation for both existing Core fingerprints and typed DAG plans;
Core's pre-existing canonical input and fixed digest remain unchanged. The seven approved
`DAG-AUTH-*` descriptors live in the existing diagnostic catalog. `Dag.Define`, typed node
builders, `TryBuild`/`Build`, node references and plan snapshots implement authoring only:
authored ordinals, copied dependencies, ordered diagnostics, structural fingerprints and
opaque mapping delegates. Nothing starts a child, executes a mapper, or grants runtime access.

The new metadata guard decodes `OrcaCore.Dag.dll` member-reference signatures and allows
exactly six internal `OrcaCore` references: five compiler-created constructors and the shared
hash method. Its independent mutation probes made an unlisted internal type/member, another
method, and an overload red; a public reference stayed green. The sole DAG product dependency
is still `OrcaCore`. Review this classifier against compiled metadata, including whether an
internal reference could evade it without a `MemberReference` row.

The exact DAG public API baseline, three affected package-source-provenance hashes, Section 7R
declaration crosswalk, and historical current-match paths are refreshed. The package consumer
now compiles the new `Dag.Define` authoring API; its `dag-hosting` fixture remains the one
expected red for later runtime registry and snapshot symbols. The active friend-graph docs,
Task 7.3 rows 1/6/7/12 and source-pinned artifact digest, and Task 8.0 map pin move with the
compiled friend in this same target. A new source-pinned validation artifact records the lanes.
Tasks 2.1–2.5 remain unchecked pending this review; Task 8.3 and the registry's final
`Complete` transition are not in scope.

The 29 semantic paths excluding this request, its manifest, and both self-referential
review fixtures (`immutable-document-history.json` and `review-manifest-provenance.json`)
have a reproducible content record: sort repository-relative paths ordinally; for each,
render `path<TAB>raw-byte-count<TAB>lowercase-SHA-256`; join with LF and one final LF as
UTF-8 without BOM. Result: **29 rows, 3,986 bytes, SHA-256
`e0f8ff4ad3fcd099e944953336319d3333dd2851cb055d89f1dc883bf1da2fa8`**.
The all-file active content anchor excludes only the self-referential
`review-manifest-provenance.json` fixture and is recorded there. Recompute it independently
after the packet is frozen; do not confuse a dirty-worktree digest with a committed-object hash.

## Validation to reproduce

- Debug and Release `dotnet build OrcaCore.slnx -warnaserror --no-incremental --no-restore`:
  zero warnings and errors.
- Core 350, Ephemeral 79, Durable 99, Acceptance 37, Hosting 24, ProviderCertification 96;
  PostgreSQL 101, SQL Server 72, Integration 11.
- `Disposition=Infrastructure`: 231 passed, 0 failed. The focused DAG authoring/metadata and
  documentation-pins lane passes 6/6.
- Package-consumer green lane: 8 passed. The single `dag-hosting` compile fixture remains
  `ExpectedRed` on two runtime symbols. The separate guard `Disposition=ExpectedRed` lane
  has exactly 14 intentional failures and no passes.
- `openspec validate --all --strict`: 19 passed. `git diff --check`: clean.

## Independent checks requested

1. Reproduce base, raw-order manifest, semantic and all-file content records, staged tree,
   and committed-diff whitespace check. Check every listed path and that no unrelated
   source or document is hidden in the target.
2. Compare public API and `OrcaCore.Dag` package edge against the approved §17.2.6 contract.
   Confirm the exact eight-friend product graph and sole Durable Hosting-to-DAG Hosting
   runtime bridge. Reject any public constructor/factory, `Core -> Dag` edge or extra internal
   OrcaCore member access.
3. Recompute the six decoded metadata signatures from the compiled DAG assembly. Mutation-test
   a forbidden member/type/overload and a permitted public call, independently of source-text
   claims. Check the one-way package graph.
4. Exercise `TryBuild`/`Build` parity and all seven DAG diagnostics, first-authored ordering,
   duplicate/foreign/self/cyclic dependencies, defensive copies, and mapper non-execution.
   Verify fingerprints change only with approved structural inputs and existing Core digests
   remain byte-identical. Probe whether an opaque mapper can smuggle runtime behavior into
   authoring. Keep DAG hosting and child-start outside this slice.
5. Recompute the source-provenance fixture, exact DAG API baseline, crosswalk counts, maximal
   historical current matches, four refreshed Task 7.3 rows/digest, Task 8.0 map pin, and
   validation-artifact pin. Sweep active documents for stale “friend not compiled” wording.
6. Run both warning-as-error builds, product/provider/container lanes, 231 Infrastructure
   guards, 14 intentional expected-red guards separately, package-consumer green/expected-red
   lanes, strict OpenSpec, and staged `git diff --check`.

If approved, checkpoint only these frozen bytes. Add the verdict byte-exact in a direct-child
evidence commit, then activate review state without folding in Task 8.3 or unreviewed source.
