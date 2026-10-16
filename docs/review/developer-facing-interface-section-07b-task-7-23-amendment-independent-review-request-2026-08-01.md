# Section 7B task 7.23 amendment — independent review request

**Date:** 2026-08-01  
**Requested verdict:** `APPROVE` or `REJECT`  
**Review scope:** planning amendment only; no task 7.24–7.34 implementation is authorized by this request

## 1. Gate under review

Reshape task `7.23` requires the exact event-contract, route, acceptance, start-or-deliver, fanout,
publish, dispatcher, and workflow-catalog amendment to be reconciled into both normative document
17 artifacts and independently approved before task `7.24` retargets guards.

The two missing OpenSpec deltas already exist, and the contradictory non-buffering/fanout ownership
has already been removed from `harmonize-downstream-capability-specs`. This target supplies the two
remaining planning changes:

1. reconcile `docs/specs/17-selected-mode-capability-matrix.md`; and
2. reconcile its compile-shaped companion `docs/specs/17-public-authoring-contract.cs`.

Task `7.23` remains unchecked pending this verdict. Tasks `7.24`–`7.34`, the Section 7 checkpoint,
and task `8.0` remain blocked.

## 2. Provenance and frozen target

- HEAD: `ac46d99543daf85c0fa3234272997ba40f47f96b`
- HEAD tree: `28f4033c4773ea7761afa905c9836fd25866f1c0`
- Prior independently verified post-harmonize-sync freeze:
  - 426 porcelain entries;
  - raw SHA-256 `0432097368b4c3c1a1ebe54a5a743d24b1ac806c625edbdc7b1b5ec39f7c8d73`;
  - 456 NUL-expanded normalized records;
  - normalized SHA-256 `9765189e0236dc878c28cc930cb82ad1f46b082f0047200d10169c20ced19fe8`.

The exact target manifest is:

- `docs/review/developer-facing-interface-section-07b-task-7-23-amendment-dirty-manifest-2026-08-01.txt`

Final self-inclusive anchors:

- porcelain entries: `430`
- raw ordered SHA-256: `db8d70860d6646f79e8c00f634a076131b390c626e1446a4f08b1fc8bdb49ef1`
- NUL-expanded normalized records: `460`
- normalized LF SHA-256: `fc290960af8b593645a1f50862d28c773e79de662ae46482326343bdf23baff0`
- capability directories: `16`
- capability-directory SHA-256:
  `5e8a9725979d702ff2f639fef587828958c92533139602ab13f1401d6df7ebd5`
- capability directories lacking `spec.md`: `0`

Use the exact Decision 6 pipelines in
`openspec/changes/harmonize-downstream-capability-specs/design.md`. Reproduce all anchors before
validation and again afterward. Reject drift.

## 3. Exact amendment artifacts

| Artifact | Lines | SHA-256 |
|---|---:|---|
| `docs/specs/17-selected-mode-capability-matrix.md` | 2722 | `1B5BC96B6AA1641CB96F88AF61666306EB1F9FE78412F064BA8257FE28EF1F47` |
| `docs/specs/17-public-authoring-contract.cs` | 1287 | `41F6472C2774363D2AB922C608922E787EC241333E1D1C0B76B0C6D529AB8EC3` |
| reshape `design.md` | 573 | `DA0D0068842D120068537B63C661FE844E3923EFC62EF57B83BB4A935513FD6E` |
| reshape `event-routing-and-waits` delta | 129 | `B56A88A05217437ACADAF04E1F6404AAC8225E9140053C2E94520BE7D10BBA2F` |
| reshape `durable-persistence-and-outbox` delta | 106 | `946ED0CE5572AD703A94E1EC660BD26ADD92D065E3E6303BA276F576418AD528` |

The matrix status explicitly labels this as a Section 7B amendment pending independent approval.

## 4. Required semantic review

Independently verify all of the following rather than trusting name presence:

1. `EventContractVersion` is positive and immutable, and payloadless/typed
   `WorkflowEventContract` descriptors use exact `EventName` plus version as identity.
