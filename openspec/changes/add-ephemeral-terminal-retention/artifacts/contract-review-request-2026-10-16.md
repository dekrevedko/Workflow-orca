# Issue #1 proposed retention contract — independent review request

Target: `issue/1-ephemeral-retention`, baseline `370b239402c8974a45fc0a54f539a823ab470cf4`. This request covers draft contract/routing only. No independent reviewer has been dispatched and no approval is implied.

## Exact draft scope

All files are below `openspec/changes/add-ephemeral-terminal-retention/`:

1. `.openspec.yaml`
2. `proposal.md`
3. `design.md`
4. `tasks.md`
5. `specs/developer-facing-surface/spec.md`
6. `specs/management-and-querying/spec.md`
7. `artifacts/proposed-amendment-route-2026-10-16.json`
8. `artifacts/initial-assessment-2026-10-16.md`
9. `artifacts/contract-review-request-2026-10-16.md` (this request)

Before issuing a binding verdict, freeze the self-inclusive raw Git manifest and exact content hashes for this complete scope, recheck them after review, and append the new verdict without rewriting this request. HEAD, ordinary index, existing registry fixtures, canonical specs, public declaration companion, product source, and test source must remain unchanged while review is pending. This request does not assert that its scope has already received a binding external freeze/verdict.

## Reviewer decisions

- Accept or reject the exact policy signatures/default/validation in design decision 1; distinguish a host policy from deferred public destructive commands.
- Verify eligibility proves per-instance physical settlement on every dispatch path, including no-pool/no-throttle attempts, and never equates terminal status or task return with independently proven external-effect cleanup.
- Verify count/age limits, monotonic timestamps, count pressure ending retention earlier than maximum age, transient query/wait pins, and idle cleanup have precise implementable boundaries.
- Check complete runtime/facade/key/payload retirement, synchronous completion before facade publication, exceptional provisional starts, stale handles, caller-owned copies, wait notification races, and key reuse against both normative trees.
- Check all five exact added requirement identities and proposed routing. If any existing requirement must change, require its exact reviewed successor process before reviewing that amended block; no generic owner exception is proposed.
- Verify the phase ledger separates contract approval/checkpoint, atomic canonical/registry transition, test-first source slices, and fresh package-consumer qualification. All issue acceptance criteria remain open.

## Reverse occurrence dispositions

The sweep used `retention|idempotency|not.found|evict|archive|purge|WaitForOutputAsync` across every numbered spec/companion, all canonical capabilities, and affected active guides. The following groups classify all matched files; design's target table additionally records source-map owners with no exact-keyword hit.

| Matched files | Disposition |
|---|---|
| docs 03/04/09 | Owning/cross-capability lifetime, committed-state/output, terminality and safe-eviction vocabulary; reconcile during approved transition |
| docs 10/11/12/13/17 matrix + companion | Package/API/default ownership, resource limits, acceptance references and future-capability record; amend only the approved narrow surface/behavior |
| docs 01/02/05/06/08/14/16 | Cross-capability context: selected-mode/deferred-management, durable residency/retention, events, physical effects and scheduler/DAG semantics remain unchanged; qualify broad lifetime wording if review finds it could describe ephemeral policy |
| docs specs README | Reading/provenance links only; no behavior amendment |
| canonical developer-facing-surface, management-and-querying | Exactly five uniquely named additions; existing owner blocks preserved |
| canonical workflow-contracts, workflow-authoring, structured-fiber-execution, runtime-resource-governance | Typed output waiting, codec/idempotency, logical/physical capacity and scope invariants preserved; test references apply |
| canonical durable-runtime, durable-persistence-and-outbox, event-routing-and-waits | Durable start/inbox/outbox/activation/tombstone retention remains unchanged; ephemeral policy grants no durable cleanup authority |
| canonical quality-and-verification | Burden-of-proof and provider certifications remain; no deleted legacy eviction/retention test is resurrected as an ordinary application API |
| ephemeral guide, project technical overview, production readiness | Update opt-in examples and lifetime boundaries after contract approval; preserve current published behavior descriptions until then |

The no-hit numbered document 15 still owns observability via the source map and must distinguish retained/protected/pinned counts without instance/key metric labels. Other no-hit canonical capabilities (repository-foundation, state-driven-runtime, saga-orchestration, event-driven-prototype) need no proposed requirement changes. The exact public host option belongs to the existing ephemeral package; package direction and codec/friend allowlists remain intact.

Guard fixtures were inspected for provider-retention deletion lineage, terminal/eviction crosswalk rows, public call shapes and provenance. Existing `provider-retention-capability-replacement` evidence belongs to durable provider/operator maintenance; this amendment does not restore its deleted public archive/purge types. Baseline guards/fixtures remain byte-exact.

## Validation and decision boundary

See the initial assessment for 22/22 strict structural passes, the focused expected 184-versus-189 provenance failure, exact candidate row/byte/SHA-256 inventory, and five unresolved additions. The proposed route is deliberately unactivated and not a substitute fixture schema. A structural pass is not semantic approval; no passing full infrastructure/product lane is claimed.

Return an explicit APPROVE or REJECT for the exact frozen contract/routing target, findings with locations, and the precisely authorized next gate. Source implementation, canonical synchronization, a checkpoint, push/merge, issue completion, and starting issue #2 are not authorized merely by this request. If process/fixture preparation must precede contract approval, return that narrower prerequisite explicitly rather than approving an incomplete source target.
