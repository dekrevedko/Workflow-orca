# Task 2.1 occurrence decisions — 2026-11-01

Base: 7697c7981c02d03408cbccf9a2e0b9bade33ca14. Scope includes every active numbered Markdown/C# companion,
all canonical capability specs, the five current delta specs, README/package docs,
the source map and both engine/native guides. The literal case-insensitive query is:

```text
WorkflowFailure|WorkflowOutputUnavailableException|WaitForOutputAsync|WaitForSettlementAsync|InnerException|ExceptionType|FailureOccurrence|WhenSettled|WF-OUTPUT-UNAVAILABLE|WF-STEP-UNHANDLED|WF-LEGACY-LIFECYCLE|settl|physical quiescen|never live|Neither mode SHALL return live|every runtime operation|all calls through|cached Completed|after Completed|post-Completed|From Stopping|snapshots/copies|CLR type|exception type|failure subformat|five-field|five-property|seven-property|original.exception|live.exception|declaration.*baseline|normative declaration companion
```

The replay finds 298 base hits
and 492 current hits. There are 502 individually
accounted rows: a retained current row links its exact base line; a new current
row is Add; an unmatched old hit is Edit with no fabricated current-line mapping.
Every retained line is byte-equal after the stated LF normalization; edited old
lines never receive a false Retain label. Header/paragraph decisions below name
the actual semantic owner and why its rule is kept or reconciled. These are explicit
decisions, not keyword-derived semantic dispositions or an occurrence template.
Canonical/delta duplicate text is independently linked by the twelve block hashes.

The TSV SHA-256 is `69beb586e553c93273ff14f9d1268039756dfda123cc68783a312057663c2506`. Scope with zero hits
is still recorded in the adjacent coverage JSON. No historical review artifact is
silently treated as current normative text. This is documentation coverage, not
runtime acceptance evidence.

## Reviewed owner decisions

### D001 — README.md: Issue #16 pending application contract

Category: CrossCapabilityDependency. TSV rows: 1, 2, 3.

Advertise only the approved pending contract; explicitly exclude the members from preview.3 and link the resource-owner caveats, without changing release claims.

### D002 — docs/ephemeral-engine-developer-guide.md: Registration, start, and inspection

Category: CrossCapabilityDependency. TSV rows: 4, 5.

Retain the implemented typed output-wait and local cancellation recipe. Its existing output completion remains logical, not new physical proof.

### D003 — docs/ephemeral-engine-developer-guide.md: Retained-instance lifetime

Category: CrossCapabilityDependency. TSV rows: 6, 7, 8, 9, 10.

Keep count/age, pin, start-to-observe not-found and physical-abort warnings. The pending receipt adds no output retention lease or effect-safe retry.

### D004 — docs/ephemeral-engine-developer-guide.md: Shutdown drain recipes

Category: CrossCapabilityDependency. TSV rows: 11, 12, 13, 14.

Keep all three existing drain/DI recipes; add the explicitly pending passive-receipt exception to the old post-Completed allowlist.

### D005 — docs/ephemeral-engine-developer-guide.md: Issue #16 resource-release recipe (contract approved; implementation pending)

Category: CrossCapabilityDependency. TSV rows: 15, 16, 17, 18, 19, 20, 21, 22, 23, 24.

Specify external-owner settlement before job disposal, no-handle fallback and unjoined-process limit; preserve shared exception stacks with ExceptionDispatchInfo/wrapping and clearly mark the API absent from preview.3.

### D006 — docs/ephemeral-native-support.md: Build a candidate package consumer

Category: AcceptanceReference. TSV rows: 25, 26.

Preserve the working issue #13 public package output example and ownership claims for its exact version; it is not a witness for the new pending receipt.

### D007 — docs/ephemeral-native-support.md: Process and type boundary

Category: CrossCapabilityDependency. TSV rows: 27.

Preserve native support limits and the unchanged process/metadata ownership boundary; adding diagnostic type text grants no new event/DI or cross-process authority.

### D008 — docs/ephemeral-native-support.md: Issue #16 pending extension (2026-11-01)

Category: AcceptanceReference. TSV rows: 28, 29.

Require new six-cell original/type/wire/receipt execution and JIT type-name parity, while preserving the old release evidence and prohibiting broader native credit.

### D009 — docs/normative-source-map.md: Native publishing assessment (approved 2026-10-21)

Category: AcceptanceReference. TSV rows: 30.

Preserve the dated issue #3 no-new-API assessment record. Its declaration/baseline freeze describes that assessment, not a prohibition on the separately approved issue #16 amendment.

### D010 — docs/normative-source-map.md: Issue #13 native support contract transition (2026-10-24)

