# Issue #16 frozen contract — independent review

Date: 2026-10-29, America/Los_Angeles.

Verdict: **APPROVE, with one non-blocking P3 clarification for task 2.1.**
No P1 or P2 findings. This is a fresh adversarial review in the current chat,
separate from the author's earlier self-review; it does not claim a second human
or external reviewer. It does not grant owner approval or authorize implementation.

## Target and scope

- Branch: `codex/issue-16-ephemeral-failure-settlement`.
- Base/HEAD: `0a8e375fbcf5005a2a2f491cf24dfbbd9d64c580`.
- Manifest: `contract-manifest-2026-10-29.txt`, exactly 26 paths.
- Candidate tree: `cf054886e39c20d9294b3baac55e58fa19c6bbdf`.
- Content SHA-256: `995a02e57923cbad1073c42c066f7e3c13c1b2c45dcef0318bbb964d5aa026cb`.
- Original porcelain SHA-256, excluding review verdicts:
  `1e1f69a9ce80ab2f2d55ff842952843df53868477fc597a0761d9136e51ea7d7`.

The review read the live GitHub issue and owner directions, proposal, design,
all five delta specs, tasks, exact API, scenario inventory, owner routing,
source audit, proposed-owner registration, provenance and validation records,
both tracked guard/fixture diffs, relevant current source and canonical/numbered
requirements, retained build/TRX/probe evidence and ReSharper SARIF.

No frozen path was edited. This report is outside the checkpoint manifest. The
earlier author self-review is preserved separately; this report does not silently
replace its bytes. No checkpoint, push, issue edit, canonical sync or source
implementation was performed.

## P3 — clarify when the declaration companion changes

Location: `specs/developer-facing-surface/spec.md`, lines 13–14.

The new normative paragraph groups "declaration baselines" with executable
API/compile/package baselines that must change with actual source. In contrast,
task 2.1, the exact-API artifact and owner-routing record explicitly put
`docs/specs/17-public-authoring-contract.cs` in the preceding canonical transition.
The companion itself identifies it as a declaration artifact rather than product
source. Both staging instructions cannot describe that same artifact literally.

Clarify this in the independently reviewed task 2.1 target: the normative
declaration companion changes with the canonical/numbered contract; executable
assembly API, compile and package baselines change with source. This does not
alter either proposed member or the runtime behavior and does not block the
current contract checkpoint after explicit owner approval. Leave the current
frozen target unchanged during this review.

## Exception handling and detached metadata

The two carriers have distinct, coherent purposes. The existing
`WorkflowOutputUnavailableException` can receive the original through the
already-supported protected `OrcaCoreException` constructor and inherited
`Exception.InnerException`; it needs no new public member. The proposed
`WorkflowFailure.ExceptionType` getter contains only detached type-name text,
not an exception, `Type`, dispatch object or synthetic cause.

The positive promise is reference identity for the actual elected business-step
exception, including an explicit returned error, for both output-wait forms.
The engine does not rethrow/reset the object, reconstruct it, or expose its own
normalization wrapper. User-thrown `TargetInvocationException` is preserved;
only a wrapper known to have been created by engine invocation may be unwrapped.
Business execution is distinguished from DI construction, initialization,
selectors/merges, codec and lifecycle failures. The latter gain no live-original
promise.

The final retry and real failure/attempt identity govern selection. Equal messages,
types or locations cannot identify an exception. Single branch/item propagation
preserves the association; a true join aggregate has ordered detached causes and
no invented live `AggregateException`. Recovery and timeout/cancellation/termination
winners cannot inherit an abandoned attempt's original. The existing
`WF-STEP-UNHANDLED` code is reused, genuine OrcaCore codes survive, and blank
messages have an exact detached fallback while the original remains unchanged.

Snapshots, branch/item data, Core/provider records and codecs retain only detached
values. The narrow live graph is explicitly confined to the ephemeral wrapper;
its potential application references are acknowledged. Output pins protect the
atomic capture, retirement clears engine-held references, and already delivered
objects remain caller-owned. Durable runtime failure production and live-inner
behavior are unchanged.

