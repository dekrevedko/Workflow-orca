# Task 8.0 — Section 8 requirement gate

Date: 2026-09-26. This is a planning and review target, not authorization for
Section 8 product source. The prerequisite harmonization closeout is checkpoint
`4f95a1ce1cd643bafff3b7375c53334a11733c0f`, independent-approval evidence
`457a862ffe4bfad7a97b403ed70a1c9938271cc1`, and activation
`4ef6253e31aaad3698afcbf7fd2d3eaa0dda0bb8`. Section 7A/7B exit is already
checkpointed under reshape tasks 7.22 and 7.23.

## Authority and synchronization

The canonical OpenSpec tree is the requirement-level owner; numbered
`docs/specs/` documents supply the selected-mode contract and acceptance
criteria. The active reshape deltas are the provenance of synchronized changes,
not a second independent implementation contract. At this gate,
`tests/OrcaCore.DeveloperSurface.Guards/Fixtures/openspec-provenance-checkpoint.json`
records 176 rows, 46,211 record bytes, SHA-256
`ea8719e768182f4eea097cb280d3487426a9a7450ca7becb72616e567e30b756`,
`semanticApprovalEligible = true`, and zero pending canonical operations.
Strict OpenSpec validation remains necessary but does not alone approve
semantics. Any disagreement found during Section 8 work must enter a reviewed
amendment before source work; this gate does not silently edit either normative
tree.

The selected-mode authority is
`docs/specs/17-selected-mode-capability-matrix.md` §17.2.6 and its hosting,
package, concurrency, and compiler sections. The associated first-release
criteria are `docs/specs/12-acceptance-criteria.md` AC-606–AC-618, especially
AC-613 (one unified internal outbox) and AC-614 (root/parent lineage), and
`docs/specs/14-driving-scenario-eks-job-scheduler.md` JS-001–JS-010 and
JS-AC-001–JS-AC-018. Document 14 has no canonical counterpart by design;
its numbered obligations are not waived by a zero-pending OpenSpec sync.
`docs/specs/06-requirements-durable-execution.md` DU-031/033 and
`docs/specs/16-requirements-durable-driver.md` DR-037/041 supply the child-start
outbox, disjoint-claim, and typed-driving obligations. DR-AC-008 is document
16's DR-P4 exit criterion. AC-601–605
cover bounded workflow fanout, not the DAG-node ceiling. The C# authoring
companion `docs/specs/17-public-authoring-contract.cs` is intentionally
workflow-authoring-only and is not a source of DAG declarations.

## Task-to-requirement map

The quoted OpenSpec headings below are exact canonical requirement identities.
`doc 17` means `17-selected-mode-capability-matrix.md`; `AC` means
`12-acceptance-criteria.md`.