Category: AcceptanceReference. TSV rows: 31.

Preserve the dated issue #13 planning/source-gate history and its own later evidence; issue #16 uses its separate current routing and approval chain.

### D011 — docs/normative-source-map.md: Issue #16 transition routing (2026-11-01; implementation pending)

Category: OwningRule. TSV rows: 32.

Route exactly twelve blocks and AC-619–638 to their numbered owners; distinguish the normative companion transition from later executable baselines and leave approval unset.

### D012 — docs/packages.md: Issue #16 pending application contract

Category: CrossCapabilityDependency. TSV rows: 33, 34, 35.

Keep installed-package promises tied to published preview.3; describe the future two-member contract and matched-version/source/native gates separately.

### D013 — docs/specs/03-domain-model-and-glossary.md: Definitions and authoring

Category: CrossCapabilityDependency. TSV rows: 36.

Keep Code rather than CLR type/message as identity. The nullable type slot is diagnostic text, not an added matching-value family.

### D014 — docs/specs/03-domain-model-and-glossary.md: Lifecycle

Category: CrossCapabilityDependency. TSV rows: 37, 38, 39.

Keep immutable terminal states and physical protection for accepted/provisional work; a logical status alone still cannot justify retention or resource disposal.

### D015 — docs/specs/03-domain-model-and-glossary.md: Issue #16 failure and physical-settlement vocabulary (contract approved; implementation pending)

Category: OwningRule. TSV rows: 40, 41, 42, 43, 44.

Define original application exception versus detached type text and terminal final physical proof, separating query-pin/cleanup eligibility and whole-root ownership.

### D016 — docs/specs/04-requirements-core-runtime.md: CR-009 Shared compiler diagnostics

Category: CrossCapabilityDependency. TSV rows: 45.

Keep authored compiler diagnostic identity and location; runtime exception metadata neither changes build diagnostics nor makes CLR names protocol keys.

### D017 — docs/specs/04-requirements-core-runtime.md: CR-014 Failure semantics

Category: OwningRule. TSV rows: 46, 47, 48.

Own elected business-step reference/type/code/message semantics, returned errors, final retry, aggregate/recovery/late-loser exclusions and durable byte stability.

### D018 — docs/specs/04-requirements-core-runtime.md: CR-014a Workflow failures retain authored and runtime occurrence provenance

Category: OwningRule. TSV rows: 49, 50, 51, 52, 53, 54, 55.

Keep all prior provenance invariants; add exact recursive five/seven-field failure forms, unknown/duplicate negatives, all-null golden bytes and opaque diagnostic metadata.

### D019 — docs/specs/04-requirements-core-runtime.md: CR-016 Synchronous completion bridge

Category: OwningRule. TSV rows: 56, 57, 58, 59, 60, 61.

Preserve existing output/DAG wait meaning and the observation gap; add a distinct token-local physical receipt, inheritance and no-I/O durable rejection.

### D020 — docs/specs/05-requirements-events-waits-timers.md: Ephemeral host drain and producer sealing

Category: CrossCapabilityDependency. TSV rows: 62.

Keep events/timers/retries operational during Closed and sealed at Stopping. The new passive receipt is not event admission or a new producer.

### D021 — docs/specs/08-requirements-composition.md: CP-002 `WhenAll` success join

Category: CrossCapabilityDependency. TSV rows: 63.

Keep one failure versus authored-order SFE-JOIN-FAILED semantics and sibling completion. Live originals never enter detached merge inputs.

### D022 — docs/specs/08-requirements-composition.md: CP-033 Ephemeral failure and settlement ownership

Category: OwningRule. TSV rows: 64, 65, 66, 67.

Own branch/item association, detached aggregates/recovery, durable engine-produced null-type bytes and physical handoffs without observer path/host ownership.

### D023 — docs/specs/09-requirements-management-operations.md: MG-003 Committed state and typed output only

Category: OwningRule. TSV rows: 68, 69, 70, 71.

Retain detached root-state/output and normal wait pins, with a separate exact ephemeral InnerException carrier rather than live query data.

### D024 — docs/specs/09-requirements-management-operations.md: MG-005 Snapshots and closed outcomes only

Category: OwningRule. TSV rows: 72, 73, 100.

Narrow the live-object prohibition to query values/runtime authority; explicitly cross-reference the application-exception carrier without allowing live snapshots.

### D025 — docs/specs/09-requirements-management-operations.md: MG-053 Terminal instances leave memory quickly

Category: CrossCapabilityDependency. TSV rows: 74.

Preserve the explicit ephemeral-retention override to durable eviction; a receipt cannot bypass physical and pin protections.

