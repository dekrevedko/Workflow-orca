# Independent review — proposed Task 8.4 durable DAG bridge contract

Date: 2026-10-04. Change: reshape-developer-facing-interfaces. Contract only.

## Authorization boundary

Review the exact five-path uncommitted target on base `c85fd0f301f110db691a026bcdd0c0e35c6b26c1`.
The new artifact is `openspec/changes/reshape-developer-facing-interfaces/artifacts/task-8-4-durable-dag-bridge-contract-2026-10-04.md` (21406 raw bytes,
SHA-256 `acbb4b7030cc1c7e2c7d5c48d7d9830dcebe21e362cbc60092db41cc59075b50`). No product source, canonical/delta spec,
friend, package edge, public API, task checkbox, acceptance waiver or intentional
ExpectedRed disposition changes. It proposes the implementation seam required by
the existing Task 8.4, not another friend amendment. Archival is not in scope.

The pushed base finishes the approved runtime-view amendment: closeout checkpoint
`3270678409593c9af0a7c1810f3c4be329b133fd`, direct-child evidence
`496f30ccd91fbe63b8fd099c546d36864c440a4d`, then Task 3.2-only activation/base.
Its APPROVE bytes are immutable: 9,888 B /
`25962bbf9e461fe8fb5aaddac6b846b4820442b3e45525f52751773611ae94cc`.

Task 8.4 remains open because it includes source implementation. Tasks 8.3–8.10,
AC-613/614 and the compile-aware Section 8 exit are not completed by this proposal.
Approve/reject the contract before source; green checks are not contract approval.
If a proposed choice conflicts with either normative tree, require an amendment,
not a silent canonical edit.

## Exact self-inclusive freeze

- Manifest: `docs/review/developer-facing-interface-section-08-task-8-4-durable-dag-bridge-contract-dirty-manifest-2026-10-04.txt`, five paths, 534 B,
  SHA-256 `a7235ef34da53cc1e7d3ec6089ff496f80f63898a4f653e2bd0c0be8fa9004ef`. Raw Git porcelain v1 order, all untracked,
  no renames; raw/NUL entry sets and commit-real diff-plus-untracked paths agree.
  It names this request and itself. Main's index must be empty.
- Semantic record: one artifact row, 185 B,
  SHA-256 `f498aa76cc29460fa07bebf36264da6bdd30dbae83c17b90c0a4db397d8bc90c`. For each manifest path other than docs/review
  files and the exact two history/provenance fixtures, emit
  path<TAB>raw-byte-length<TAB>lowercase-SHA256. Sort full rows by ordinal string
  order; UTF-8, LF separators and one terminal LF. No line-ending normalization.
- Active record: four rows, excluding only the exact review-manifest-provenance.json
  self-reference. Each row is XY<TAB>path<TAB>raw-length<TAB>lowercase-SHA256,
  full rows sorted ordinally, UTF-8/LF with one terminal LF. Its byte count/digest
  are stored in activeFreeze and the handoff.
- The staged tree is reported in the final handoff after rehearsal using a copied
  index. Embedding a request's own tree in that request is self-referential. Compare
  the reported tree and base parent before authorizing any checkpoint.

The two fixture edits only admit the new immutable request/manifest and pin this
active target. Historical packets, verdicts, registry rows and canonical provenance
are not rewritten. Provenance remains 180 rows / 47,347 B /
`40d4d8c0b9144ea087d7b36d33df35d4736942ae615f50126f7ba136b20da8c1`,
zero pending and semanticApprovalEligible true. Current-match pins remain maximal.

## Questions requiring substantive review

1. Do the six internal operations and their exact types fit the existing
   Durable.Hosting → Dag.Hosting friend and one-way package graph? The descriptor
   uses Guid/ordinal because Durable.Hosting cannot reference Dag-owned IDs/types.
   No mapper delegate, Core/engine/provider DTO or workflow private state crosses it.
2. Is the proposed DAG-run lineage anchor, deterministic identity byte recipe and
   full-coordinate collision check sufficient for AC-614, without inventing a
   synthetic executable workflow or new public navigation API?
3. Does the single-stream input/start-intent commit plus retryable second child
   commit honestly satisfy DU-031/033 and AC-613? It explicitly does NOT promise
   cross-stream atomicity or exactly-once physical delivery. Review all crash points.
4. Are eager declared-type decoding of every successful resultful dependency,
   exact immutable map construction, successful null, exception exclusions and
   fixed-codec committed-byte fingerprints faithful to the approved runtime-view
   and both normative trees? Codec ownership remains on the durable side.
5. Is the cancellation/start-authorization boundary sound with two streams? Once
   admitted, an ambiguous child must be created/reattached and cancelled, not inferred
   absent. Pending durable intents retain admission until terminal; review that
   representation with the future 8.5/8.7 implementation.
6. Are start/join/cancel kinds disjoint from continue and workflow-event in the ONE
   provider outbox? Does terminal join use authoritative committed output/failure
   and subscribe/recheck rather than polling or a live child state object?
7. Does §9 preserve every future compiled exit and require behavior through the
   shipped Hosting assembly and both real production providers, not a linked-harness
   friend shortcut or in-memory mock?

The closeout's later UnsafeAccessor/expression guard notes and DagRuntimeView.cs
evidence-path note are explicitly carried into the 8.4 SOURCE review obligation;
they are not claimed fixed in this documentation target. The stale successful-null
implementation-status sentences in doc 08/doc 17 are also disclosed for that next
reviewed source/document reconciliation. Current decisions are not silently changed.

## Verification scope

Non-incremental Debug and Release solution builds with -warnaserror pass at this
unchanged product source: zero warnings/errors. The exact C# signature block also
compiles independently under the repository SDK with only the OrcaCore contract
project reference; no Dag, engine, provider, protocol, Core or new friend is required.
An initial check referenced the former project filename and failed; correcting the
local check project to src/OrcaCore.Abstractions/OrcaCore.csproj yielded 0/0. The
signature text was unchanged.

Infrastructure on the pushed base passes 243/243; strict OpenSpec passes 20/20.
Final frozen Infrastructure, separate ExpectedRed, current-match -Check, whitespace
and copied-index staged-tree results are reported in the handoff after packet
construction. No new behavioral or mutation evidence for an unimplemented bridge
is claimed. The full product/provider/container counts in prior verdicts are prior
evidence, not tests rerun for this contract target.

## Review output and sequencing

Write a new immutable APPROVE or REJECT verdict under the per-task stem
developer-facing-interface-section-08-task-8-4-durable-dag-bridge-contract-
independent-review-verdict-YYYY-MM-DD.md, with one final verdict line. Do not edit
this request/manifest, earlier dated decisions, frozen evidence or main's index.
The verdict is an additional file for the following evidence commit, not this freeze.

After APPROVE, recheck zero drift and checkpoint exactly the five manifest paths
at the reported tree, then make its direct-child evidence commit adding/cataloguing
the byte-exact verdict and clearing activeFreeze while retaining the history pointer.
There is no completion-checkbox activation for this contract-only preparation:
8.4 remains open until its separately reviewed source target is implemented.
Do not start source from validation alone or mix archival into either target.