| Task | Canonical OpenSpec requirement ownership | Numbered contract and executable exit |
| --- | --- | --- |
| 8.1 | `repository-foundation`: “Repository has explicit solution topology”, “Dependency direction remains one-way”, “Package topology and ownership are exact”; `developer-facing-surface`: “Hosting registration selects execution mode explicitly”, “DAG authoring makes no visualization promise” | doc 17 §§17.2.6, hosting/package tables; the DAG role requires durable-engine hosting. Reconcile the existing two package skeletons against the exact manifest; do not create duplicate project IDs. |
| 8.2 | `developer-facing-surface`: “DAG authoring makes no visualization promise”; `workflow-contracts`: “Workflow references expose typed external contracts”; `workflow-authoring`: “Built definitions are immutable”; `quality-and-verification`: “Typed DAG execution is acceptance tested” | doc 17 §17.2.6 typed resultless/resultful nodes, direct dependencies and one `MapInput`; AC-606, AC-615. |
| 8.3 | `durable-runtime`: “Durable DAG progression is runtime owned”; `workflow-contracts`: “Durable values use one fixed detached codec”; `quality-and-verification`: “Typed DAG execution is acceptance tested” | doc 17 §§17.2.6, 17.3; AC-607 and AC-608. Build rejects inspectable structure; opaque non-direct `OutputOf` fails at mapping time as `DAG_INPUT_MAPPING_INVALID` before input commit or child start. |
| 8.4 | `repository-foundation`: “Dependency direction remains one-way”, “Executable compiler IR remains implementation only”; `developer-facing-surface`: “Implementation package boundaries use exact internal friends”; `durable-runtime`: “Durable DAG progression is runtime owned” | doc 17 §17.2.6 internal child-start/join; AC-612 and AC-614. The sole DAG-to-durable runtime bridge is `OrcaCore.Durable.Hosting -> OrcaCore.Dag.Hosting`; no public child API. The separately proposed `OrcaCore -> OrcaCore.Dag` authoring friend grants no child-start access and requires its own post-gate contract approval. |
| 8.5 | `durable-runtime`: “Durable DAG progression is runtime owned”; `durable-persistence-and-outbox`: “Workflow store persists first-class durable records”, “Outbox delivery is adapter-driven and at-least-once”; `quality-and-verification`: “Typed DAG execution is acceptance tested” | doc 17 §17.2.6 runtime-owned restart/reattachment; DU-031/033, DR-037/041, DR-AC-008 and AC-608, AC-610, AC-613, AC-614. DU-031 names internal DAG child-start commands in the commit-bound outbox; DR-037 gives the internal dispatcher the disjoint claim. Child-start/join work uses the one logical provider outbox, with claims partitioned from public workflow events; only the internal runtime pump may claim it, and the application dispatcher never receives it. No DAG-private transport. |
| 8.6 | `durable-runtime`: “Durable DAG progression is runtime owned”; `management-and-querying`: “DAG terminal waiting is notification-driven”; `developer-facing-surface`: “Completion waits are notification-driven and race-free”, “Closed registration and start results retain a cast-free success path”; `quality-and-verification`: “Typed DAG execution is acceptance tested” | doc 17 §17.2.6 statuses, authored-order snapshots, typed handles and cancellation; AC-611, AC-616–618. |
| 8.7 | `runtime-resource-governance`: “Execution-path concurrency is host-owned”; `workflow-contracts`: “Host-facing execution hints remain optional and declarative”; `durable-runtime`: “Durable DAG progression is runtime owned”; `quality-and-verification`: “Typed DAG execution is acceptance tested” | doc 17 §§17.2.6 and concurrency-lifetime table; AC-609 and AC-528's DAG clause. `MaxConcurrentNodes` counts every admitted nonterminal child, including one parked on a wait, until terminal. |
| 8.8 | `repository-foundation`: “Infrastructure integrations are separate projects”, “Optional integrations are isolated”; `developer-facing-surface`: “Infrastructure integrations depend outward from OrcaCore”; `quality-and-verification`: “Infrastructure separation is architecture verified” | doc 17 package/companion boundary; document 14 JS-AC-016 and JS-001 project boundary. No Kubernetes/AWS SDK in an OrcaCore package. |
| 8.9 | `repository-foundation`: “Optional integrations are isolated”; `quality-and-verification`: “Typed DAG execution is acceptance tested”, “Infrastructure separation is architecture verified” | doc 17 DAG/companion journey; document 14 JS-004–JS-010 and JS-AC-006–JS-AC-015, JS-AC-018; AC-321 create-or-observe identity and AC-609/613 as applicable. Ordinary typed submit step, event wait, lease-held terminal verification, no generic external-job node. |
| 8.10 | `quality-and-verification`: “Typed DAG execution is acceptance tested”, “Completion waits are notification-driven and race-free”, “Package consumer smoke tests guard dependency experience”, “Infrastructure separation is architecture verified” | doc 17 §17.2.6, document 16 DR-AC-008, AC-606–AC-618, AC-321, AC-528's DAG clause, and JS-AC-001–JS-AC-018; provider restart/competing-host, unified-outbox AC-613, and lineage AC-614 must be executable before first-release exit. |

