# Independent review — Task 8.4 BR-1/BR-2 contract remediation

Date: 2026-10-04. Change: reshape-developer-facing-interfaces. Contract only.
Base/parent: `c85fd0f301f110db691a026bcdd0c0e35c6b26c1`. Nothing staged; no checkpoint authorized yet.

## Authorization and design decision

Review exactly the fifteen paths in `docs/review/developer-facing-interface-section-08-task-8-4-durable-dag-bridge-contract-br-remediation-dirty-manifest-2026-10-04.txt`.
The revised contract is `openspec/changes/reshape-developer-facing-interfaces/artifacts/task-8-4-durable-dag-bridge-contract-br-remediation-2026-10-04.md`: 36345 raw bytes,
SHA-256 `6be4d3d9dfcb52078a518713819420b84ea7dbd02aef49ceb0506a6f081d06ce`.
This target takes the reviewer's explicitly offered NARROWER BR-2 option. The six
operations are a child protocol, not a complete run API. Section 10 defines a named
separate Task 8.4/8.5 run-persistence/progression CONTRACT gate before any bridge
source, its required run/readiness/cancel/failure/terminal/recovery responsibilities,
the Durable.Hosting-owned ready callback direction, and a proposed discriminated
run stream. That representation lies outside the Task 8.0 continuation-only reading:
an independently approved canonical amendment and sync are required before source.
Approval here must NOT be read as approval of that new aggregate/schema/port shape,
its future signatures or its ready-kind allowlist extension.

Task 8.4 stays open. No product source, friend, package edge, public API, canonical
or delta spec, task checkbox, acceptance waiver or ExpectedRed disposition changes.
The only C# edit strengthens an EXISTING infrastructure gate; it adds no Fact/Theory,
so no declaration crosswalk increment is due. All bridge implementation remains blocked.

## Findings fixed in this target

- BR-1: dedicated durable-runtime Include-only DAG pump, independent of dispatcher
  and DAG role; unconditional all-host transport exclusions plus post-claim rejection;
  committed outcome before acknowledgement; per-kind poison/retry dispositions;
  required DR-037/PR-015 and DR-AC-029-style certification extension. InMemory's
  transport sink cannot be a handler. These are contract corrections, not a claim
  that today's unchanged runtime already routes the records correctly.
- BR-2: remove the complete-seam claim. The separately reviewed section 10 target
  owns creation/reopen, typed input + graph binding, fenced recovery/discovery,
  mapping failure + blocked closure, joins, run cancellation/terminalization,
  notification-driven mapper wakes and waits, fault recovery and persistence kind.
  It must review final signatures and metadata allowlist before either 8.4/8.5 source.
- P3-1: deterministic child InstanceId/full-coordinate binding, no DAG key in the
  public caller start-key namespace. Any needed internal domain is reviewed later.
- P3-2: present null is the four fixed-codec UTF-8 bytes for null, HasInput true,
  explicit declared type/format and ordinary committed-byte SHA-256 fingerprint.
- P3-3: MaxConcurrentNodes is explicitly PER RUN, including parked children and
  committed yet unmaterialized starts until authoritative terminality.
- P3-4: both the revised contract and the rejected predecessor are whole-artifact
  normalized-SHA pinned in guard source on the canonical synchronization gate,
  independently of activeFreeze; Task 8.4 links the revised artifact.
- Status: CLAUDE.md records the actual completed runtime-view closeout chain; CP-022
  and doc 17 distinguish the approved implemented null evaluator from the still
  unimplemented durable bridge. Task 7.3 row 1/digest and doc/task block pins refresh
  atomically. No dated decision or frozen review record is rewritten.

## Rejected evidence retained

Original manifest: 534 B /
`a7235ef34da53cc1e7d3ec6089ff496f80f63898a4f653e2bd0c0be8fa9004ef`.
Original request: 7483 B / `bcd4da55fd4d0c513992f30d2f5d11089f4ab840ae046e3d2195ec667690a3aa`.
Original artifact: 21406 B / `acbb4b7030cc1c7e2c7d5c48d7d9830dcebe21e362cbc60092db41cc59075b50`.
REJECT: `docs/review/developer-facing-interface-section-08-task-8-4-durable-dag-bridge-contract-independent-review-verdict-2026-10-04.md`, 16148 raw B /
`914ae8081cb117a933f6bdee4816313ae86af71038bd5fe69a590e0508bf184f`.
All three original packet/artifact blobs still equal the rejected tree
`80a4b5b68fc84d2b2a113ee840a487abd30a4cb1` byte-for-byte. A new rejected-freeze
record binds the original request/manifest/verdict; immutable-document-history
catalogues the verdict under its separate LF-normalized byte count/hash. Raw and
normalized hashes are not conflated. The rejected artifact is durably source-pinned.
No REJECT is represented as approval.