### D026 — docs/specs/09-requirements-management-operations.md: MG-055 Physical settlement and provisional aborts

Category: CrossCapabilityDependency. TSV rows: 75, 76, 77, 78, 79.

Keep terminal fences, actual physical return, no published handle after abort and retention-off cleanup. An unaccepted start cannot fabricate a public receipt.

### D027 — docs/specs/09-requirements-management-operations.md: MG-056 One complete ephemeral retirement unit

Category: CrossCapabilityDependency. TSV rows: 80, 81, 82, 83.

Keep one payload/key ownership unit and monotonic settlement/count ordering. A small issued receipt survives without retaining that removed unit.

### D028 — docs/specs/09-requirements-management-operations.md: MG-057 Retained handles and output waiting

Category: OwningRule. TSV rows: 84, 101.

Replace the blanket every-runtime-operation rule with ordinary not-found plus the one passive receipt exception; keep output capture/pins and the start-to-observe gap.

### D029 — docs/specs/09-requirements-management-operations.md: MG-060 Concurrency limits

Category: CrossCapabilityDependency. TSV rows: 85.

Keep physical capacity protected independently of logical path tokens and retained data. Receipt observation is capacity-free.

### D030 — docs/specs/09-requirements-management-operations.md: MG-066 Admission closes before shutdown cancellation

Category: OwningRule. TSV rows: 86, 87.

Preserve Closed drain and Stopping admission/error precedence; add only passive existing-handle settlement, without lane/DI/pin/host work.

### D031 — docs/specs/09-requirements-management-operations.md: MG-067 Completed means stable host-wide physical quiescence

Category: OwningRule. TSV rows: 88, 89, 90, 91, 102.

Add an issued completed receipt to the pure post-Completed allowlist; retain producer sealing, real host obligations and no post-disposal engine work.

### D032 — docs/specs/09-requirements-management-operations.md: MG-068 Incomplete reports ownership without claiming stop

Category: CrossCapabilityDependency. TSV rows: 92.

Retain faulted/physical obligations and truthful incomplete reporting. A receipt cannot turn accounting faults or observation expiry into whole-host completion.

### D033 — docs/specs/09-requirements-management-operations.md: MG-070 Passive per-instance physical-settlement observation

Category: OwningRule. TSV rows: 93, 94, 95, 96.

Own the complete monotonic accepted-instance receipt, final seal, local cancellation, retirement/disposal/mode precedence and no-handle/root/unjoined-work caveats.

### D034 — docs/specs/09-requirements-management-operations.md: MG-071 Original exceptions are narrowly ephemeral

Category: OwningRule. TSV rows: 97, 98, 99.

Own both output forms original reference versus authoritative detached metadata, original-release ownership and safe handling by multiple observers.

### D035 — docs/specs/10-provider-model-and-extensibility.md: PR-024 Provider certification suite

Category: AcceptanceReference. TSV rows: 103.

Keep provider certification semantics; issue #16 additionally requires actual stored failure-byte parity, not projection-only or live-exception persistence.

### D036 — docs/specs/10-provider-model-and-extensibility.md: PR-040 Exact role-based host integration

Category: CrossCapabilityDependency. TSV rows: 104.

Keep null-default retention and mandatory abort settlement without any new option, role-registration overload or fingerprint input.

### D037 — docs/specs/10-provider-model-and-extensibility.md: PR-041 Ephemeral lifetime service and application disposal owner

Category: CrossCapabilityDependency. TSV rows: 105.

Keep the actual provider/root owner and all-hosted-stop prerequisite; per-instance success never grants shared dependency disposal.

### D038 — docs/specs/10-provider-model-and-extensibility.md: PR-042 Issue #16 contract ownership (implementation pending)

Category: OwningRule. TSV rows: 106, 107, 108.

Own the exact two members/package/namespace, local durable rejection, no new provider/friend and stored-byte witness; stage normative declarations before executable baselines.

### D039 — docs/specs/11-non-functional-requirements.md: NF-031 Capacity controls

Category: CrossCapabilityDependency. TSV rows: 109.

Keep eligible terminal data distinct from active/pinned/physical capacity and caller-held values. The new receipt is not a byte/RSS or retention guarantee.

### D040 — docs/specs/11-non-functional-requirements.md: NF-032 Shutdown budgets preserve real ownership

Category: CrossCapabilityDependency. TSV rows: 110.

Keep observation budgets from releasing real physical or DI ownership; cached proof is passive rather than forced shutdown.

### D041 — docs/specs/11-non-functional-requirements.md: NF-035 Original-failure and settlement qualification (implementation pending)

