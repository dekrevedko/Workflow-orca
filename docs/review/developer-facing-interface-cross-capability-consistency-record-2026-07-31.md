# Cross-capability consistency record — reshape-developer-facing-interfaces

**Date:** 2026-07-31
**Baseline HEAD:** `50254d0` ("Checkpoint developer-facing interface through Section 7")
**Trigger:** review question — the public-API reshape was applied, but downstream capability specs
were never checked for consistency against it.
**Status:** spec harmonization **proposed** as change `harmonize-downstream-capability-specs`,
awaiting independent approval before canonical synchronization; **three code findings remain open**
and are not addressed by this record.

> **Process note.** The harmonization was first applied by editing `openspec/specs/` directly. That
> bypassed the OpenSpec workflow: canonical specs are derived artifacts, written by
> `openspec archive` from an approved change's deltas, not hand-edited. The direct edits were
> reverted and reconstructed as a reviewable change. Two lessons worth keeping: canonical content
> with no originating change breaks provenance and can be overwritten by a later archive run; and
> `openspec validate --strict` passes on structure alone, so **green validation is not evidence of
> workflow compliance** — it cannot detect canonical content that no change describes. Note the
> defect class is the same one this record reports: content reaching canonical outside the pipeline.

Ambiguity rule used throughout: **`reshape-developer-facing-interfaces` is authoritative.** Where a
downstream spec and the reshape contract disagreed, the reshape contract won and the downstream
spec was corrected to match.

---

## 1. Root cause

Task `10.14` is scoped to *"synchronize every approved **delta** into canonical OpenSpec specs."*
The task is delta-driven, so a capability with **no delta directory is invisible to it**. There is
no reverse check asking, for every canonical spec, whether it still holds under the new contract.

Result: 11 of 14 canonical specs were synchronized at `50254d0`. Three were never opened by the
reshape and remained at the pre-reshape baseline `ba2478e` ("docs: track OpenSpec workspace"):

| Capability | Had delta | Last touched before this record |
|---|---|---|
| `event-routing-and-waits` | no | `ba2478e` |
| `durable-persistence-and-outbox` | no | `ba2478e` |
| `event-driven-prototype` | no | `ba2478e` |

`runtime-resource-governance` also has no delta in this change but was hand-synchronized at
`50254d0` and was already consistent. It is the reason the gap was not uniform — and the reason it
went unnoticed.

**Recommendation:** add a reverse-direction step to the canonical-sync task — enumerate every
canonical spec, not just every delta — before the next change reaches its sync gate.

---

## 2. Spec changes proposed

Delivered as deltas under `openspec/changes/harmonize-downstream-capability-specs/`. Canonical specs
are **unchanged** and stay at their baseline until task `1.4` approval and `2.1` synchronization.
Content is in the delta files; this section records only *what changed and why*.

### 2.1 `event-routing-and-waits` — 4 contradictions

| Was | Now | Authority |
|---|---|---|
| Definition-scoped fanout required as one of three routing modes | Two routes / four overloads; fanout deferred with registry entry | `durable-runtime` §"Event routing outcomes are typed"; `docs/specs` EV-010 |
| Multi-match resolved by lowest `WaitSequence`, `FiberId` tiebreak | Ambiguity rejected at registration via `AmbiguousWaitRegistrationException`; no match-time tiebreaker exists | `durable-runtime`; the ambiguous state is unreachable by construction |
| Pre-registration event buffering required | No buffering; `NoActiveWait` + non-consumed `EventId` + redelivery | Closed result set `Accepted`/`Duplicate`/`NoActiveWait`/`InstanceTerminal`/`EventConflict` admits no "buffered" value |
| Wait cleanup keyed on `WhenFirst` loser selection | Cleanup on cancellation, failure, deadline, termination, abandonment | `WhenFirst` absent in v1 |

`WaitSequence` retained as a persisted registration ordinal for ordering/recovery/audit, explicitly
**not** a match-time selector, and explicitly not application-visible.

### 2.2 `durable-persistence-and-outbox` — 2 contradictions + 1 gap

