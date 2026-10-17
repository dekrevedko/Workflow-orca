## Why

[GitHub issue #1](https://github.com/dekrevedko/Workflow-orca/issues/1) identifies a missing lifetime contract for resident ephemeral hosts: completed runtime instances, facade bindings, and start-idempotency keys currently have no supported bounded retention policy. An opt-in policy must bound these owned records together without forgetting active or physically unsettled work or changing the current default.

## What Changes

- Propose an optional immutable `EphemeralTerminalRetentionPolicy` on `EphemeralEngineHostOptions`, with a host-wide maximum retained eligible-terminal count, maximum age, and sweep interval. Omission preserves host-lifetime retention.
- Treat runtime state/output, facade instance binding, and its start-key binding as one retention unit; preserve definition registrations. Eligibility requires an immutable terminal status plus complete runtime-owned physical settlement and drainage of owned callbacks/commits.
- Specify deterministic count/age eviction, typed not-found behavior for expired handles, and the exact boundary of start-idempotency guarantees. Eviction never signals that application-owned external effects stopped.
- Define query/output-wait races and coordinated start/retention publication, including synchronous completion before a facade binding is published.
- Add contract scenarios, deterministic race/settlement test tasks, and a fresh package-only consumer retention experiment covering payloads and metadata.
- Keep public archive/purge/evict/history operations, durable retention, cooperative host shutdown (issue #2), and AOT qualification (issue #3) outside this change.

This is the R1 remediation of the rejected proposed amendment, not an approved API or product-source authorization. Its complete provenance fixture/artifact/inventory/source-pin prerequisite belongs in this contract target. No duplicate owner or separate process amendment is involved; the five additions remain semantically pending until approved synchronization.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `developer-facing-surface`: propose the exact ephemeral-only policy signatures and validated immutable host-options ownership without a management API or additional friend edge.
- `management-and-querying`: define automatic terminal retention as a host policy, its start-idempotency boundary, and stale-handle/query/wait behavior while ordinary destructive management stays deferred.

## Impact

The source baseline remains deliberately pinned to the original synced `master` commit `370b239402c8974a45fc0a54f539a823ab470cf4`; current GitHub `master` is newer and is recorded separately in the R1 request. Existing code ownership is `src/OrcaCore.Engine.Ephemeral/EphemeralEngineHostOptions.cs`, `Hosting/OrcaCoreEphemeralEngineServiceCollectionExtensions.cs`, `Facade/EphemeralWorkflowFacade.cs`, `EphemeralWorkflowEngine.cs`, the instance registry, timers, and physical-attempt/governance lifetime paths. Product source and executable API baselines remain unchanged in this contract target; only exact provenance guard/fixture metadata changes alongside the draft.

Before implementation, reconcile numbered specs 03/04/09/10/11/12/13/15/17, both canonical capabilities, affected guides, public API/compile/package guards, and the machine-readable post-gate/provenance records. Explicitly exclude the authoring-only declaration companion, which has no hosting-options declarations. Design contains target-by-target dispositions; the deltas retain five uniquely named requirements without replacing existing owner blocks. The contract target includes every provenance prerequisite; the canonical transition carries signature text and later permanent records; executable API/compile/package baselines move with implementing source slice 4.2. Aborted-start fencing/cleanup and the possible loss of a completed result between start return and wait admission are normative scenarios and required deterministic tests. Contract review, canonical transition, source review, and final published-consumer qualification remain separate gates. Issues #2 and #3 start only after issue #1 is completed through its approval/checkpoint process, each from a fresh branch of the then-current GitHub default branch.