Category: OwningRule. TSV rows: 111, 112, 113, 114.

Own future stored-byte/provider, barrier/retention/stress/coverage and native JIT-name parity evidence with no current source/support credit.

### D042 — docs/specs/12-acceptance-criteria.md: Core runtime (AC-0xx)

Category: AcceptanceReference. TSV rows: 115, 116, 117.

Keep logical typed output/DAG waits and terminal failure outcomes; AC-619–638 add separate physical/original witnesses rather than changing those waits.

### D043 — docs/specs/12-acceptance-criteria.md: Deferred saga scenario reservations (AC-4xx)

Category: AcceptanceReference. TSV rows: 118.

Keep the explicitly deferred saga failure/compensation reservation. Its English exception occurrence is not a current original-reference or settlement API promise.

### D044 — docs/specs/12-acceptance-criteria.md: Management & operations (AC-5xx)

Category: AcceptanceReference. TSV rows: 119, 120, 121, 122.

Keep existing retention, provisional cleanup, pins and exact age/count tests; the new receipt cannot weaken their physical ownership assertions.

### D045 — docs/specs/12-acceptance-criteria.md: Ephemeral host shutdown acceptance (source candidate under review)

Category: AcceptanceReference. TSV rows: 123, 124, 125, 126.

Preserve host drain, faults and no post-Completed work. AC-552 explicitly allows only an issued cached receipt in addition to detached/pure reads.

### D046 — docs/specs/12-acceptance-criteria.md: Native publishing assessment (AC-555–562)

Category: AcceptanceReference. TSV rows: 127.

Preserve the original profile-specific assessment scenarios as historical evidence, with no support credit for pending issue #16 source.

### D047 — docs/specs/12-acceptance-criteria.md: Ephemeral native support (AC-563–574; source-qualified, release pending)

Category: AcceptanceReference. TSV rows: 128.

Keep all source-qualified NS/AOT subcases intact; the future source must add original/type/settlement controls rather than replace or relabel them.

### D048 — docs/specs/12-acceptance-criteria.md: Issue #16 original failure and settlement (contract approved; implementation pending)

Category: AcceptanceReference. TSV rows: 129, 130, 131, 132, 133, 134.

Map every FS-01–20 row to unique AC-619–638 after the current maximum 618, including stored durable-byte and native type-name controls. All remain future source assertions.

### D049 — docs/specs/13-phasing-and-open-questions.md: 13.2 Open questions to resolve during implementation

Category: CrossCapabilityDependency. TSV rows: 135.

Keep the notification-driven wait requirement and earlier implementation questions; issue #16 does not change output waiting into physical completion.

### D050 — docs/specs/13-phasing-and-open-questions.md: Deferred capabilities

Category: CrossCapabilityDependency. TSV rows: 136.

Preserve deferred destructive/bulk commands and existing retention boundaries; a passive receipt adds no deferred management command.

### D051 — docs/specs/13-phasing-and-open-questions.md: Issue #13 native support transition (2026-10-24)

Category: AcceptanceReference. TSV rows: 137.

Preserve the earlier converter/declaration/source-baseline amendment history and exact source checkpoints. Its source approval does not approve the new failure/receipt members.

### D052 — docs/specs/13-phasing-and-open-questions.md: Issue #16 contract-to-source status (2026-11-01)

Category: OwningRule. TSV rows: 138, 139, 140.

Record the real contract chain and uncommitted canonical preparation, with pending members/source/native/publication gates and no preview.3 API claim.

### D053 — docs/specs/15-requirements-observability-otel.md: OB-026 Ephemeral abort and retention observations

Category: CrossCapabilityDependency. TSV rows: 141, 142, 143.

Keep internal abort/retention origin and protected physical counts. The original object and passive observer do not become telemetry ownership or operator commands.

### D054 — docs/specs/15-requirements-observability-otel.md: OB-027 Ephemeral host shutdown observations

Category: CrossCapabilityDependency. TSV rows: 144.

Keep no new diagnostics after Completed; reading a completed receipt emits none and cannot clear a shutdown accounting fault.

### D055 — docs/specs/15-requirements-observability-otel.md: OB-082 Original exception references are not telemetry payloads

Category: OwningRule. TSV rows: 145, 146, 147, 148, 149, 150, 151.

Own WF-STEP-UNHANDLED and Code identity, detached type/message rules, no live graph/stack/Data persistence and no late-loser or receipt diagnostic side effects.

### D056 — docs/specs/17-public-authoring-contract.cs: (preamble)

Category: OwningRule. TSV rows: 152, 153, 154, 155, 156, 157.

Add self-contained member fragments for existing types only. Internal throwing constructors grant no new public API; the new getter/base ValueTask method are pending while existing full shapes remain in doc 17.

