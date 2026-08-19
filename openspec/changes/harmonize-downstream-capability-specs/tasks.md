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
- [ ] 4.2 Add strict change-to-canonical provenance validation to the checkpoint routine. Structural
      OpenSpec validation SHALL NOT be reported as semantic approval when active changes conflict,
      duplicate one requirement, or omit a canonical capability.
- [ ] 4.3 Define a post-gate amendment path so a decision approved after its original section gate
      closed must re-enter canonical requirements, acceptance criteria, implementation tasks,
      refreeze, and independent approval rather than silently bypassing the completed gate.

## 5. Coordination owned by `reshape-developer-facing-interfaces`

- [ ] 5.1 Verify reshape task 7.23 approves and reconciles the complete event-contract, durable
      buffering, four-route ingress, fanout, start-or-deliver, publish/outbox, dispatcher, and
      application-catalog amendment before either change synchronizes canonical content. Consume
      all 42 vocabulary-bearing canonical mismatches recorded by the task 3.1 sweep artifact.
- [ ] 5.2 Verify reshape tasks 7.16–7.22 close the complete API baseline, typed-boundary replacement,
      deletion ledger, operational statistics/observability/retention restoration, provider
      disposition, semantic test crosswalk, deterministic guards, samples, and fresh package feed;
      account for the eight remaining non-vocabulary canonical mismatches recorded by task 3.1.
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
      the separate Orleans future-hosting note.
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