DU-033 partitions public events from internal continuations **and other
host/provider work**. The Task 8.0 interpretation is that the child-start/join
handoffs required by AC-613 and JS-002 are canonical *internal continuations*
of DAG progression: runtime-owned durable work rather than an authored event.
This is a category/ownership interpretation, not an assertion that a child
start uses the existing `Continue` record discriminator. The canonical durable
store's first-class internal continuations and AC-613's specific child-start
obligation therefore fit together without a new authoring capability. If an
8.5 design needs a public route, a second outbox, or a provider record outside
the canonical internal-continuation category, this interpretation no longer
applies and an approved canonical amendment must precede that source change.

### Document 14 numbered obligation handoff

Document 14 is normative even without a canonical counterpart. The task rows
above are supplemented by this complete JS-series routing; task 8.10 owns the
compiled acceptance proof for every criterion, including criteria whose
resource-governance substrate was delivered in Section 6.

| Requirement | Owning implementation task(s) |
| --- | --- |
| JS-001 | 8.2, 8.3 |
| JS-002 | 8.4–8.7 |
| JS-003 | 8.2, 8.3, 8.5, 8.6 |
| JS-004 | 8.9 |
| JS-005 | 8.9 |
| JS-006 | 8.9 |
| JS-007 | 8.9 |
| JS-008 | 8.9 |
| JS-009 | 8.9; Section 6 lease safety is prerequisite |
| JS-010 | 8.6, 8.9, 8.10 |

| Criterion | Owning implementation task(s); compiled exit owner 8.10 |
| --- | --- |
| JS-AC-001 | 8.2, 8.5, 8.10 |
| JS-AC-002 | 8.2, 8.3, 8.10 |
| JS-AC-003 | 8.5, 8.10 |
| JS-AC-004 | 8.6, 8.10 |
| JS-AC-005 | 8.3, 8.6, 8.10 |
| JS-AC-006 | 8.9, 8.10 |
| JS-AC-007 | 8.9, 8.10 |
| JS-AC-008 | 8.9, 8.10 |
| JS-AC-009 | 8.6, 8.9, 8.10 |
| JS-AC-010 | 8.7, 8.9, 8.10 |
| JS-AC-011 | 8.9, 8.10 |
| JS-AC-012 | 8.9, 8.10 |
| JS-AC-013 | 8.9, 8.10 |
| JS-AC-014 | 6.7, 6.10, 8.9, 8.10 |
| JS-AC-015 | 6.6, 6.10, 8.9, 8.10 |
| JS-AC-016 | 8.8, 8.10 |
| JS-AC-017 | 8.1–8.3, 8.10 |
| JS-AC-018 | 6.7, 8.6, 8.9, 8.10 |

### Other numbered criteria and waiver ownership

| Criterion | Section 8 owner and compiled exit, or explicit later owner |
| --- | --- |
| DR-AC-008 | DR-041 typed DAG driving: 8.4–8.6 implementation, 8.10 compiled exit; DR-P4, not DR-P3. |
| AC-321 | Durable `StepOperationId` substrate from 6.3, create-or-observe journey in 8.9, compiled exit in 8.10. |
| AC-528 | Root-workflow ceiling substrate from 5.7, separate DAG `MaxConcurrentNodes` and parked-child accounting in 8.7, compiled exit in 8.10. |
| AC-317 | Not a Section 8 DAG obligation. Section 7.10 provides a development-only provider role, but does not prove the outside-dev/test diagnostic or honest sample. Task 9.1 owns completing and compiling that evidence; 8.4 is unrelated. |

Every current acceptance waiver naming an 8.x task is accounted for above or
in the JS-AC table: AC-321, AC-528, AC-617, AC-618, DR-AC-008,
and JS-AC-014–JS-AC-018. JS-AC-007's only remaining trait is in a
compile-excluded legacy file, so the physical-file catalog sees that text
without a waiver; the Section 8 gate grants it zero credit. AC-317's stale 8.4 attribution is removed;
its 9.1 obligation remains open rather than being miscredited to Section 8.

### Executable acceptance-credit baseline