### D057 — docs/specs/17-selected-mode-capability-matrix.md: 17.2.1 Strong values and execution context

Category: OwningRule. TSV rows: 158.

Keep the existing OrcaCoreException inner-capable base, without adding a new public exception or constructor to carry originals.

### D058 — docs/specs/17-selected-mode-capability-matrix.md: 17.2.4 Root-only `Parallel` and bounded root `ForEach`

Category: OwningRule. TSV rows: 159, 160, 161, 162, 163, 164, 165, 166, 167, 168, 169, 170, 171, 172, 173.

Show the new nullable getter alongside already-approved provenance; keep runtime construction and detached branch/item outcomes, with no live original field.

### D059 — docs/specs/17-selected-mode-capability-matrix.md: Independently source-approved DAG runtime-view boundary (Tasks 2.1–2.4)

Category: CrossCapabilityDependency. TSV rows: 174, 175.

Keep the already-approved detached DAG runtime-view failure fields and exact friend boundary; no original object or new DAG API is added.

### D060 — docs/specs/17-selected-mode-capability-matrix.md: 17.3 Shared compiler, fingerprint, and diagnostic contract

Category: OwningRule. TSV rows: 176, 177, 178, 179.

Keep WF-STEP-UNHANDLED and stable Code identity; diagnostic ExceptionType is not an event/definition/failure matching key or fingerprint resolver.

### D061 — docs/specs/17-selected-mode-capability-matrix.md: 17.4 Concurrency and resource lifetimes

Category: CrossCapabilityDependency. TSV rows: 180, 181.

Keep host retention/capacity/physical settlement options and source-independent clocks; no new configuration or early physical release is introduced.

### D062 — docs/specs/17-selected-mode-capability-matrix.md: 17.7 Registration, continuation, and application projections

Category: OwningRule. TSV rows: 182, 183, 184, 185, 186, 187, 188, 189, 190, 191, 192, 193, 194, 195, 196, 211.

Add only the base settlement method, preserve typed output helpers/DTOs, and narrow retired-operation rejection to ordinary calls plus the passive old receipt.

### D063 — docs/specs/17-selected-mode-capability-matrix.md: Ephemeral shutdown operation and error boundary (MG-066–069)

Category: OwningRule. TSV rows: 197, 198, 199, 200, 201.

Add one implementation-pending passive row/Completed allowance; preserve every live operation error, original-ID precedence and host DI ownership.

### D064 — docs/specs/17-selected-mode-capability-matrix.md: Issue #16 exact application amendment (contract approved; implementation pending)

Category: OwningRule. TSV rows: 202, 203, 204, 205, 206, 207, 208, 209, 210.

Bind exact member ownership, strict wire compatibility, original/detached separation, full mode/lifecycle receipt table and six-cell pending qualification.

### D065 — docs/specs/README.md: Document map

Category: CrossCapabilityDependency. TSV rows: 212.

Keep the normative declaration companion in the document map; task 2.1 updates that companion without treating it as product or executable API evidence.

### D066 — openspec/changes/expose-ephemeral-failures-and-settlement/specs/developer-facing-surface/spec.md: Requirement: Completion waits are notification-driven and race-free

Category: OwningRule. TSV rows: 213, 214, 215, 216, 217, 218, 230.

Keep output/DAG waits logical, notification-driven and caller-cancellation-local. The added physical receipt is separate; normative declarations precede executable source baselines.

### D067 — openspec/changes/expose-ephemeral-failures-and-settlement/specs/developer-facing-surface/spec.md: Requirement: Ephemeral settlement observation is a passive handle operation

Category: OwningRule. TSV rows: 219, 220, 221, 222, 223, 224, 225, 226, 227, 228, 229.

Own exactly the new base ValueTask method and nullable type getter. Preserve inheritance, token-first durable rejection and local receipts after retirement/disposal.

### D068 — openspec/changes/expose-ephemeral-failures-and-settlement/specs/management-and-querying/spec.md: Requirement: Management API is scope-oriented and fluent

Category: OwningRule. TSV rows: 231, 232, 233, 234, 235.

Extend the exact instance allowlist with passive settlement only; no bulk/global lookup, new helper, provider authority or replacement-ID targeting.

### D069 — openspec/changes/expose-ephemeral-failures-and-settlement/specs/management-and-querying/spec.md: Requirement: Typed committed root state and output are queryable

Category: OwningRule. TSV rows: 236, 237, 238, 239, 240, 241, 242, 243, 244, 245, 246, 247.