2. Every existing structural wait location exposes exactly four descriptor-based overloads:
   payloadless/typed, each with and without timeout. No `EventName`-only wait remains.
3. Exactly eight durable sequential builder families — root, nested, branch, item, and four leased
   families — expose exactly two `Publish` overloads. Every ephemeral/completion/join/DAG surface
   exposes none.
4. `WorkflowEventRoute` is closed to direct instance, definition-scoped correlation,
   definition-fanout, and exact-definition `StartOrDeliver<TInput>` with a distinct workflow input
   and `StartIdempotencyKey`.
5. `WorkflowInboundEvent`/typed form carry contract, event/correlation/optional causation identity,
   UTC occurrence time, route, and fixed-codec-detached typed payload.
6. `IWorkflowEventIngress` has exactly payloadless/typed `AcceptAsync` overloads and returns the
   closed `Accepted`/`Duplicate`/`Rejected` result. The rejection union is exactly event conflict,
   direct-not-found, direct-terminal, start conflict, and fanout limit exceeded.
7. Identity comparison precedes target-state evaluation; only `Accepted` and `Duplicate` are
   acknowledgement-safe. There is no durable `NoActiveWait` outcome or caller redelivery loop.
8. Direct/correlation buffering, atomic event/wait races, cold activation, callback-only handoff,
   stable fanout membership, and start-or-deliver intent ownership agree with Decisions 23–25 and
   both reshape deltas.
9. `WorkflowOutboundEvent` contains the complete contract/event/correlation/causation/origin/time
   projection and descriptor-checked payload access without provider records.
10. `IWorkflowEventDispatcher.DispatchAsync` has the one exact signature and closed
    succeeded/retryable/permanent result. Internal continuations never reach it.
11. Ephemeral and durable definitions expose typed references; mode-specific engine builders expose
    only their two matching `AddWorkflow` overloads and atomic staged-batch semantics.
12. `MissingWorkflowEventDispatcher` participates in host compatibility, and definitions containing
    `Publish` cannot start on a definition-owning host without a dispatcher.
13. The deferred registry no longer lists durable `Publish` or definition fanout. Authored `Cancel`
    remains deferred. `IWorkflowEventClient`, `EventDeliveryStatus`, `EventDeliveryResult`,
    `DeliverToInstanceAsync`, and `DeliverByCorrelationAsync` are absent from both artifacts.
14. The matrix and companion agree exactly on namespace ownership, generic arity, overload count,
    placement, result closure, and return families. Flag every invented extra member or missing
    declaration.

## 5. Required mechanical checks

Re-run and record:

1. `openspec.cmd validate --all --strict` — expected 18 passed, 0 failed.
2. `git diff --check` — expected exit 0.
3. Cross-change `(capability, requirement heading)` ownership scan — expected 175 entries and zero
   duplicate owners.
4. Matrix/companion superseded-symbol scan — expected zero for all five old event-client symbols.
5. Companion placement scan — expected each of 12 wait-capable builders to have four waits; each of
   eight durable sequential builders to have two publishes; every ephemeral builder to have zero
   publishes.
6. C# syntax parse of the companion — unresolved document-17 reference types are expected in a
   standalone parse, but syntax/namespace/declaration errors are not.
7. `openspec.cmd list` — expected reshape remains 107/158 and task 7.23 remains open.
8. Confirm `src/`, `tests/`, and `samples/` are byte-identical to the prior 426-entry freeze.

## 6. Verdict rule

`APPROVE` only if the matrix and companion form one coherent exact pre-source contract and all
anchors reproduce without drift. Approval authorizes checking task `7.23` and beginning guard
retargeting at task `7.24` in a later implementation turn; it does not approve any product source,
Section 7 exit, checkpoint commit, or task `8.0`.

`REJECT` for any missing route/result/placement/catalog member, stale two-route/non-buffering
contract, cross-change ownership conflict, unverifiable manifest, product/test/sample drift, or
other release-blocking ambiguity. Write one new dated immutable verdict and edit no reviewed file.