## Exact self-inclusive freeze and reproducible recipes

- Raw porcelain: fifteen entries, 1446 B,
  SHA-256 `e6330a42bb0e1303e2676d6b72e4ce034f5a0e35d3921b4eabdc664f98923734`; XY + space + path, UTF-8/LF and terminal LF,
  exact Git-emitted tracked/untracked order, --untracked-files=all --no-renames.
  This request and its manifest are included. Compare commit-real diff-plus-untracked
  path set, NUL status, copied-index staged set and every raw blob as well.
- Semantic record: eight rows, 1162 B,
  SHA-256 `dedc94ab4030edb311703afd6154108c8432caa40d6783b326f464654059cf39`. Exclude docs/review paths and exactly the two
  history/provenance fixtures. Emit path<TAB>raw-byte-length<TAB>lowercase-SHA256,
  sort COMPLETE rows ordinally, UTF-8/LF join with one terminal LF. No normalization.
- Active record: fourteen rows excluding only review-manifest-provenance.json;
  emit XY<TAB>path<TAB>raw-length<TAB>lowercase-SHA256, sort complete rows ordinally,
  UTF-8/LF + terminal LF. Its count/digest are pinned in activeFreeze and reported
  in the handoff. The fixture is excluded solely to remove self-reference.
- The simulated tree is reported in the final handoff after copied-index rehearsal.
  Embedding this request's own final tree in itself is self-referential. Reviewer
  must independently reproduce it and compare its parent before authorizing a commit.

Canonical provenance remains 180 rows / 47,347 B /
`40d4d8c0b9144ea087d7b36d33df35d4736942ae615f50126f7ba136b20da8c1`,
zero pending and semanticApprovalEligible true for the EXISTING canonical corpus,
not an assertion that the proposed future persistence amendment is already approved.

## Required independent questions and negative controls

1. Is the narrowed scope unambiguous, and does section 10 gate every missing run
   responsibility, persistence amendment and final allowlist before any bridge source?
2. Can any reserved DAG intent reach the external transport, including a host with
   no DAG role or no registered dispatcher? Does every permanent failure remain
   visible without losing the start/join/cancel fact or freeing an ambiguous slot?
3. Do typed mapping, committed-byte null/input identity, lineages and cancellation
   winner rules remain faithful to doc 17/CP-022 and the approved runtime view?
4. Does the explicit callback direction respect the one-way package graph, and is
   startup recovery + subscribe/recheck required rather than terminal polling?
5. Does the future proposed run aggregate correctly trigger the Task 8.0 amendment
   stop rule, instead of assuming WorkflowEventCodec accepts unknown record kinds?
6. After the freeze is cleared in an isolated copy, alter only the revised contract's
   identity recipe or routing rule: the focused canonical gate must fail on its new
   durable source pin. Alter only the rejected artifact: its independent pin fails.
7. Reopen a false stale-status sentence: exact normalized block/source-map pins fail.
   Tamper with the preserved rejected packet: immutable/rejected-freeze pins fail.

No runtime behavior implementation or bridge integration proof is claimed. The old
UnsafeAccessor/expression prohibition and DagRuntimeView.cs evidence-path notes are
still explicitly owned by the future SOURCE target, not marked closed by this plan.

## Validation reporting and sequencing

The handoff reports the checks actually rerun on this freeze, including both
non-incremental warn-as-error builds, Infrastructure/ExpectedRed separately, focused
pin controls, exact signature-block compile, strict OpenSpec, current-match -Check,
staged whitespace and copied-index tree/raw-blob verification. Do not treat inherited
product/container counts as new runs. Any omitted lane is stated explicitly.

Write a NEW immutable verdict using stem
developer-facing-interface-section-08-task-8-4-durable-dag-bridge-contract-br-remediation-
independent-review-verdict-YYYY-MM-DD.md and one final APPROVE/REJECT line. Do not edit
the packet, rejected evidence or main's index. The verdict is an additional file for
the evidence commit, not part of this fifteen-path freeze.

Only after APPROVE and zero-drift verification: checkpoint exactly fifteen paths at
the rehearsed tree, then its single-parent direct-child evidence commit must add and
catalogue the verdict and clear activeFreeze while keeping the history manifest
pointer. No task-completion activation is planned: 8.4 and 8.5 remain open. Next is
the section 10 contract/amendment gate, NOT source and NOT archival.
