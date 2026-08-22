# Tasks

## 1. Freeze and review the reduced scope

- [x] 1.1 Reconcile this change against the current `reshape-developer-facing-interfaces` proposal,
      design Decisions 22–26, every delta heading, the canonical baseline, and the two frozen
      rejection verdicts; confirm that only `event-driven-prototype` and `state-driven-runtime`
      remain unique capability deltas.
- [x] 1.2 Remove duplicate delta ownership for `event-routing-and-waits`,
      `durable-persistence-and-outbox`, `repository-foundation`, and `durable-runtime`; retain their
      complete contract exclusively in `reshape-developer-facing-interfaces` and preserve no
      last-writer-wins synchronization path here.
- [x] 1.3 Create and review `design.md` through `/opsx:continue`, documenting capability ownership,
      cross-change provenance, canonical synchronization order, immutable-history treatment, and
      rollback of planning-only changes.
- [x] 1.4 After the design exists, strict-validate this change and all active changes together and
      verify that every modified capability listed by the proposal has exactly one local delta.
- [x] 1.5 **REQUIRED:** obtain a new dated immutable independent approval of the reduced planning
      target against the unchanged canonical baseline. The prior rejection remains immutable and
      synchronization SHALL NOT begin before approval without a gate-blocking finding.

## 2. Canonical synchronization

- [x] 2.1 **POST-APPROVAL:** synchronize only the approved `event-driven-prototype` and
      `state-driven-runtime` deltas into `openspec/specs/`, then strict-validate the synchronized
      result and record the exact canonical diff.
- [x] 2.2 Apply any approved `Purpose` revisions by hand during 2.1: retain the prototype capability
      for planning history outside v1 and describe ephemeral ordinary-`Wait` residency as runtime
      and hosting policy without a separate authored long-wait member.
- [x] 2.3 Verify canonical messaging, persistence/outbox, repository-friend, and governance
      requirements are changed only by the approved reshape synchronization. Reject any duplicate
      active delta heading or harmonize sync edit to those capabilities.

## 3. Corpus and orphan sweep

- [x] 3.1 Sweep every canonical spec and active planning/documentation source for removed and
      deferred vocabulary. Distinguish removed `WaitLong`/authored `Yield`; deferred `WhenFirst`,
      Saga, generic jobs/children, nested fan-out, pause/resume/archive/purge, and authored `Cancel`;
      and current durable pre-wait buffering, explicit definition fanout, start-or-deliver, and
      authored `Publish`. Reject both positive use of removed/deferred APIs and negative treatment of
      newly approved Section 7B behavior. **Completed:** the dated task 3.1 sweep artifact records
      89 active sources, zero positive removed/deferred call forms, 23 active documentation sources
      with stale Section 7B-negative language, and 50 approved reshape-to-canonical requirement
      operations across 10 capabilities; every unresolved class is assigned to tasks 4.1-4.3,
      5.1-5.2, 7.1, and 7.3-7.5 without duplicating reshape-owned requirements.
- [x] 3.2 Retain `event-driven-prototype` as an out-of-v1 planning-history capability; require a
      separate reviewed amendment before any prototype project, package, or public surface returns.
- [x] 3.3 Add recurring enumeration of every `openspec/changes/*/specs/*/` capability directory and
      fail the corpus sweep when any directory lacks `spec.md`. Preserve the recorded disposition
      of the former `add-runtime-concurrency-limits/specs/state-driven-runtime/` directory, which
      planning remediation verified as stray and removed because that completed change declares
      only the `runtime-resource-governance` delta. **Completed:** the infrastructure corpus guard
      dynamically enumerates all 16 active capability directories, requires `spec.md` in each, and
      proves the runtime-concurrency proposal and directory inventory both declare only
      `runtime-resource-governance` while the former stray directory remains absent.

## 4. Process correction