Keep codec-detached root state/output and local wait cancellation; permit the exact original only in the separate ephemeral wrapper, never a snapshot/value or durable result.

### D070 — openspec/changes/expose-ephemeral-failures-and-settlement/specs/management-and-querying/spec.md: Requirement: Retired ephemeral handles have deterministic outcomes

Category: OwningRule. TSV rows: 248, 249, 250, 251, 252, 253, 254, 255, 256, 257.

Narrow blanket runtime not-found to ordinary operations. Only an issued receipt survives; output captures original plus detached failure before its existing pin releases.

### D071 — openspec/changes/expose-ephemeral-failures-and-settlement/specs/management-and-querying/spec.md: Requirement: Ephemeral host shutdown closes admission atomically

Category: OwningRule. TSV rows: 258, 259, 260, 261, 262, 263, 264.

Preserve Closed drain and acceptance election at Stopping. Carve out only passive issued-handle settlement, without reopening query/output/command admission.

### D072 — openspec/changes/expose-ephemeral-failures-and-settlement/specs/management-and-querying/spec.md: Requirement: Completed ephemeral host shutdown proves host-wide quiescence

Category: OwningRule. TSV rows: 265, 266, 267, 268, 269, 270, 271, 272, 273, 274, 275, 276, 277.

Add only cached receipt observation after Completed/correct disposal. Preserve stable zero, sealed producers, root dependency ownership and ordinary live-operation rejection.

### D073 — openspec/changes/expose-ephemeral-failures-and-settlement/specs/management-and-querying/spec.md: Requirement: Ephemeral physical settlement is independently observable

Category: OwningRule. TSV rows: 278, 279, 280, 281, 282, 283, 284, 285, 286, 287, 288, 289, 290, 291.

Require accepted terminal final handoffs and a monotonic seal, local cancellation, no pin/host owner/tombstone, and a no-handle resource-owner fallback.

### D074 — openspec/changes/expose-ephemeral-failures-and-settlement/specs/quality-and-verification/spec.md: Requirement: Ephemeral original failures and settlement are behavior verified

Category: OwningRule. TSV rows: 292, 293, 294, 295, 296, 297, 298, 299, 300.

Require identity/late-loser/retirement controls, actual durable stored-byte parity and six native type-name witnesses; these are future source obligations, not prose execution credit.

### D075 — openspec/changes/expose-ephemeral-failures-and-settlement/specs/structured-fiber-execution/spec.md: Requirement: Join policies define one scope outcome

Category: OwningRule. TSV rows: 301, 302, 303, 304, 305, 306, 307.

Keep sibling completion and authored order; a single original propagates outside records, true aggregate stays detached, recovery releases obsolete associations, and durable produced bytes stay null-type.

### D076 — openspec/changes/expose-ephemeral-failures-and-settlement/specs/workflow-contracts/spec.md: Requirement: Workflow success projections and output waits are typed

Category: OwningRule. TSV rows: 308, 309, 310, 311, 312, 313, 314, 315, 316, 317, 318, 319, 320, 321, 322.

Keep typed projection and subscribe/recheck. Elect the exact final single error by real attempt identity; aggregates and durable paths retain detached behavior.

### D077 — openspec/changes/expose-ephemeral-failures-and-settlement/specs/workflow-contracts/spec.md: Requirement: Workflow failures carry authored and occurrence provenance

Category: OwningRule. TSV rows: 323, 324, 325, 326, 327, 328, 329, 330, 331, 332, 333, 334, 335, 336, 337, 338.

Keep location/occurrence/cause ordering and add opaque nullable type metadata. Own strict legacy/v2 forms, diagnostic-only Code distinction and no CLR resolution.

### D078 — openspec/specs/developer-facing-surface/spec.md: Requirement: Completion waits are notification-driven and race-free

Category: OwningRule. TSV rows: 339, 340, 341, 342, 343, 344.

Keep output/DAG waits logical, notification-driven and caller-cancellation-local. The added physical receipt is separate; normative declarations precede executable source baselines.

### D079 — openspec/specs/developer-facing-surface/spec.md: Requirement: Public failures carry stable codes

Category: CrossCapabilityDependency. TSV rows: 345, 346.

Keep nonblank fixed codes and closed failure data. Captured CLR type names remain diagnostic text, never the matching key.

### D080 — openspec/specs/developer-facing-surface/spec.md: Requirement: Ephemeral terminal retention policy is explicit and host-owned

Category: CrossCapabilityDependency. TSV rows: 347, 348.

Keep null-default retention, all option validation/fingerprints and mandatory provisional-abort cleanup. No new setting or registration option is introduced.

### D081 — openspec/specs/developer-facing-surface/spec.md: Requirement: Ephemeral host shutdown has one supported observation contract

