# Tasks

## 1. Freeze and review

- [x] 1.1 Confirm the three unsynchronized capabilities and the one residual term against `ba2478e`
      and `50254d0`, and record the exact conflicting clauses with their authoritative counterparts.
- [x] 1.2 Author `ADDED`/`MODIFIED`/`REMOVED` deltas whose requirement headings match the canonical
      baselines verbatim, with `**Reason**`/`**Migration**` on every removal.
- [x] 1.3 Strict-validate the change.
- [ ] 1.4 **REQUIRED:** obtain independent approval of this change against the unchanged canonical
      baseline. Synchronization SHALL NOT begin before sign-off without a gate-blocking finding.

## 2. Canonical synchronization

- [ ] 2.1 **POST-APPROVAL:** apply every approved delta to `openspec/specs/`, then strict-validate the
      synchronized result and record the exact sync diff.
- [ ] 2.2 Apply the `Purpose` revisions by hand during 2.1. OpenSpec deltas carry requirements only,
      so the following are not expressible as deltas and SHALL be applied at sync time:
      - `event-routing-and-waits`: state that the identities named in the capability belong to
        `OrcaCore.Runtime.Protocol` and provider records, and that the application surface observes
        only the closed delivery results and the projected wait fields.
      - `durable-persistence-and-outbox`: state that these contracts belong to the provider-authoring
        tier owned by `OrcaCore.Provider.Abstractions` and are not part of the application surface.
      - `event-driven-prototype`: state that the capability is retained for planning history only and
        is superseded by the accepted v1 contract.
- [ ] 2.3 Verify no canonical spec still asserts pre-registration buffering, definition-scoped fanout,
      match-time ambiguity tie-breaking, `WhenFirst` cleanup, replaceable serialization, or an
      untiered purge promise.

## 3. Corpus sweep

- [ ] 3.1 Sweep all fourteen canonical specs for removed and deferred vocabulary — `WaitLong`,
      authored `Yield`, `WhenFirst`, definition-targeted fanout, event buffering, author TTL,
      renewal, pause/resume/archive/purge, `MaxActiveFibers`, catch-all `AddOrcaCore` — and confirm
      every surviving occurrence is a negative guard rather than a positive requirement. Residue
      survived inside an already-synchronized spec, so the sweep SHALL NOT be limited to the
      capabilities this change modifies.
- [ ] 3.2 Decide whether `event-driven-prototype` is retained as an out-of-scope capability record or
      deleted outright. This change retains it; deletion remains open and is reversible either way.
- [ ] 3.3 Resolve the empty capability directory
      `openspec/changes/add-runtime-concurrency-limits/specs/state-driven-runtime/`, which contains
      no `spec.md`. Determine whether a delta was dropped or the directory is stray, and remove or
      restore accordingly.

## 4. Process correction

- [ ] 4.1 Amend the canonical-synchronization gate so it enumerates **every canonical spec**, not only
      the change's own deltas. A capability with no delta is currently invisible to that gate, which
      is the defect that produced this change.
- [ ] 4.2 Add strict validation of change-to-canonical provenance to the checkpoint routine: strict
      validation passes on structure alone and cannot detect canonical content that no change
      describes, so it SHALL NOT be reported as evidence of workflow compliance.

## 5. Coordination (owned elsewhere — tracked, not performed here)

- [ ] 5.1 `reshape-developer-facing-interfaces` task `7.17` covers the `ActiveWaitSnapshot` projection
      leak (`FiberId`/`ScopeId`/`WaitSequence`/`Mode` exposed; `AuthoredLocation` and deadline
      missing) and the public `IWorkflowTypeSerializerRegistry` schema-resolution SPI.
- [ ] 5.2 **Unowned:** public `DurableWorkflowRuntime.RaiseEventToDefinitionAsync` implements
      definition-scoped fanout in a shipping first-release package, while reshape task `4.10`
      ("delete public event fanout") is marked complete. Assign an owning task or widen `7.17`.
      Raise before the Section 7 exit review concludes.