- [x] 4.1 Amend the canonical-synchronization gate so it enumerates every canonical capability,
      every active capability directory, and every active delta heading; requires `spec.md` in each
      capability directory; fails on an unexplained missing capability or duplicate owner; and does
      not limit review to one change's own delta directory. **Completed:** the repository-wide
      infrastructure gate enumerates 14 canonical capabilities, 16 active capability directories,
      and all 176 active requirement headings; reconciles each change proposal bidirectionally with
      its delta directories; distinguishes declared new capabilities from unexplained missing
      modified capabilities; and rejects duplicate `(capability, requirement)` owners. The gate
      exposed and repaired the pre-existing undeclared `structured-fiber-execution` reshape delta,
      reports malformed proposal headings diagnostically, and preserves the runtime-concurrency
      stray disposition across either an active or normally archived change record.
- [x] 4.2 Add strict change-to-canonical provenance validation to the checkpoint routine. Structural
      OpenSpec validation SHALL NOT be reported as semantic approval when active changes conflict,
      duplicate one requirement, or omit a canonical capability. **Completed:** the checkpoint
      guard now hashes a reproducible 176-row change-to-canonical record, enumerates the complete
      canonical capability inventory independently of whether each capability currently has an
      active delta, rejects duplicate owners and non-verbatim `MODIFIED` headings, requires
      reason/migration evidence plus an embedded exact historical block and pinned hash for synchronized `REMOVED`
      headings, and records the exact 50 reshape operations against their explicitly scoped tasks
      5.1/5.2. Normal archival may change the active record and require a reviewed
      fixture refreeze, but it cannot make the guard permanently red through an active-delta
      existence invariant. After tasks 5.1 and 5.2, its strict semantic-approval path records all
      173 active canonical operations as synchronized; the three declared bootstrap requirements
      outside the canonical capability set remain recorded separately. The
      gate also dispositions the 18-row `developer-facing-surface` case where a declared-new
      capability already exists canonically and links it to the permanent post-gate amendment
      registry, while pinning the exact 14-directory canonical inventory and 16-directory
      active-delta inventory with reproducible SHA-256 records. The human-readable provenance
      artifact, per-capability LF-normalized canonical-preamble hashes, squash-safe embedded
      removal evidence, CI `Disposition=Infrastructure` lane, symmetric requirement-block parser,
      and turns-green capability/count assignments are executable parts of the same contract. The
      historical-removal catalog is permanent: currently active synchronized removals are validated
      as its subset, and an available source commit is read directly without requiring ancestry from
      `HEAD`, so normal archival and squash/rebase integration remain recoverable without a guard
      source edit. Post-review hardening gives the active set and permanent catalog distinct wrapper
      types so reversing them fails compilation, exercises the subset helper against a synthetic
      strict-subset catalog so its body cannot invert silently, pins the permanent catalog's reviewed
      cardinality with a precise shrink diagnostic, requires every CI checkout step—named or
      unnamed and independent of action version—to retain full history whenever historical
      corroboration is available, and distinguishes Git-emitted raw-anchor manifests from
      path-sorted set-only evidence. The review-manifest provenance fixture now makes that
      distinction executable for Tasks 5.1 and 5.2, mutation-tests path sorting of the Task 5.2 raw
      manifest, validates the exact Task 5.1 committed path set and tree, and blocks every Task 5.2
      checkpoint until Task 5.1 approval evidence is committed in its ancestry. New freezes derive
      their Git-order manifest from tracked content diffs plus untracked files, so status-only
      attribute/index entries that cannot enter a commit are excluded and mutation-tested.