Category: CrossCapabilityDependency. TSV rows: 349, 350.

Keep the single host stop API, result and budget semantics. A per-instance receipt is neither a new hosted hook nor a replacement for the host join.

### D082 — openspec/specs/developer-facing-surface/spec.md: Requirement: Ephemeral host shutdown preserves dependency ownership

Category: CrossCapabilityDependency. TSV rows: 351, 352.

Keep the true provider/scope alive until host Completed and all hosted stops. One instance receipt cannot authorize shared/root disposal.

### D083 — openspec/specs/developer-facing-surface/spec.md: Requirement: Native authoring bootstrap has a static ownership recipe

Category: CrossCapabilityDependency. TSV rows: 353.

Keep the existing static metadata/bootstrap ownership and post-Completed prohibition. New receipt/type paths require new qualification rather than inheriting a blanket badge.

### D084 — openspec/specs/developer-facing-surface/spec.md: Requirement: Ephemeral settlement observation is a passive handle operation

Category: OwningRule. TSV rows: 354, 355, 356, 357, 358, 359, 360, 361, 362, 363, 364.

Own exactly the new base ValueTask method and nullable type getter. Preserve inheritance, token-first durable rejection and local receipts after retirement/disposal.

### D085 — openspec/specs/event-routing-and-waits/spec.md: Requirement: Waits match by declared event identity and correlation

Category: CrossCapabilityDependency. TSV rows: 365.

Keep declared event identity/correlation; diagnostic exception types cannot become event matching keys or a new public ingress capability.

### D086 — openspec/specs/management-and-querying/spec.md: Requirement: Management API is scope-oriented and fluent

Category: OwningRule. TSV rows: 366, 367, 368, 369, 370.

Extend the exact instance allowlist with passive settlement only; no bulk/global lookup, new helper, provider authority or replacement-ID targeting.

### D087 — openspec/specs/management-and-querying/spec.md: Requirement: Typed committed root state and output are queryable

Category: OwningRule. TSV rows: 371, 372, 373, 374, 375, 376, 377, 378, 379, 380, 381, 382, 444.

Keep codec-detached root state/output and local wait cancellation; permit the exact original only in the separate ephemeral wrapper, never a snapshot/value or durable result.

### D088 — openspec/specs/management-and-querying/spec.md: Requirement: Workflow, wait, and step timeout facts remain distinct

Category: CrossCapabilityDependency. TSV rows: 383.

Keep timeout metadata distinct from physical settlement and lease quarantine. No new attempt fields or false external-stop claim are added to snapshots.

### D089 — openspec/specs/management-and-querying/spec.md: Requirement: Ephemeral terminal retention requires physical settlement

Category: CrossCapabilityDependency. TSV rows: 384, 385, 386, 387, 388.

Keep protection for active/noncooperative and provisional work in both modes. A public receipt cannot infer return from timeout or an unpublished failed start.

### D090 — openspec/specs/management-and-querying/spec.md: Requirement: Ephemeral terminal retention retires one complete ownership unit

Category: CrossCapabilityDependency. TSV rows: 389, 390, 391, 392, 393, 394, 395.

Keep age-first/ordinal-count removal and eager provisional cleanup. A small receipt is not retained payload, pin, key binding or physical capacity.

### D091 — openspec/specs/management-and-querying/spec.md: Requirement: Retired ephemeral handles have deterministic outcomes

Category: OwningRule. TSV rows: 396, 397, 398, 399, 400, 401, 402, 403, 404, 405, 445, 446.

Narrow blanket runtime not-found to ordinary operations. Only an issued receipt survives; output captures original plus detached failure before its existing pin releases.

### D092 — openspec/specs/management-and-querying/spec.md: Requirement: Ephemeral start idempotency expires with atomic retirement

Category: CrossCapabilityDependency. TSV rows: 406.

Keep actual-retirement key expiry and fresh replacement IDs. A receipt addresses its original identity only and cannot provide effect-safe retry authority.

### D093 — openspec/specs/management-and-querying/spec.md: Requirement: Ephemeral host shutdown closes admission atomically

Category: OwningRule. TSV rows: 407, 408, 409, 410, 411, 412, 413.

Preserve Closed drain and acceptance election at Stopping. Carve out only passive issued-handle settlement, without reopening query/output/command admission.

### D094 — openspec/specs/management-and-querying/spec.md: Requirement: Completed ephemeral host shutdown proves host-wide quiescence

Category: OwningRule. TSV rows: 414, 415, 416, 417, 418, 419, 420, 421, 422, 423, 424, 425, 426.