- [x] 5.3 Audit `docs/specs/` against the reshape contract. Complete — results in section 6.

## 6. `docs/specs/` harmonization (audited 2026-07-31; no file edited by this change)

Audit result: `docs/specs/` is largely consistent with the reshape. All `WaitLong`, author `Yield`,
`WhenFirst`, `RunExternalJob`, and `RunChild`/`RunChildren` occurrences are negative guards or
deferred-registry entries, not live requirements. The non-buffering rule (`05` §"SHALL NOT buffer
the envelope", "no pending-event mailbox"), two-route targeting (EV-010), wait projection (`09`),
the eleven-package split and `IDurableResourceGovernanceStore` (`10`), and the fixed codec are all
already correct. On the four semantics this change corrects in OpenSpec, `docs/specs` held the right
answer — the OpenSpec capability specs were the stale side.

Root cause of what did not land: the `x.0` gate tasks apply mapped `docs/specs` amendments **before**
each source section begins. The bulk application ran at `8c2dd71` (2026-07-19). The
`AMENDMENT-2026-07-28` package was approved after every relevant gate had already closed, and it
enumerates its targets explicitly — `docs/specs/17-selected-mode-capability-matrix.md` amended,
`docs/specs/17-public-authoring-contract.cs` deliberately **not** amended (its §5), and the numbered
requirement files not mentioned at all. Task `10.14` then synchronized OpenSpec specs, the matrix,
the guide, and the appendix, but its scope never included `04`–`16`. So the omission is a silent
scope gap, not a recorded decision.

- [ ] 6.1 Add the authoring-session lifecycle to `docs/specs/04-requirements-core-runtime.md` as a
      new `CR-xxx` requirement mirroring OpenSpec `workflow-authoring` — session states `Open` /
      `JoinPending` / `Frozen`, handle validity bound to session epoch and lexical scope, atomic
      root-terminal freeze, and rejection of stale, superseded, escaped, and duplicate-join handles
      without graph mutation. Currently absent from every numbered requirement file; the only trace
      in `docs/specs` is the single `SFE-AUTH-LIFECYCLE-003` diagnostic row in the matrix.
- [ ] 6.2 Add `WorkflowFailure` occurrence provenance to
      `docs/specs/04-requirements-core-runtime.md` as a new `CR-xxx` requirement mirroring OpenSpec
      `workflow-contracts` — one authored instruction location plus one runtime-created
      `root`/`branch`/`item` occurrence, attached at failure creation, preserved through ordered
      aggregation, and round-tripped through `orcacore-json-v1` under the closed discriminator
      allowlist. Implemented by reshape task `5.11`; absent from `docs/specs`.
- [ ] 6.3 Add acceptance criteria in `docs/specs/12-acceptance-criteria.md` for 6.1 and 6.2. All 137
      current `AC-` entries contain zero references to authoring session, `JoinPending`, `Frozen`,
      `FailureOccurrence`, failure provenance, or `AuthoredLocation`, so two implemented models have
      no acceptance coverage in the tree that `docs/review/README.md` judges code against.
- [ ] 6.4 Correct `docs/specs/18-semantic-appendix.md:163`, which states that "product source retains
      `MaxActiveFibers` until task 5.13 lands." Task `5.13` is complete and `MaxActiveFibers` has
      zero occurrences in `src/`, so the line is factually wrong about current source state. Note
      this file was synchronized at `50254d0`, so residue survived the most recent sync here too.
- [ ] 6.5 Confirm whether `docs/specs/17-public-authoring-contract.cs` should remain unamended. The
      amendment excluded it deliberately, but reshape proposal item 34 makes that file plus the
      matrix the exact compile/reflection guard baseline. Verify the exclusion still holds now that
      the authoring session is implemented, and record the answer either way so it stops being
      re-derived.
- [ ] 6.6 Extend the `x.0` gate contract so an amendment approved **after** its section's gate has
      closed still has a defined path into `docs/specs`. This is the same defect shape as the
      delta-scoped OpenSpec gate in task `4.1`: a synchronization step that runs once, at a fixed
      point, and silently ignores anything approved later.

## 7. Documentation coherence (structure done 2026-07-31; content items open)

Completed: `docs/normative-source-map.md` created (source classification + cross-tree crosswalk);
`CLAUDE.md` corrected and given a mandatory specification-workflow section; historical
documentation physically separated into `docs/archive/` (171 files, all via `git mv`);
`docs/README.md` and `docs/specs/README.md` relinked. Zero move-caused link breakage.

`CLAUDE.md` had been teaching removed APIs to every agent session — `WaitLong` as a current node in
two places, `AddOrcaCore()` and `AddOrcaCoreHostedServices()` as the hosting entry points, and a
project list missing five v1 packages while naming provisional ones. That is the likeliest single
source of agents reintroducing removed surface.

**Deferred is not removed.** `developer-facing-surface` draws the line: deferred capabilities
(`WhenFirst`, Saga, `RunExternalJob`, public children, nested fan-out, durable lambdas, retry,
pause/resume/archive/purge, authored `Publish`/`Cancel`, event fanout) "SHALL be absent from v1
public assemblies **while remaining recorded with rationale and re-entry criteria in the
future-capability registry**". Only `WaitLong` and author `Yield` are removed "with no alias or
tombstone". Code treatment is identical — absent either way — but the **documentation treatment is
opposite**: a deferred capability must stay on the record, a removed one must disappear. Tasks below
therefore stop docs *teaching* `WhenFirst` as usable; they do not erase it.

- [ ] 7.1 `docs/ephemeral-engine-developer-guide.md` — stop presenting `WhenFirst` as available API.
      Its banner (lines 4, 30) correctly states `WhenFirst` does not ship, but section
      "## Parallel And WhenFirst" (line 492) still explains it and line 536 gives a working
      `.WhenFirst<string>(` example. A guide that contradicts its own banner will be copied from,
      not read around. Replace the how-to content with a short deferred-capability note linking
      `docs/specs/13-phasing-and-open-questions.md` §13.4; **do not delete the mention** — the
      deferral must remain discoverable. **This is the doc developer documentation will be based on
      — fix first.**
- [ ] 7.2 `docs/durable-driver-status.md` — lines 27, 41, 68 assert `WhenFirst` behavior positively
      ("selects one deterministic terminal winner and cancels every loser") with no banner. Restate
      as deferred with a §13.4 link, or archive; it is a dated status doc (2026-07-14) predating
      Sections 4–7.
- [ ] 7.3 `docs/durable-driver-audit.md` — same vintage and same `WhenFirst` exposure; classify as
      RECORD (and freeze) or GUIDE (and restate as deferred).
- [ ] 7.7 Reconcile the registry's **name** across trees. `openspec/specs` calls it "the
      future-capability registry"; `docs/specs` implements it as
      `13-phasing-and-open-questions.md` §13.4 "Explicitly deferred or removed capabilities". No
      link connects the two, so searching either tree for the other's term finds nothing and the
      registry reads as missing. Either rename one side or cross-reference both. Reshape task `9.6`
      ("maintain the future registry") is open and should own this.
- [ ] 7.4 Confirm the remaining active-tree hits are negative guards only: `00-stack-decisions.md`
      (banlist), `02-engineering-conventions.md`, `production-readiness.md`, `end-to-end-plan.md`,
      `01-solution-architecture.md`, and the refactor plan. Spot-checks so far show all negative.
- [ ] 7.5 `docs/orleans-engine/` (5 files) references `WaitLong`. The directory is PLANNED, not
      historical, so it stays active — but a planned variant must not specify a removed node. Decide
      whether to update it now or add an explicit "targets pre-v1 surface" banner.
- [ ] 7.6 Add a CI or pre-commit check that greps the **active** documentation tree (excluding
      `docs/archive/` and `docs/review/`) for removed vocabulary and fails on a positive usage.
      Physical separation makes this checkable for the first time; without it, §7.1–7.3 will recur.
