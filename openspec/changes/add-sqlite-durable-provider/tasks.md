## 0. Proposal and implementation approval gate

**Current state: Proposed; implementation pending explicit approval. All tasks below are intentionally unchecked.** Artifact creation/validation and OpenSpec `done`/apply-ready status do not authorize code, dependency changes, public API exposure, canonical synchronization, task activation, or publication. Each coherent phase ends with its own validated, independently reviewed focused checkpoint before the next phase begins. Preserve unrelated working-tree changes and historical verdicts throughout.

- [ ] 0.1 Review and freeze the proposal, design, five delta specs, exact proposed SQLite registration/options declarations, complete ten-port role, single-owner deployment scope, and final DAG parity dependency as one contract target.
- [ ] 0.2 Obtain explicit human approval to proceed with this contract and implementation plan plus the required independent contract verdict. Record the exact reviewed source/planning identity; until then perform no implementation or canonical-spec changes.
- [ ] 0.3 Checkpoint only the approved planning target and its required evidence, then record approval/activation without treating passing validation as approval. Keep publication and external release actions separately authorized.

## 1. Canonical contract and traceability reconciliation

Dependency: 0.3. This is a separately reviewed documentation/specification phase; executable API baselines remain unchanged until the owning source phase exists.

- [ ] 1.1 Resolve the live normative source map, package/friend manifest, active reshape/harmonization changes, and latest approved DAG contract/fingerprint amendments; enumerate every affected provider-count, package-count, hosting/API-owner, schema, certification, and support claim.
- [ ] 1.2 Synchronize the approved SQLite capability and four modified capability deltas into canonical OpenSpec through the repository's approved sync workflow, preserving unrelated requirements and all historical reviews.
- [ ] 1.3 Reconcile `docs/specs/10-provider-model-and-extensibility.md`, `docs/specs/17-selected-mode-capability-matrix.md`, relevant acceptance/non-functional/observability requirements, `docs/implementation/00-stack-decisions.md`, architecture notes, normative source map, and affected active deltas. Preserve the existing deferred-authoring and DAG source gates.
- [ ] 1.4 Produce the certification applicability/traceability matrix: unchanged logical invariants, SQLite same-host/boot-boundary equivalents, explicit live multi-host deployment exclusions, exact future source/API/package guard owners, and workflow-only versus full/DAG completion evidence.
- [ ] 1.5 Run strict OpenSpec and focused documentation/provenance checks; obtain independent approval and checkpoint this reconciliation before starting source work. No existing expected-red DAG guard is reclassified without its owning evidence.

## 2. SQLite package, file ownership, worker, and schema foundation

Dependency: 1.5 and the approved exact contract. Do not advertise/register an incomplete provider role. Intermediate schema files remain pre-release development artifacts with no promised upgrade path.

- [ ] 2.1 Review and freeze the centrally pinned `Microsoft.Data.Sqlite`/native-runtime tuple, including SQLite 3.51.3-or-later WAL-reset fixes, native Windows/Linux assets, supported .NET 10 targets, actual version/source-id verification, and finite queue/command/busy/stop handling constants.
- [ ] 2.2 Add the optional provider and black-box test projects with only the approved dependencies; wire the root solution and matching package/API/architecture inventory entries as real source appears, with no new friend grants or engine/provider-to-provider references.
- [ ] 2.3 Implement the approved immutable path options and validation-only registration profile rules; verify captured relative paths, invalid/sentinel/known-network input, profile equality, and absence of registration-time I/O or threads.
- [ ] 2.4 Implement the owned worker and one non-pooled private-cache connection, actual exclusive lock acquisition, required pragma readback, initialization/readiness gate, fair bounded request admission, finite busy budgets, and complete failure/shutdown disposal ordering.
- [ ] 2.5 Implement provider-owned first-create workflow/governance record tables, constraints/indexes/order counters, ownership/schema markers, canonical-digest migration journal, and unknown/malformed schema rejection without reset or compatibility upgrade DDL.
- [ ] 2.6 Prove ownership with independent service providers and processes, supported path aliases, failed initialization, abrupt owner death/reopen, and no acceptance by the losing owner. Prove cancellation before execution and safe ownership retention during an incomplete stop.
- [ ] 2.7 Run focused foundation tests, schema/architecture checks, and scoped ReSharper inspection where applicable; retain findings, obtain independent phase approval, and checkpoint before implementing the workflow ports.