Add only cached receipt observation after Completed/correct disposal. Preserve stable zero, sealed producers, root dependency ownership and ordinary live-operation rejection.

### D095 — openspec/specs/management-and-querying/spec.md: Requirement: Incomplete ephemeral host shutdown preserves truthful ownership

Category: CrossCapabilityDependency. TSV rows: 427, 428.

Keep incomplete/faulted accounting and real cancellation-handler ownership. A passive per-instance observer cannot clear a host fault or manufacture a completed host join.

### D096 — openspec/specs/management-and-querying/spec.md: Requirement: Repeated ephemeral host shutdown observes one irreversible stop

Category: CrossCapabilityDependency. TSV rows: 429.

Keep repeat-observer race/precedence and cached host result semantics. The receipt neither restarts producers nor becomes a new shutdown obligation.

### D097 — openspec/specs/management-and-querying/spec.md: Requirement: Ephemeral physical settlement is independently observable

Category: OwningRule. TSV rows: 430, 431, 432, 433, 434, 435, 436, 437, 438, 439, 440, 441, 442, 443.

Require accepted terminal final handoffs and a monotonic seal, local cancellation, no pin/host owner/tombstone, and a no-handle resource-owner fallback.

### D098 — openspec/specs/quality-and-verification/spec.md: Requirement: Completion waits are notification-driven and race-free

Category: CrossCapabilityDependency. TSV rows: 447.

Keep output/DAG waits logical, notification-driven and caller-cancellation-local. The added physical receipt is separate; normative declarations precede executable source baselines.

### D099 — openspec/specs/quality-and-verification/spec.md: Requirement: AOT and trimming findings remain explicit and non-promissory

Category: CrossCapabilityDependency. TSV rows: 448.

Keep the earlier unsupported assessment and diagnostic recording as dated research. It grants no support to the pending exception/receipt implementation.

### D100 — openspec/specs/quality-and-verification/spec.md: Requirement: Published compatibility evidence executes the selected ephemeral scenarios

Category: CrossCapabilityDependency. TSV rows: 449, 450, 451.

Keep all original public consumer journeys and host ownership barriers. Current pending API docs cannot substitute for native artifact execution.

### D101 — openspec/specs/quality-and-verification/spec.md: Requirement: Ephemeral original failures and settlement are behavior verified

Category: OwningRule. TSV rows: 452, 453, 454, 455, 456, 457, 458, 459, 460.

Require identity/late-loser/retirement controls, actual durable stored-byte parity and six native type-name witnesses; these are future source obligations, not prose execution credit.

### D102 — openspec/specs/structured-fiber-execution/spec.md: Requirement: Join policies define one scope outcome

Category: OwningRule. TSV rows: 461, 462, 463, 464, 465.

Keep sibling completion and authored order; a single original propagates outside records, true aggregate stays detached, recovery releases obsolete associations, and durable produced bytes stay null-type.

### D103 — openspec/specs/workflow-contracts/spec.md: Requirement: Branch and merge contracts are type checked

Category: CrossCapabilityDependency. TSV rows: 466.

Keep typed ordered results and detached failure values at joins; the original object travels outside the merge input, never in a BranchOutcome payload.

### D104 — openspec/specs/workflow-contracts/spec.md: Requirement: Workflow success projections and output waits are typed

Category: OwningRule. TSV rows: 467, 468, 469, 470, 471, 472, 473, 474, 475, 476, 477, 478, 479, 480, 481.

Keep typed projection and subscribe/recheck. Elect the exact final single error by real attempt identity; aggregates and durable paths retain detached behavior.

### D105 — openspec/specs/workflow-contracts/spec.md: Requirement: Public failure exceptions carry stable codes

Category: CrossCapabilityDependency. TSV rows: 482, 483, 484.

Keep OrcaCoreException Code as stable protocol identity and WorkflowFailure detached. The separate ephemeral wrapper reference does not persist stack/inner graphs.

### D106 — openspec/specs/workflow-contracts/spec.md: Requirement: Workflow failures carry authored and occurrence provenance

Category: OwningRule. TSV rows: 485, 486, 487, 488, 489, 490, 491, 492, 493, 494, 495, 496, 497, 498, 499, 500.

Keep location/occurrence/cause ordering and add opaque nullable type metadata. Own strict legacy/v2 forms, diagnostic-only Code distinction and no CLR resolution.

### D107 — openspec/specs/workflow-contracts/spec.md: Requirement: Event contracts have stable explicit wire identity

Category: CrossCapabilityDependency. TSV rows: 501, 502.

Keep explicit event name/version identity independent of CLR names. ExceptionType is unrelated diagnostic text and cannot change event routing or schema identity.