The legacy `RepositoryGuardTests.AcceptanceCriterionCatalog_HasTraitCoverageOrExplicitWaiver`
scans physical `tests/**/*.cs` without applying project `<Compile Remove>`.
It therefore cannot certify Section 8 acceptance. The independently checked
Section 7 declaration crosswalk classifies the actual source files: among the
34 Section 8 exit criteria DR-AC-008, AC-321, AC-528, AC-606–AC-618, and
JS-AC-001–JS-AC-018, none has a currently active compiled trait-bearing test
source. Twenty-four criteria have trait text only in compile-excluded sources;
ten have no trait source and remain pending; applicable waivers are not credit.
The removed JS-AC-007 trait on a
provider pool-capacity test proved no part of the Job watcher, durable ingress,
stable `EventId`, or wait journey. It is no longer counted as acceptance credit.

No 8.1–8.9 slice may cite a compile-excluded trait or the legacy catalog's
green result as executable acceptance. Task 8.10 owns replacing that catalog's
physical-file credit with compile-aware credit and adding compiled, behaviorally
reviewed evidence for **every** DR-AC-008, AC-321, AC-528, AC-606–AC-618,
and JS-AC-001–JS-AC-018 before
it can close. The Task 8.0 infrastructure gate reads the crosswalk's active
source classification, ignores comments and string-literal decoys, and
independently blocks every ledger-equivalent 8.10 completion with the full
list of missing compiled traits. This records the coverage debt rather than laundering
it as already complete; each earlier source slice remains subject to its own
independent tests and review.

The selected canonical and numbered obligations above agree under the stated
DU-033 interpretation at this gate.
This is a mapping decision, not a claim that those Section 8 behaviors already
ship or pass. In particular, AC-613 and AC-614 are mandatory, not deferred.

## Expected-red handoff

`tests/OrcaCore.DeveloperSurface.Guards/Fixtures/dag-contract-scenarios.json`
contains nine Task 3.10 scenarios. They remain `ExpectedRed`; this gate changes
no disposition. Their declared turn-green owners are:

| Scenario ID | Section 8 task(s) |
| --- | --- |
| `missing-duplicate-mapinput` | 8.1–8.3 |
| `valid-independent-node` | 8.2–8.3 |
| `opaque-direct-output-validation` | 8.3, 8.5 |
| `fixed-codec-input-once` | 8.3, 8.5 |
| `typed-registration-start-reopen` | 8.6 |
| `ordered-snapshot-output-wait-cancel` | 8.6 |
| `stable-child-reattachment` | 8.4–8.6 |
| `parked-node-admission` | 8.7 |
| `friend-and-child-opacity` | 8.1, 8.4, 8.10 |

Each disposition moves to must-green only with its owning implementation
slice's exact executable evidence and review; this gate does not relabel the
14 currently intentional expected-red scenarios as passing.

## Existing-package decision and review boundary

`src/OrcaCore.Dag/` and `src/OrcaCore.Dag.Hosting/` already contain their
approved project files and minimal source skeletons (six files total), and
both package IDs are already in the exact twelve-package manifest. Task 8.1
therefore means reconcile and fill those packages against the approved graph,
hosting role, and public baseline. It does not mean create second projects,
rename the package IDs, or expose child protocol prematurely.

Task 8.0 uses reshape's own dated review request, exact dirty manifest,
independent verdict, checkpoint, evidence, and activation convention. The
harmonization registry stays keyed to its completed change; it is not a
surrogate Task 8.0 authority. This mapping target must be independently
approved and checkpointed before any Task 8.1–8.10 source slice begins.
Archiving harmonization is separate and not a prerequisite.

The first 11-entry Task 8.0 freeze was independently **REJECTED** on
2026-09-26 for missing document 14/DU-033 handoff and false executable credit
from compile-removed tests. Its unchanged manifest and request remain in
`docs/review/`, and
`developer-facing-interface-section-08-task-8-0-requirement-gate-independent-review-verdict-2026-09-26.md`
is retained byte-exact as a separate immutable record. This remediated map
supersedes that uncommitted target; it does not rewrite the rejection or claim
that the first freeze was approved. The second 15-entry remediation freeze was
also independently **REJECTED** on 2026-09-26: four criteria were outside the
Section 8 handoff, and the 8.10 gate accepted equivalent checkbox forms and
comment-only traits. Its request, manifest, and verdict remain unchanged in
`docs/review/`; this target addresses those findings without authorizing 8.1.