- [x] 4.3 Define a post-gate amendment path so a decision approved after its original section gate
      closed must re-enter canonical requirements, acceptance criteria, implementation tasks,
      refreeze, and independent approval rather than silently bypassing the completed gate. This
      task explicitly owns the `developer-facing-surface` (18) declared-new/already-canonical case
      and SHALL preserve ordinary active-change archival by updating the reviewed provenance
      fixture rather than requiring every canonical capability to retain an active delta forever.
      **Completed:** a machine-readable eight-stage registry now binds the Section 7B amendment to
      its original 4.15/10.14 gates, 7.23 approval, completed canonical task 5.1, open numbered
      requirement/acceptance task 7.3, exactly 11 completed 7.24-7.34 implementation tasks,
      executable evidence, and the 7.22 refreeze/verdict. The registry embeds all 18 exact
      requirement identities, verifies their zero remaining canonical mismatches against the resolved
      active or dated archived change record after task 5.1, and normalizes verdict line endings before
      exact task/approval checks. The structural case remains linked permanently without requiring the
      owning change to remain active. Checkpoint validation
      also replaces the shared 3.11c lease-recovery fixture's scheduler-sensitive wall-clock gate
      waits with workflow-owned completion signals and pins that rule across every behavior-scenario
      and provider-certification source, excluding generated `bin`/`obj` output and including the
      former provider confirmation/tombstone gate. The completed canonical-reconciliation count is
      required inside the task's `**Completed:**` statement, so its earlier `(7)` scope declaration
      cannot satisfy the resulting `(0)` pin.

## 5. Coordination owned by `reshape-developer-facing-interfaces`

- [x] 5.1 Verify reshape task 7.23 approves and reconciles the complete event-contract, durable
      buffering, four-route ingress, fanout, start-or-deliver, publish/outbox, dispatcher, and
      application-catalog amendment before either change synchronizes canonical content. Consume
      all 42 vocabulary-bearing canonical mismatches recorded by the task 3.1 sweep artifact:
      `developer-facing-surface` (7), `durable-persistence-and-outbox` (5), `durable-runtime` (8),
      `event-routing-and-waits` (8), `state-driven-runtime` (2), `workflow-authoring` (3), and
      `workflow-contracts` (9). **Completed:** the immutable task 7.23 verdict is `APPROVE`; all 42
      requirement blocks now match the authoritative reshape deltas exactly, including removal of
      the authored-`Yield` canonical block. `developer-facing-surface` (0) and the other six named
      capabilities have zero pending canonical operations; at that checkpoint only the eight task
      5.2 mismatches remained.
- [x] 5.2 Verify reshape tasks 7.16–7.22 close the complete API baseline, typed-boundary replacement,
      deletion ledger, operational statistics/observability/retention restoration, provider
      disposition, semantic test crosswalk, deterministic guards, samples, and fresh package feed;
      account for the eight remaining non-vocabulary canonical mismatches recorded by task 3.1:
      `management-and-querying` (1), `quality-and-verification` (5),
      `repository-foundation` (2). **Completed:** reshape tasks 7.16–7.22 and the immutable task 7.22
      `APPROVE` verdict close the owned source, package, provider, deletion-ledger, semantic-crosswalk,
      sample, deterministic-guard, and fresh-feed gates. All eight requirement blocks now match the
      authoritative reshape deltas exactly; the 176-row provenance record contains 173 synchronized
      canonical operations, three declared bootstrap requirements outside canonical, zero pending
      operations, and is semantic-approval eligible. The same slice closes the remaining task 5.1
      review observations by making CI checkout detection version-agnostic, correcting the recursive
      wall-clock-source scan description, and requiring future raw-anchor manifests to retain Git's
      emitted order. Post-review hardening pins all 14 canonical preambles individually and as one
      aggregate record, so an unreviewed `## Purpose` edit fails even when requirement provenance is
      unchanged. The 2026-08-21 independent review reproduced every technical claim but returned
      `REJECT` because checkpoint `ff11ead781f8fef343fafc6e6bc8307d746e4a05` has no Task 5.1
      independent-approval verdict or disclosed retroactive owner approval. Its non-blocking raw
      manifest-order observation is now enforced by a dedicated executable fixture and mutation
      regression; that hardening does not clear the provenance rejection.