## 3. Atomic workflow, inbox, idempotency, and outbox storage

Dependency: 2.7. Use provider-owned parameterized SQLite SQL and exact protocol values; existing engine semantics remain authoritative.

- [ ] 3.1 Implement expected-version event append, stream heads, exact checkpoint/tail load, complete atomic commit batches, and storage-error versus logical-conflict classification; certify competing same-host drivers and rollback/commit boundaries.
- [ ] 3.2 Implement route/target inbox identity, normalized-envelope binding, accepted-before-wait storage, stable acceptance ordering, route revisions, and atomic event/wait matching; certify duplicate/conflicting IDs and ordinal round trips.
- [ ] 3.3 Implement complete fanout target snapshots and per-target ownership plus input-bound start-or-deliver intent/reservation/materialization and provider-global start-key conflict behavior; certify all-or-none acceptance.
- [ ] 3.4 Implement external/internal outbox records, disjoint selectors, claims/fences, attempts/retry delay, acknowledgement/release/poison, and identity-stable replay/ambiguity handling without broker SDKs or application sends in transactions.
- [ ] 3.5 Implement instance/wait/history/output projections needed by the shared runtime, ingress matching, management, and reconciliation; prove side-effect-free reads and consistent snapshots.
- [ ] 3.6 Run the shared event-store, start/fanout, messaging, and continue-as-new certification portions against real SQLite files, including exact bytes/fingerprints and kill-before/after-commit evidence; review and checkpoint this phase before proceeding.

## 4. Timers, resource pools, governance, and provider operations

Dependency: 3.6. No missing port may be replaced with a no-op or process-local durable authority.

- [ ] 4.1 Implement durable timer schedule/claim/complete/release, boot/claim fencing, and due ordering; certify event/timeout races, interrupted claims, and overdue recovery through fresh hosts.
- [ ] 4.2 Implement the complete `IResourcePoolStore` contract with persisted requests/tickets/waiters, atomic multi-pool obligations, confirmation/release evidence, audit/review history, and ordinal identity behavior; run shared resource-pool certification.
- [ ] 4.3 Implement expected-version resource-governance append/load with copied payloads, format/checksum validation, exact sequence continuity, whole-batch atomicity, quarantine/tombstones, resize/stop-confirmation, and workflow/governance reconciliation; run shared governance/barrier certification through the existing approved seam.
- [ ] 4.4 Implement operational observations/statistics and finite-batch maintenance/retention/archive/purge/poison semantics, including all messaging/lease/reference blockers and safe WAL checkpoint pressure reporting.
- [ ] 4.5 Complete provider-role composition and startup integration with the existing durable engine and alternative callback-only ingress owner; reject partial/conflicting registrations and prove no progression/acceptance before schema/ownership readiness.
- [ ] 4.6 Run timer, resource-pool, governance, retention, operational, and infrastructure certification plus scoped static analysis; obtain independent approval and checkpoint before application-level workflow qualification.

## 5. Workflow parity and failure-recovery milestone

Dependency: 4.6. This milestone establishes supported workflow evidence only; it does not establish full SQLite completion while phase 6 remains open.

- [ ] 5.1 Run every applicable shared logical certification case unchanged against disposable real files; record each server-specific live multi-host case's explicit disposition and SQLite owner-rejection, sequential-boot, or concurrent same-host equivalent.
- [ ] 5.2 Add public application journeys for supported structured workflows, cold waits/events, callbacks/start-or-deliver, fanout, durable publish, deadlines/retries/operation identities, cancellation, continue-as-new, output/query access, and resource governance across host replacement.
- [ ] 5.3 Add controlled process termination before/after transaction commit, acceptance, timer claim, dispatch acknowledgement, and governance handoff. Reopen from a new process and prove whole ownership/record recovery and identity-stable ambiguity handling.
- [ ] 5.4 Exercise disk-full/read-only/I/O failures, corrupt/unsupported records, changed migration digests, queue pressure, lock contention, cancellation, disposal failure paths, and repeated startup/stop; distinguish native/harness failures from product defects and preserve evidence.
- [ ] 5.5 Add a SQLite native Windows x64/Linux x64 untrimmed JIT CI lane and retain existing server-provider lanes; record actual managed/native package identities and required runtime settings for each lane.
- [ ] 5.6 Obtain independent approval and checkpoint the workflow-only evidence with an explicit pending-DAG status. Keep phase 6 and final-closeout tasks open if the shared DAG dependency is not ready.