| Was | Now | Authority |
|---|---|---|
| Payloads serialized through replaceable payload-envelope + schema-resolution abstractions | Non-replaceable certified `orcacore-json-v1`; no serializer hook, envelope abstraction, or schema-resolution SPI | `developer-facing-surface` §codec; `quality-and-verification` serializer-hook guard |
| Retention/purge required without tier | Operator/provider tier only; absent from the v1 application surface | `management-and-querying`; `durable-runtime` |
| *(gap)* `IWorkflowStore` / `IMessageDispatcher` carried no tier or owner | Tiered to `OrcaCore.Provider.Abstractions`; `IDurableResourceGovernanceStore` named as the separate append contract | `repository-foundation` namespace/assembly table |

### 2.3 `event-driven-prototype` — recorded as out-of-scope

The capability described a runtime with "its own engine, definitions, persistence model, and project
boundary." No such project exists under `src/`, and `repository-foundation` declares the
first-release project list **exhaustive** without it. It also referenced `WaitLong`, a removed
concept required to carry no alias or tombstone.

Rewritten on the `saga-orchestration` pattern: explicitly outside the first release, no v1
obligation, future work requires a separate reviewed capability amendment.

**Open question for the reviewer:** deletion of the capability was the alternative to this rewrite.
The rewrite is reversible and preserves planning history; deletion is cleaner. Not taken unilaterally.

### 2.4 `state-driven-runtime` — residual fix (found in an *already-synced* spec)

Line 41 still cited `WaitLong` as a durable-only feature that ephemeral mode rejects. Replaced with
wait residency as runtime/hosting policy plus the accurate durable-only set (root `ContinueAsNew`,
scoped `AcquireResources`), and a matching negative scenario.

This one matters beyond its size: it shows the delta-driven sync also left residue **inside** the
specs it did process. A term-level sweep across all 14 specs is warranted, not just the three.

### 2.5 Delivery

```
openspec/changes/harmonize-downstream-capability-specs/
├── proposal.md
├── tasks.md
└── specs/
    ├── durable-persistence-and-outbox/spec.md   (2 ADDED, 1 MODIFIED, 2 REMOVED)
    ├── event-driven-prototype/spec.md           (2 ADDED, 4 REMOVED)
    ├── event-routing-and-waits/spec.md          (3 ADDED, 5 MODIFIED, 2 REMOVED)
    └── state-driven-runtime/spec.md             (1 MODIFIED)
```

All 16 `MODIFIED`/`REMOVED` headings were verified to match their canonical baselines verbatim —
the exact defect class that produced the P1 finding in the 2026-07-14 post-fiber review. Every
removal carries `**Reason**` and `**Migration**`.

Deltas carry requirements only, so the three `Purpose` revisions are not expressible as deltas and
are scheduled as an explicit hand-application step in task `2.2`.

The `state-driven-runtime` delta touches only "Ephemeral mode has explicit limitations" and does not
overlap the two requirements the reshape's own delta for that capability modifies.

Canonical `openspec/specs/` is untouched by this change. The two files dirty there
(`quality-and-verification`, and the `Yield` → "Quantum rotation is runtime-owned" hunk in
`state-driven-runtime`) predate this session and belong to the reshape's in-progress `10.14` work.

---

## 3. Open code findings

Not addressed. Ranked most severe first, per `docs/review/README.md` §5.

### [P1] `ActiveWaitSnapshot` exposes forbidden runtime routing identities — `src/OrcaCore.Abstractions/Instances/ActiveWaitSnapshot.cs:48`

- **Requirement:** `management-and-querying` — `WorkflowInstanceSnapshot.ActiveWaits` "SHALL NOT
  expose `FiberId`, `ScopeId`, wait sequence, raw park reason, or obligation ownership."
- **Evidence:**
  ```csharp
  public FiberId? FiberId { get; init; }        // :48
  public ScopeId? ScopeId { get; init; }        // :53
  public long WaitSequence { get; init; }       // :58
  public required string Mode { get; init; }    // :43  — "wait residency mode name"
  ```
- **Failure scenario:** this record is public in `OrcaCore.Abstractions` — the primary `OrcaCore`
  application package. Any v1 consumer can read `FiberId`/`ScopeId`/`WaitSequence` and build logic
  on internal routing identity, which the tier separation exists to prevent. `Mode` additionally
  re-exposes the wait-residency distinction the reshape removed with `WaitLong`.
