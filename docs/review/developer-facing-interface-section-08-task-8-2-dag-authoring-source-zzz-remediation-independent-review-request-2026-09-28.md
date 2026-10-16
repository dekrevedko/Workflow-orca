# Independent re-review request — Task 8.2 DAG authoring source, ZZZ remediation

Date: 2026-09-28. Base: `db5f3fb2b0733c06ae052536748f5a445bf52de7`.
Change: `admit-dag-authoring-friend-boundary` tasks 2.1–2.5 and reshape Task 8.2.
The first source freeze and its 2026-09-28 REJECT verdict are immutable inputs to this target;
no Task 8.2 source checkpoint or product approval has occurred. This request seeks approval of
the complete new dirty target, not merely the three repaired files.

Manifest: `developer-facing-interface-section-08-task-8-2-dag-authoring-source-zzz-remediation-dirty-manifest-2026-09-28.txt`.
Raw-order manifest: **39** paths, **2,968** bytes, SHA-256
`c9462505515394deb34865b8fd9e5c4dd94c6427733498c89cae586f98ec020a`; it must be byte-identical to the commit-real
`git status --porcelain=v1 --untracked-files=all` output with LF records.

The semantic content record excludes all `docs/review/` paths and the two self-referential
review fixtures, `immutable-document-history.json` and `review-manifest-provenance.json`.
For each remaining dirty path, render `path<TAB>raw-byte-count<TAB>lowercase-SHA-256`, sort
paths ordinally, join with LF and one final LF, then hash the UTF-8 bytes without BOM.
Result: **32** rows, **4,334** bytes, SHA-256
`ff924cd53d63e24f807aadc79d02b3b119d8b2035bc314cd5cee72bc2c5eb110`.
The all-file active record excludes only `review-manifest-provenance.json` and is pinned in
that fixture. Neither dirty-worktree digest is a committed-object hash.

## Blocking findings and repairs

- **ZZZ-1 — non-public type references:** the compiled DAG metadata guard now rejects every
  non-public `OrcaCore` TypeReference, including type-only references in signatures, base
  types and implemented interfaces. It separately retains the six exact internal member
  signatures. Four permanent probes compile as `OrcaCore.Dag` through the real friend edge:
  `typeof(WorkflowDiagnosticCatalog)`, a `WorkflowDiagnosticDescriptor` field, an
  `IWorkflowDefinitionRuntimeMetadata` implementation, and an `is` check on that interface.
  Each must fail specifically on its forbidden type, even when its member-reference set is
  treated as approved. Removing the type check must make the focused guard red.
- **ZZZ-2 — unbounded recursive cycle walk:** cycle detection now uses explicit DFS frames,
  not call-stack recursion. Two `[Theory]` cases construct valid 100,000-node chains with
  dependencies wired forward and backward; `TryBuild` must return a valid plan in both.
  The original 20,000-node forward case killed the test process with stack overflow before
  this repair. An isolated seven-case diagnostic theory asserts each `DAG-AUTH-*` code as
  its exact single result. Self-dependency now reports only `DEPENDENCY-002`, not a second
  cycle diagnostic.
- **ZZZ-3 — stale friend status:** docs 03, 08 CP-020, 10 PR-005, and 17 §17.2.6 now state
  that the authoring-only grant is compiled in the Task 8.2 candidate but awaits independent
  source approval. No text claims a completed checkpoint. Task 7.3 rows 14 and 18, its
  source-pinned artifact digest, and the rejected verdict registry are updated in this target.

The previously reviewed public DAG API, sole `Dag -> OrcaCore` product edge, shared hash
operation, six allowed internal members, and no runtime/child-start friend remain unchanged.
The DAG source hash is refreshed from the repository capture recipe. The Section 7R
crosswalk moves only for two new `[Theory]` declarations. The original review request and
verdict are not rewritten.

The DAG plan's approved structural fingerprint excludes its own `TRunInput` type; the
validation artifact explicitly leaves conflict keying to Task 8.6. A successful null
`OutputOf` result is Task 8.3's runtime decision, not a new claim of this source slice.
Tasks 2.1–2.5 and reshape Task 8.2 remain unchecked pending independent approval.

## Validation to reproduce

- Debug and Release solution builds, `-warnaserror --no-incremental --no-restore`: zero
  warnings and errors.
- Focused DAG behavior: 13/13; focused compiled-metadata guard: 1/1.
- Core 350, Ephemeral 79, Durable 99, Acceptance 37, Hosting 24,
  ProviderCertification 96; PostgreSQL 101, SQL Server 72, Integration 11.
- Infrastructure: 240/240. Run the separate `ExpectedRed` lane and confirm exactly its
  14 documented intentional failures, zero unexpected failures. Green package consumers:
  8/8; `dag-hosting` remains one expected-red runtime fixture.
- OpenSpec `validate --all --strict`: 19/19. Verify both worktree and staged-tree
  `git diff --check` are clean.

## Independent controls requested

1. Recompute both freeze anchors, the staged tree and every changed-file hash. Confirm the
   rejected packet remains byte-exact and the new manifest includes its verdict.
2. Compile the four type-only probes and remove the type-reference check in a disposable
   copy: each probe should catch the corresponding mutation without relying on a member
   reference. Also probe a forbidden internal method/overload and a permitted public call.
3. Reproduce the 100,000-node valid chains in both directions, and reintroduce recursive
   traversal in an isolated process to verify the crash regression. Check the seven exact
   diagnostic sets and self-dependency's single result.
4. Recompute the DAG source hash, declaration crosswalk, Task 7.3 rows/digest, validation
   artifact pin, review-history catalog and active-freeze content record.
5. Sweep all active normative/binding/guide docs for a remaining “friend not compiled”
   claim. Confirm all four corrections say candidate, not checkpointed approval.
6. Re-run the builds, product/provider/container lanes, Infrastructure, package fixtures,
   intentional reds, strict OpenSpec and committed-diff whitespace check.

If approved, checkpoint only the frozen manifest bytes. Add the verdict byte-exact in a
direct-child evidence commit, then activate review state without folding in Task 8.3.
