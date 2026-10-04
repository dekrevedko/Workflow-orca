# Approved runtime-view atomic canonical and registry transition

Date: 2026-10-03
Change: `admit-dag-hosting-runtime-view`, prepared Tasks 1.3/1.4
Base activation: `64d97c644475f6dfbe183540bf546c7ab48eab46`

## Contract authority

Contract checkpoint `43d869fc29e7daa3ec567d4602960f458eb98492`, tree `5c9d63fe0b30ea4465054ac01c986a8b80707e62`, is followed by its single-parent verdict evidence commit `1ea7f44b5a32d058e04b913f387317c21747add5`. That commit adds the 10,608-byte independent terminal APPROVE, SHA-256 `5ff6b3322d3071149fefe583fd74fba35afa5d28ea6a81ca50637bcdb029b457`. Activation `64d97c644475f6dfbe183540bf546c7ab48eab46` changes Task 1.2 only. The checkpoint/evidence/activation target is not product-source authority.

## Exact synchronization and supersession

Only two complete verbatim-heading blocks are applied, preserving every preamble and every unrelated requirement/scenario. The approved deltas are unchanged. The two latest canonical hashes and four predecessor identities are source-pinned independently of fixture values. A third heading, fourth owner, rewritten predecessor or unrelated canonical mismatch remains red.

| Capability | Requirement | Historical authoring canonical | Approved runtime-view canonical |
|---|---|---|---|
| `developer-facing-surface` | Implementation package boundaries use exact internal friends | `bed102a2e4c98598b30cbb741c956a236f2050d27d88e6fc567b915f5b260793` | `3a848931f75b44c6cf2edf97b564c6e213b23d90720701a31a4a45394cbf93ab` |
| `repository-foundation` | Dependency direction remains one-way | `bbae0c226c6824570650d1f6f980e3b74e6c35cb196784581eef20e6e3563ce9` | `f0dbc156d044536a83d73934dfcb6eaac5f974069b1a5c555433f2a4b56a1ab8` |

The authoring amendment remains Complete as historical implementation evidence. Both original Proposed arrays and the authoring ApprovedPending/Complete evidence stay unchanged. Schema 7 adds the runtime-view ApprovedPending pair, its actual contract approval evidence, and four change/capability/heading/operation/hash-bound superseded predecessors. Both older records resolve through exactly one active or dated archived change; duplicate active/archive records fail closed. Actual archival still requires an independently reviewed inventory/provenance refreeze. Proposed withdrawal is a separately reviewed removal of both runtime-view rows and the directory; after synchronization reversal requires a new approved successor/reverse amendment. No archive or withdrawal occurs in this target.

## P3-1: permanent superseded contract decisions

Both dated contract artifacts below are retained unedited and catalogued by change-relative path and normalized SHA-256. They describe their own preparation dates, not current status. Guard-source pins and the source-pinned fixture list protect them independently of the active freeze or refreshable current-match pins. Resolve their owning change active or dated archived.

| Historical relative path | Normalized SHA-256 |
|---|---|
| `artifacts/task-1-1-runtime-view-contract-2026-10-02.md` | `5527189563e0f39eccbb9e56bc902d2e1e4cc9e2695e0db929cc5e4b03a7dc12` |
| `artifacts/task-1-1-runtime-view-contract-rv-remediation-2026-10-02.md` | `2dec007df0e06740373fdc4c4d0cd69066d0d08cda417f141a3897a4b3824398` |

Contract catalog recipe: ordinal-sort `relative-path<TAB>normalized-sha256`, LF join and one final LF: 255 bytes / `6e65ddb74a05be1ff935ac75d73ec492170414e8f3ccf6915df450c38c7903bc`.

## P3-2: unchanged runtime owner consistency and bridge handoff

The canonical durable-runtime “Durable DAG progression is runtime owned” block is unchanged at `11741edaf2f26fd43846948d5395a8fe5be92ce61c016e3dc232d11dc810cd40`; workflow-authoring “Built definitions are immutable” is unchanged at `d1a5dc99ac3f9c8b2ecf9285d3ff7df0bcb3a547240b4c6b0d2621243b37a240`. Both are verified separately from the new package-boundary blocks.

Eager declared-type materialization applies to **every successful direct resultful dependency output**, including an output that the mapper never reads. An ordinary decode failure is DAG_INPUT_MAPPING_INVALID before mapper invocation, node-input commit or child start. This is the approved declared-type validity refinement; the runtime owner does not promise lazy decoding or success for invalid committed values. Dependency readiness/success, independent-node progression, opacity, commit-before-start and durable codec ownership are unchanged. Reshape 8.4 must confirm this same bridge rule and its separation from protocol/storage failures before 8.5 implements it. A discovered cross-tree contradiction requires an independently approved owning delta before source.

Only OutOfMemoryException exclusion is executable-testable in-process. StackOverflowException and AccessViolationException exclusions are process-integrity policy, not recoverable mapper outcomes and not unsafe in-process throw probes. Mapper-thrown OperationCanceledException is normalized; host/run cancellation outside invocation keeps its runtime outcome. The existing fixed codec and authoring allowlist remain unchanged.

## Authority still withheld

Task 1.3 is synchronized in this candidate; Task 1.4 stays open until independent review/checkpoint. It is deliberately not pinned Open so its later checkbox-only activation is green without a C# edit. Tasks 2.1–3.2 remain pinned Open. Current compiled friends remain eight. No src/**, friend attribute, public API, package reference, codec allowlist or declaration-count change occurs. Approval of this atomic target authorizes only its exact checkpoint; the runtime view/member-reference/source slice is the next separate reviewed target.