- **Recommendation:** reduce to the specified projection — opaque `WaitId`, immutable
  `AuthoredLocation`, `EventName`, `CorrelationId`, registration time, optional deadline. Drop
  `FiberId`, `ScopeId`, `WaitSequence`, `Mode`; re-evaluate `BranchId` and `Status` (neither is in
  the allowed list). `AuthoredLocation` and the deadline are currently **missing** and must be added.
- **Confidence:** CONFIRMED — read the full type.
- **Attribution:** open task `7.17` ("public legacy instance projections/statuses").

### [P1] Public definition-scoped event fanout still ships — `src/OrcaCore.Engine.Durable/Execution/DurableWorkflowRuntime.cs:354`

- **Requirement:** `docs/specs` EV-010 — definition-targeted fanout "has no v1 route, alias,
  command, or placeholder"; `durable-runtime` — "Definition-targeted fanout and an ambiguous-match
  delivery result SHALL be absent."
- **Evidence:**
  ```csharp
  public async Task<IReadOnlyList<DurableEventDeliveryResult>> RaiseEventToDefinitionAsync<TPayload>(
      DefinitionId definitionId, string eventName, CorrelationId correlationId, TPayload payload, ...)
  ```
  on `public sealed class DurableWorkflowRuntime` (`:19`), returning public record
  `DurableEventDeliveryResult` (`:455`). Doc comment: *"Delivers one logical event to every projected
  instance of a definition."*
- **Failure scenario:** `OrcaCore.Engine.Durable` is a shipping first-release package, so this is
  callable v1 API implementing a capability the contract states is absent. A consumer can take a
  dependency on fanout that v1 does not promise, does not test, and intends to redesign (a future
  amendment must define a committed target snapshot and per-target dedup).
- **Recommendation:** delete the method and its list-returning result type from the public surface.
  The replacement two-route contract already exists and is compliant (see §4).
- **Confidence:** CONFIRMED — traced accessibility and the projection query it issues.
- **Attribution:** **none — unattributed.** Task `4.10` ("Delete public … event fanout") is marked
  `[x]` complete and does not cover this. Task `7.6` correctly built the new `IWorkflowEventClient`;
  this legacy route survived alongside it. **This is the item to raise before the Section 7 exit
  review signs off**, because a completed task overstates the deletion.
- **Secondary defect:** the doc comment justifies the method with "(EV-010)", but EV-010 was
  rewritten to mean *two* routing modes. Stale citations pointing into `docs/specs` may exist
  elsewhere — see §6.

### [P2] Public replaceable schema-resolution SPI on compiler options — `src/OrcaCore.Core/Compilation/DefinitionCompilerOptions.cs:35`

- **Requirement:** `developer-facing-surface` — "no ordinary hosting hook SHALL replace the codec";
  `quality-and-verification` guards against adding a "serializer hook"; §2.2 above now names
  "schema-resolution SPI" as absent.
- **Evidence:**
  ```csharp
  public interface IWorkflowTypeSerializerRegistry
  {
      bool TryGetSchemaIdentity(Type type, out string schemaIdentity);
  }
  ```
  Settable through public `DefinitionCompilerOptions`; default implementation gates on
  `FixedWorkflowValueCodec.IsSupportedDeclaredType`.
- **Failure scenario:** a host substitutes an implementation that returns `true` for a type the
  fixed codec rejects, defeating the pre-commit rejection of unsupported/cyclic/polymorphic shapes
  and admitting a definition that cannot round-trip.
- **Recommendation:** internalize the interface and keep `DefaultWorkflowTypeSerializerRegistry`
  behavior. It declares schema identity rather than replacing serialization, so deletion is likely
  too strong — this is a judgment call and is flagged as such.
- **Confidence:** CONFIRMED for the public surface; **PLAUSIBLE** for the exploit path (not executed).
- **Attribution:** open task `7.17` ("obsolete hosting/codec hooks").

---

## 4. Checked and cleared — do not re-litigate