- [x] 5.2a **REMEDIATION REQUIRED:** Preserve the immutable Task 5.2 `REJECT` verdict, obtain an
      independent verdict for the exact Task 5.1 checkpoint
      `ff11ead781f8fef343fafc6e6bc8307d746e4a05` or an explicit dated owner-approval disclosure, and
      commit that approval provenance as a distinct checkpoint before refreezing Task 5.2. Keep
      Task 5.3 blocked. The implementation owner may prepare the request and executable guards but
      SHALL NOT self-author the approval. Disclose that the Task 5.1 manifest is path-sorted
      set-only evidence, preserve it byte-for-byte, validate the exact 17-path checkpoint tree
      against parent `179421029f62bc0cd4d5968d465cf045f420ba34`, and retain the frozen raw and
      content anchors. **Remediation remains open after two independent `REJECT` verdicts:** the
      provenance fixture now distinguishes missing review, recorded rejection, and approval; pins
      every Task 5.1 and Task 5.2 verdict byte-for-byte; and keeps a Task 5.2 checkpoint prohibited.
      The clean-checkout deletion-ledger regression, complete `git archive` draining, LF-pinned
      companion baseline, and rejected-state guard are implemented but require review as a new
      remediation target before Task 5.1 approval evidence can be committed. A later provenance
      review rejected the fixture's invented 2,427-byte Task 5.1 record and incorrect Task 5.2
      content digest. Schema 5 now embeds every historical status/path/byte/hash row, recomputes the
      published 2,428-byte
      `741cfd6bbdd46cb4390c2f40c0d21d81d35b3e3749438b38efda44f26da1ff72` Task 5.1 record and
      1,793-byte `0068973b0dbd4c1f79086a0262cefe62b728911362b5cd43fe472e0c7daebc8a`
      Task 5.2 record,
      requires Task 5.1 to equal its raw commit-blob projection, and pins canonical OpenSpec markdown
      plus the Section 7 declaration crosswalk to LF. The approving provenance-remediation review
      then identified four status-only canonical-spec entries that were byte-identical to `HEAD`;
      the commit-real freeze projection excludes them, and the Task 5.2 record now pins the four
      historical rows still independently reproducible from current worktree bytes. The task remains
      open until external approval evidence is committed. The 2026-08-22 external `APPROVE` verdict
      for Task 5.1 is registered in `ApprovalAwaitingEvidenceCommit`, a green transition state that
      cannot authorize Task 5.2 and does not claim the SHA of its own future evidence commit. The
      same remediation discloses and exactly pins the review-only whitespace attribute, discovers
      the already-landed Task 5.2 canonical output at `d0e7c4821199b8b1ee13d5f6fd22f79133abc576`
      from content paths plus the checked ledger rather than its subject, and requires every
      current-worktree historical-row pin to be maximal. **Completed:** distinct approval-evidence
      checkpoint `5140208c7b82332ada8b7a39848888ddd58eb89d` preserves the external verdict and
      transition record; the following mechanical activation records that existing SHA as
      `Approved`. Task 5.2 may now be refrozen, but its immutable `REJECT` verdict remains in force
      until a new exact-target review approves it.
- [ ] 5.3 Verify reshape remains the sole active delta owner for repository friend topology and the
      durable resource-governance aggregate; byte-identical duplicate requirements do not count as
      harmless redundancy.

## 6. Numbered-requirement and acceptance harmonization

- [ ] 6.1 Add the authoring-session lifecycle to
      `docs/specs/04-requirements-core-runtime.md` with a new stable requirement ID: `Open`,
      `JoinPending`, and `Frozen`; session/epoch/lexical-scope handle validity; atomic root-terminal
      freeze; and mutation-free rejection of stale, superseded, escaped, or duplicate-join handles.
- [ ] 6.2 Add `WorkflowFailure` authored and runtime occurrence provenance to
      `docs/specs/04-requirements-core-runtime.md`: one `AuthoredLocation`, one runtime-created
      `root`/`branch`/`item` occurrence, creation-time attachment, ordered aggregation preservation,
      and fixed-codec round-trip under the closed discriminator allowlist.
