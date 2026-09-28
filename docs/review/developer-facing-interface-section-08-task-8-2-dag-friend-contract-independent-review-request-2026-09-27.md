# Independent review request — DAG authoring friend contract

Date: 2026-09-27
Base: `5adddc3ba0ca7e0f70ee1f3b7317e69c1df7e76a`
Manifest: `developer-facing-interface-section-08-task-8-2-dag-friend-contract-dirty-manifest-2026-09-27.txt`

Review the post-gate *contract amendment* in
`openspec/changes/admit-dag-authoring-friend-boundary/`, especially both full
`MODIFIED` requirement blocks. The process-only successor gate was independently
approved and checkpointed as `d05dbe3` -> `3895a36` -> `5adddc3`; that review did
not approve the friend. This target requests that missing semantic approval.

## Decision to review

- Add exactly one future product friend, `OrcaCore -> OrcaCore.Dag`, for DAG
  authoring construction. It does not exist in compiled metadata in this target.
  The current seven product friends remain unchanged and explicit in `CLAUDE.md`
  and document 17.
- Limit the *actual DAG consumer* to the internal constructors of
  `Validation<T>`, `WorkflowDiagnostic`, `AuthoredLocation`,
  `DefinitionFingerprint`, and `WorkflowDefinitionException`, plus one new
  internal canonical UTF-8/SHA-256 operation on `DefinitionFingerprint`. The
  compiled-metadata guard planned for Task 2.4 must pin exact signatures and
  arity, reject all other non-public `OrcaCore` references, and negative-test
  another internal type, method, and overload. A CLR friend alone is not the
  enforcement mechanism.
- Reuse `Validation<T>` diagnostic ordering and the current
  `WorkflowDiagnosticCatalog`; move only the shared hashing primitive from Core
  to `DefinitionFingerprint` after approval, with workflow fingerprint golden
  tests. DAG remains dependent only on `OrcaCore`. No `Core -> Dag` reference,
  public factory, DAG-owned parallel validation family, runtime privilege, or
  child-start access is proposed.
- Keep `OrcaCore.Durable.Hosting -> OrcaCore.Dag.Hosting` as the sole
  DAG-to-durable *runtime* bridge. The pinned Task 8.0 map now states that
  precise meaning, rather than calling it the sole product bridge.
- After this verdict, synchronize the two canonical successor blocks and move
  the registry to approved-pending **atomically** under a separate freeze and
  review (tasks 1.3/1.4). Retain both predecessor records. Do not put a
  completed amendment row or fictional executable/final-verdict evidence in
  the registry now.

## Scope and evidence

The target changes the existing proposal, design, and task ledger; `CLAUDE.md`;
document 17; the Task 8.0 map; and the Task 7.3 documentation row and its two
source-level digest pins. The two exact-heading `MODIFIED` deltas remain
byte-identical to the approved process checkpoint and are semantic inputs to
this review. Inspect their complete requirement and scenario blocks, not only
the changed-path list. `OrcaCore` source, compiled friend metadata, the exact
product-friend guard, and canonical `openspec/specs/` are unchanged.

The existing five constructors are in
`src/OrcaCore.Abstractions/Instances/PublicWorkflowValues.cs` and
`src/OrcaCore.Abstractions/Errors/WorkflowDefinitionException.cs`. Their
current accessibility is internal; `Validation<T>` sorts diagnostics by
authored-location value and code, and `WorkflowDiagnostic` validates catalog
codes. The sixth member is an approved *future* operation, not an existing API.

Release `-warnaserror --no-incremental` builds with zero warnings/errors;
Infrastructure guards pass 226/226; OpenSpec strict validates 19/19;
`refresh-review-manifest-current-matches.ps1 -Check` and `git diff --check`
are clean. Re-run against the frozen bytes and report expected-red scenarios
separately from Infrastructure failures. The two successor operations remain
pending in the provenance checkpoint; `semanticApprovalEligible` is false
until an independent contract verdict and the later atomic sync/transition.

Before verdict, reproduce the raw-porcelain manifest, every file's bytes, and
the base and staged tree. Negative review questions: would a public factory,
`Core -> Dag` reference, extra internal DAG member, new runtime friend, or a
non-atomic 1.3/1.4 transition be authorized by this text? Each must be **no**.

If approved, checkpoint only the frozen manifest; add the verdict byte-exact
in a separate evidence commit; activate only the review state. Task 1.2 is
open until that sequence completes. No Task 8.2 product source or friend
attribute may land before it.