The wire amendment is explicit rather than an accidental optional JSON property:
legacy five-field nodes, seven-field version-2 typed nodes, exact property order,
recursive mixed graphs and malformed/duplicate/version/type/provenance negatives.
The exact legacy-byte control applies to an unchanged all-null-type graph;
typed descendants necessarily add their specified version-2 bytes. New readers
accept old forms; old readers reject typed forms. The unchanged codec ID is an
expressly proposed subformat amendment requiring owner approval, with a matched
new package closure and no promise that old binaries can consume new typed data.
Type names remain opaque text and cannot activate CLR type resolution or constructors.

## Settlement and lifecycle

The proposed base method is nonvirtual, inherited by resultful handles, and
available for resultless handles without inventing an output or another service.
Pre-cancelled tokens win before mode/proof checks; otherwise durable handles
reject without provider or DI access.

Current lifetime `WhenSettled` is a replaceable zero-owned-work epoch, and
`TryOwn` presently allows another epoch before retirement. The contract correctly
requires a new monotonic receipt: terminality, final physical handoffs and a seal
against later owned admission. It does not simply expose the existing Task or
reuse the stronger retirement predicate that also waits for query pins.

Subscribe/recheck, observer-local cancellation, no physical/host lease and no
payload pin avoid lost notifications, self-counted shutdown obligations and
retirement interference. Original receipts survive retirement and key reuse
without a runtime lookup, payload resurrection or global tombstone. Closed and
Stopping allow passive observation; Completed and correct owner disposal permit
only the completed local proof without dependency, codec, user, timer or commit
work. Ordinary output/query operations keep their separate lifecycle restrictions.

The resource recipe distinguishes step-owned resources from shared/root DI
ownership. It does not infer settlement from an unaccepted start error, logical
timeout, cancelled observer or raw disposal. No accepted handle means no invented
per-start proof: step/external ownership or a completed host join is required.
Fire-and-forget child work and self-referential waits are explicitly outside the
promised resource-release proof.

## Verification performed in this review

- Re-ran strict OpenSpec validation: **26/26**.
- Verified every frozen file's byte count and SHA-256, every Git blob against the
  candidate tree, exact 26-path tree inventory, base/branch, unchanged author
  verdict and empty real index. Candidate-tree whitespace check passed with CRLF
  handling for the preserved raw SARIF.
- Independently rehashed all **218** provenance delta/canonical block pairs.
  Filtering the 12 new rows reproduces the base **206-row / 54,532-byte** record
  and SHA `63ee51d0001b33218e687a34e16058b5c972f8451de2c3454045facc5a96cd4e`.
  The new record is **57,773 bytes**, SHA
  `881aa5392e6deee881abde5620fcd50e05ebab38222008f79ff4b86e1a3cee58`.
- All **27** predecessor files match their base Git bytes and catalogue hashes.
  Both the 36-directory active inventory and 14-directory canonical inventory
  reproduce their pins. All nine proposed predecessor/successor pairs have exactly
  two owners and reproduce predecessor, canonical and successor hashes.
- Inspected the complete process-validator diff. It admits only the exact pinned
  proposed registration, preserves prior approvals, requires null approval fields
  and open canonical/source tasks, checks pending classification, and rejects a
  third owner. Semantic approval remains false: nine modifications and three
  additions are pending. No application API or runtime baseline was exposed.
- Independently compared ReSharper rule/level/message instance multisets for the
  changed guard file with the preview.3 baseline: **0 errors, 50 warnings,
  43 notes; no new instances**. Inspected the retained four semantic-probe records
  and restored green control. These probes were not re-executed in this review.
- Inspected retained warnings-as-errors Release build evidence (**0/0**) and
  the full Infrastructure TRX: **309 executed, 309 passed, 0 skipped/failed**.
  Build, Infrastructure, runtime/provider/integration/stress/native lanes and
  InspectCode were **not re-run** in this review. Their retained results are
  distinguished from the checks independently executed above. Future behavior
  and native qualification remain source-phase obligations, not current proof
  that the proposed members already exist.

## Gate and cleanup

Keep task 1.4 open until direct owner approval. An approved contract checkpoint
and verdict do not authorize canonical sync or source implementation; the next
canonical target still needs its own approval. No implementation was started.

This review created no build output or disposable worktree. The previous
validation checkout is unregistered and removed, except for the already reported
small MSBuild cache recreated at `E:/oc16c/.artifacts/publishing-validation/issue-16-contract/tmp`.
Automatic approval review previously blocked its recursive deletion with
"blocked by policy". This review did not retry or bypass that rejection.