- [ ] 6.3 Add acceptance criteria in `docs/specs/12-acceptance-criteria.md` for tasks 6.1 and 6.2 and
      map them bidirectionally to their stable requirement IDs and executable guards.
- [ ] 6.4 Correct the stale `MaxActiveFibers` implementation statement in
      `docs/specs/18-semantic-appendix.md` and verify every remaining mention is historical,
      deferred, or negative rather than a current source claim.
- [ ] 6.5 Decide and record whether `docs/specs/17-public-authoring-contract.cs` remains deliberately
      unchanged for authoring-session internals while the exhaustive assembly API baseline verifies
      their public absence.
- [ ] 6.6 Reconcile the future-capability registry name and cross-reference across both normative
      trees so deferred and removed concepts remain distinguishable and searchable.

## 7. Documentation and guard coherence

- [ ] 7.1 Remove positive how-to usage of deferred `WhenFirst` and other non-v1 APIs from active
      guides while retaining short future-registry notes and re-entry links. Re-run the task 3.1
      positive-call scan and require zero findings.
- [ ] 7.2 Keep dated status/audit/review records immutable under `docs/archive/` or `docs/review/`;
      update active indexes and superseding records instead of rewriting historical conclusions.
- [ ] 7.3 Reconcile active architecture, implementation, production-readiness, and developer guides
      with durable pre-wait buffering, four self-routing route variants, durable `Publish`, exact
      role-specific hosting, fixed codec, application-facing absence of broad statistics, and
      provider/operator ownership of retained statistics and retention behavior. Correct 22 of the
      stale active documentation sources enumerated by the task 3.1 sweep artifact; task 7.4 owns
      the separate Orleans future-hosting note. As the numbered-requirement and acceptance owner
      for the post-gate `developer-facing-surface` (18) record, this task must specifically reconcile
      `docs/specs/05-requirements-events-waits-timers.md` and
      `docs/specs/12-acceptance-criteria.md` with the approved Section 7B contract.
- [ ] 7.4 Preserve the superseded Orleans plan under the archive and maintain only one active future
      hosting boundary note using ordinary cold-capable `Wait`, the current event/outbox contract,
      role-specific hosting, exact tier ownership, and a new-change prerequisite. Correct the
      `docs/orleans-engine/README.md` finding recorded by the task 3.1 sweep.
- [ ] 7.5 Add an active-tree documentation check, excluding `docs/archive/` and immutable review
      records, that rejects positive removed/deferred APIs and stale negative claims about approved
      Section 7B behavior. Use the task 3.1 classifications as the initial complete fixture and
      require every recorded stale-negative finding to be closed.
- [ ] 7.6 Correct or delete namespace-pinned `ForbiddenPublicSymbols` entries that cannot match the
      current assembly owners; add a regression proving each forbidden symbol fails under its exact
      current or historical qualified owner rather than silently passing an impossible namespace.
- [ ] 7.7 Repair the Phase-0 kickoff prompt archive move so Git records a history-preserving rename,
      or add an explicit immutable archive provenance record when an exact rename is impossible;
      verify `git log --follow` or the recorded predecessor and do not edit historical content.

## 8. Final harmonization gate

- [ ] 8.1 After the reduced deltas, design, canonical synchronization, corpus/process/docs tasks, and
      reshape coordination prerequisites are complete, strict-validate all active changes and
      record the exact canonical diff, duplicate-heading scan, vocabulary/link checks, task
      accounting, normative-source classification, capability-directory inventory count/hash, and
      zero capability directories without `spec.md`.
- [ ] 8.2 Freeze the exact ordered target manifest and obtain a new immutable independent exit
      approval of the synchronized canonical/docs result. Prior rejection verdicts remain unchanged.
- [ ] 8.3 **POST-APPROVAL:** verify zero manifest drift, create the mandatory coherent harmonization
      checkpoint commit, and verify the committed tree/worktree state. This is the final prerequisite
      supplied by this change to reshape task 8.0.