## 6. Complete durable DAG parity after shared runtime approval

Dependency: 5.6 and independently approved/checkpointed source/provider/child-pump/runtime-bridge slices of `admit-durable-dag-run-progression`. This phase cannot bypass that change's authority or replace it with a SQLite-specific bridge.

- [ ] 6.1 Resolve the actual approved DAG protocol/schema/bridge baseline and fingerprint amendment; freeze the SQLite DG-P scenario applicability map and exact source target without changing or implementing the shared runtime under this provider task.
- [ ] 6.2 Implement the approved typed DAG stream/record/checkpoint branch and complete first-create run/event/operation/node/subscription/lease-request tables, constraints, shared-outbox fields, and indexes. Recreate disposable intermediate development databases; support an upgrade only through a separately reviewed migration amendment.
- [ ] 6.3 Implement receipt-stable atomic DAG append, input/output presence/bytes, node/admission indexes, caller-key namespace, compact history/current checkpoint, production transaction clock/internal test seam, FIFO fenced short turns, hard deadlines, and stale/consumed-owner rejection.
- [ ] 6.4 Implement discovery/subscriptions and existing shared-outbox selectors/notifications; run the approved child-start/join/cancel and ready-wake bridge with decode/map outside database turns and no new public child API or friend.
- [ ] 6.5 Execute SQLite equivalents for all approved DG-P obligations, including concurrent same-host owners, sequential boot recovery, capacity/deadline/FIFO behavior, separate-child reconciliation, null/decode/failure/cancellation closure, slow/stuck mappers, and acknowledged/lost/poisoned ready wakes. Preserve server-only remote-host evidence as a distinct deployment obligation.
- [ ] 6.6 Run an external package-shaped full DAG restart journey on a real SQLite file; obtain independent parity approval and checkpoint the source/evidence before full-support qualification.

## 7. Exact package consumers, operations, and measured support envelope

Dependency: 6.6. No full-feature label or completion claim precedes this dependency.

- [ ] 7.1 Update exact API/package manifests, consumer inventories, guard provenance, pack/release package-count checks, and build/CI wiring to match implemented source, with no extra public type/member, friend, or dependency. Run scoped ReSharper and the required build/test/architecture checks; retain and triage findings.
- [ ] 7.2 Pack one exact candidate feed; run fresh-cache PackageReference-only SQLite engine, alternative callback-only, and DAG consumers with restored package SHA-512 verification, actual SQLite version/source-id checks, and native Windows/Linux execution. Verify SQLite assets remain absent from minimal ephemeral and other-provider closures.
- [ ] 7.3 Add the application sample and supported cold backup/restore instructions; exercise stop/dispose/lock-release, consistent backup, exclusive restore, matching schema/definitions, and resumption of pending workflow/timer/message/DAG work without deleting live journal files.
- [ ] 7.4 Freeze the reproducible workload, hardware, required durability settings, and quantitative pass thresholds before measurement; capture throughput, queue/transaction latency, timer lag, startup/recovery, retention cost, and database/history/WAL growth under concurrent workflow load.
- [ ] 7.5 Publish accurate developer/operations/support documentation, README/provider comparisons, diagnostics and backup guidance, measured deployment limits, single-owner/live-inspection constraints, at-least-once external effects, and separate Native AOT/physical-storage qualification boundaries.
- [ ] 7.6 Obtain independent approval of the package/operations/performance evidence and checkpoint this phase before final closeout.

## 8. Final full-support approval and closeout

Dependency: 7.6 and all prior gates. Publication, tagging, registry verification, and archival remain separately authorized actions.

- [ ] 8.1 Freeze the complete source/package identity and workflow/DAG/certification/operations evidence; resolve all capability/scenario dispositions and refresh status/provenance only for the reviewed target. Verify unrelated working-tree changes are excluded.
- [ ] 8.2 Obtain independent final approval plus human acceptance of full SQLite durable support for the defined single-application scope. Do not derive approval from green tests, packing, native execution, or OpenSpec artifact status.
- [ ] 8.3 Checkpoint the exact approved closeout and its evidence, then activate completion only after every required task is truly satisfied. Leave final tasks open while DAG or other obligations remain pending; do not publish or archive without a separate request.