| Suspected | Verdict |
|---|---|
| `WhenFirst` in `OrcaCore.Core` (`StructuredBranchBuilders`, `SelectedWorkflowBuilder`, `CompiledPlanModels`, `DefinitionCompiler`, `ScopeReducer`, `ScopeMergeAdapter`) | **Clear.** `BranchBuilder<,>` is `internal sealed`; the other sites are internal compiled-plan/execution residue. Task `4.10`'s "delete *public* `WhenFirst`" holds. Internal execution-substrate residue is permitted. |
| Pre-registration event buffering in the ephemeral engine | **Clear.** Zero `Buffer` matches in `OrcaCore.Engine.Ephemeral`. Code already matches the no-buffering rule. |
| `IWorkflowEventClient` public event surface | **Clear and fully compliant** — exactly two route names, four overloads, closed five-value `EventDeliveryResult`, `AmbiguousWaitRegistrationException` present, no fanout member. |
| `WaitLong` in source | **Clear.** No occurrence in `src/`; one occurrence in `tests/OrcaCore.Core.Tests/Building/ModeFirstWorkflowBuilderTests.cs` (negative guard — expected). |
| External-job machinery | **Clear.** `DurableExternalJobCommandHandler`, `DurableExternalJobState`, `DurableExternalJobEventContext` are all `internal`. |
| `JsonWorkflowPayloadSerializer` | **Clear** — `internal`. |
| `YieldContinuationScheduler` | **Clear** — `internal`; the reshape permits internal cooperative turn scheduling. |
| Event-driven prototype project | **Clear** — absent from `src/`, consistent with the rewritten spec. |

---

## 5. `docs/specs/` cross-tree audit

`docs/review/README.md` makes both trees normative and judges code against both. `docs/specs/` was
audited on the same day, after the OpenSpec work.

### 5.1 Headline: the OpenSpec tree was the stale side

`docs/specs/` is substantially healthier than the OpenSpec capability specs. Every occurrence of
`WaitLong`, author `Yield`, `WhenFirst`, `RunExternalJob`, and `RunChild`/`RunChildren` is a negative
guard or a deferred-registry entry, not a live requirement. On all four semantics corrected in §2.1
and §2.2, `docs/specs` already held the approved answer:

| Semantic | `docs/specs` | OpenSpec (before this change) |
|---|---|---|
| Pre-registration buffering | `05` — "SHALL NOT buffer the envelope"; "no pending-event mailbox, pre-wait buffering guarantee" | required buffering |
| Definition-scoped fanout | `05` EV-010 — two modes; fanout "has no v1 route, alias, command, or placeholder" | required fanout as a third mode |
| Wait projection | `09` — `WaitId`, `AuthoredLocation`, `EventName`, `CorrelationId`, registration time, optional deadline | (agreed) |
| Fixed codec | `06`, `10`, `12` — `orcacore-json-v1` throughout | replaceable envelope + schema resolution |

The two trees disagreed, and the tree with no delta mechanism was the correct one. That inverts the
natural assumption and is worth carrying into the next review: **being downstream of the change
pipeline did not make OpenSpec more current — it made the gap invisible.**

Corollary for finding §3.1: `docs/specs/09` independently specifies the same wait projection as
OpenSpec `management-and-querying`, so `ActiveWaitSnapshot` violates **both** normative trees, not
one. That raises confidence in the finding rather than its severity.

### 5.2 What did not land

The 2026-07-28 amendment introduced two behavioral models. Both reached the OpenSpec specs and the
capability matrix. Neither reached the numbered requirement files:

- **Authoring-session lifecycle** (`Open`/`JoinPending`/`Frozen`, session-epoch handle validity,
  atomic root-terminal freeze). Present at OpenSpec `workflow-authoring`. Absent from `04`–`16`; the
  only trace anywhere in `docs/specs` is one `SFE-AUTH-LIFECYCLE-003` diagnostic row in the matrix.
  Implemented by reshape task `4.16` (complete).
- **`WorkflowFailure` occurrence provenance** (authored location + runtime-created
  `root`/`branch`/`item` occurrence, closed discriminator allowlist). Present at OpenSpec
  `workflow-contracts`. Absent from `04`–`16`. Implemented by reshape task `5.11` (complete).

All 137 `AC-` entries in `12-acceptance-criteria.md` contain **zero** references to authoring
session, `JoinPending`, `Frozen`, `FailureOccurrence`, failure provenance, or `AuthoredLocation`. So
two implemented, OpenSpec-normative models have no requirement ID and no acceptance criterion in the
tree that the review process uses to judge code.

One further defect: `18-semantic-appendix.md:163` asserts that "product source retains
`MaxActiveFibers` until task 5.13 lands." Task `5.13` is complete and `MaxActiveFibers` has zero
occurrences in `src/`. The line is factually wrong about current source state — and that file was
synchronized at `50254d0`, so residue survived the most recent sync here as well as in
`state-driven-runtime` (§2.4).

### 5.3 Root cause — the same shape as §1

The `x.0` gate tasks apply mapped `docs/specs` amendments **before** each source section begins; the
bulk application ran at `8c2dd71` (2026-07-19), which is why files dated 07-19 already contain
Section 7 content. The `AMENDMENT-2026-07-28` package was approved after every relevant gate had
closed. It enumerates its targets carefully — it amends the capability matrix and explicitly records
that it does **not** amend `17-public-authoring-contract.cs` (§5 of the amendment) — but the numbered
requirement files are not mentioned in either list. Task `10.14` then synchronized OpenSpec specs,
the matrix, the guide, and the appendix; its scope never included `04`–`16`.

So the omission is a silent scope gap, not a recorded decision. Both root causes are the same defect
shape: **a synchronization step that runs once, at a fixed point, and silently ignores anything
approved afterward or anything not named in its own inputs.**

Remediation is owned by `harmonize-downstream-capability-specs` tasks `6.1`–`6.6`. No `docs/specs`
file was edited — those edits belong behind a gate, and the tree is inside the frozen Section 7
target.

## 6. Validation evidence

```
openspec validate --changes --strict →   4 passed, 0 failed   (incl. harmonize-downstream-capability-specs)
openspec validate --specs   --strict →  14 passed, 0 failed   (canonical unchanged)
delta-heading match vs canonical     →  16 of 16 exact
```

No build or test run was performed; no source file was modified by this record.

**What this validation does not prove.** Strict validation checks structure, not provenance — it
cannot detect canonical content that no change describes, which is precisely the defect reported in
§1. It also does not confirm the deltas apply cleanly; that is verified at synchronization
(task `2.1`), and the heading-match check above is the available pre-approval proxy.

---

## 7. Outstanding — for whoever picks this up

1. **Approve `harmonize-downstream-capability-specs`** (its task `1.4`). Canonical synchronization
   and the `Purpose` hand-application are blocked until then.
2. **Section 7 freeze is intact.** Canonical specs and the reshape's own deltas, tasks, and manifest
   are unmodified, so the exit review can proceed. It will run against a baseline whose
   contradictions are documented here but not yet corrected — that is the accepted cost of keeping
   the freeze rather than folding this work into the reshape.
3. **`tasks.md` of the reshape was not edited.** Finding §3.2 (fanout) has no owning task; §3.1 and
   §3.3 sit under open task `7.17`. Either widen `7.17` to name the fanout route or add a task.
   Tracked here as `5.1`/`5.2` but deliberately not performed, since editing the reshape's task list
   would drift its frozen target.
4. **`docs/specs/` harmonization** (tasks `6.1`–`6.6`): add the authoring-session and failure-
   provenance requirements plus their acceptance criteria to `04` and `12`, correct the stale
   `MaxActiveFibers` line in `18`, and confirm the `17-public-authoring-contract.cs` exclusion still
   holds. See §5.
5. **Term-level sweep** across all 14 canonical specs (task `3.1`). §2.4 shows residue survived
   inside already-synced specs, so the sweep must not be limited to the four capabilities here.
6. **Decide `event-driven-prototype`'s fate** (task `3.2`) — retained as an out-of-scope record
   versus deleted outright.
7. **Empty capability directory** (task `3.3`):
   `openspec/changes/add-runtime-concurrency-limits/specs/state-driven-runtime/` contains no
   `spec.md`. Strict validation passes regardless, so this is either a dropped delta or stray.
8. **Close the gate that caused this** (tasks `4.1`/`4.2`): make canonical synchronization enumerate
   every canonical spec rather than only the change's own deltas, and stop treating strict
   validation as evidence of workflow compliance.
