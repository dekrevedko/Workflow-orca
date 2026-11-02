# Task 2.2 dedicated DAG pumps and certification hardening

Date: 2026-11-01. Status: prepared source target; independent approval and checkpoint pending.

## Authority and boundary

Base is Task 2.1 evidence commit `602844547539e0e5908d250c2a0d9560fc4b25dc`.
Its only parent is checkpoint `f62b0aafd8beeb69aa28efaba0f25413c4970b3b`, tree
`0e5300632b9804f096ac45d119a602b5b9f5b9c7`, whose only parent is
`0a8e375fbcf5005a2a2f491cf24dfbbd9d64c580`. The 2026-10-29 protocol/provider
APPROVE is 8,809 bytes, SHA-256
`c971876e87d871f5fce5b48bc685a918f7d2e1dd8b6dd81869068c4bf9d5584b`.
The evidence commit adds that verdict, catalogs it and clears the freeze, without activation.
The old proposed-candidate registry object and every old packet remain unchanged history;
the new separate approval binding records what actually landed.

This target implements the every-durable-engine-host child lane and the guarded ready
claim seam from Task 2.2, together with the three carried provider-certification gaps.
It changes no public API, friend graph, canonical specification, delta, provider schema,
business codec or caller start-key namespace. Tasks 2.1–3.2 and reshape 8.4/8.5 remain open.
No commit, activation, publication or next-task work is authorized by this artifact.

## Lane ownership

`DurableDagPumpHostedService` is installed by `AddOrcaCoreDurableEngine`, regardless of
`IMessageDispatcher` or the DAG role. Definitionless callback-only ingress still owns no
progression services. The child selector includes only `dag-child-start-v1`,
`dag-child-join-v1` and `dag-child-cancel-v1`. A foreign record is released before any
payload read, callback, poison or successful acknowledgement. The existing continuation
consumer now performs that same early kind check. The external selector intersects every
custom selector with its reserved-kind exclusion; the shared workflow-event adapter has
an explicit internal workflow-event-only lane, not a widened external dispatcher.

The ready selector includes only `dag-run-ready-v1`, with null-or-local boot-host recipient.
Without an `IDurableDagReadySinkV1`, there is **no ready claim call**. No no-op sink is
registered. The prepared consumer validates trusted run/recipient/subscription coordinates,
the closed payload and authoritative checkpoint before calling the sink, then acknowledges
only accepted wake/hub delivery. Sink/storage failure releases the hint, not data poison.
Corrupt hint data is visible poison, never run success or admission release.

The **real Dag.Hosting scheduler/sink, observer hub, continuous wrapping discovery and
full run bridge remain Task 2.3**. Until that capable sink is installed together with
rediscovery, production hosts leave ready hints claimable. Tests supply an owned-boundary
sink to prove the shipped ready consumer; this is not end-to-end public DAG execution or
DG-P22–24 acceptance credit. No mapper is invoked by this target.

Observer pruning runs independently of child/ready processing, with a positive bounded
batch and ten-second cycle. The service owns and joins all three loops. Existing hosted
failure boundaries pace transient retries; an uncertain outbox claim remains recoverable.

## Deterministic child recovery

Engine.Durable alone touches compiled workflow metadata and the fixed-codec checkpoint.
Durable.Hosting owns the provider FIFO and run join/fault coordinator. No reverse package
edge or new friend is needed. An internal source-generated child signal contains exactly
`version`, `runId`, `ordinal`; the trusted owner remains outside its opaque payload.
Version 1 and canonical bytes are required before processing.

The existing approved run-GUID/ordinal-only SHA-256 recipe selects the child stream.
Start/reattach checks the exact workflow/version/fingerprint, declared input/output
contracts, input presence/format/bytes/hash and parent/root anchor. Type contract keys are
opaque uppercase SHA-256 of the installed fixed-codec schema/type identity, never a
dynamic type-loading instruction. An unavailable exact definition retries; a conflicting
binding is an integrity fault. No caller start key is used. Present-null input remains
explicit committed bytes. Creating a child commits its ordinary continuation but never
runs user steps inside the child claim or a run lease turn.

The child terminal transaction appends its join intent atomically with terminal facts,
output/checkpoint and projections. The parent run is a separate aggregate: there is no
cross-stream atomicity claim. A join re-reads authoritative child facts and exact binding,
then takes the shared local-arrival/provider FIFO turn, rechecks the current run and commits
one ChildJoined fact plus any required Terminal fact. Successful joins ready eligible
dependencies; non-success blocks all reachable Pending/Ready descendants in linear graph
time, preserves independent work and derives the run outcome. Duplicate/lost-response joins
verify the already-committed outcome and cannot free admission twice.

Cancellation consumes an already-committed run barrier. It materializes an authorized
missing child before delivering cancellation; the run retains admission until authoritative
terminal join. Malformed signals and binding conflicts fence a visible IntegrityFault before
poison, preserving every uncertain child/input/admission. Failure to commit that fault leaves
the transport retryable. A corrupt/absent trusted owner is quarantined visibly, never success.

The engine writes the existing checkpoint/operation framing independently; the providers'
approved reducer validates every transition and compares the supplied checkpoint before
commit. No provider-private source is linked into Hosting or Engine. Provider certification
keeps its own independent wire builder. Shipped API baselines remain unchanged.

## Carried certification gaps

Three separately discovered failing cases run unchanged through all three providers:

1. A dead FIFO head expires after thirty seconds while a younger live waiter can still obtain
   the next fence. Removing waiter expiry makes this case fail, not merely a timing assertion.
2. A ChildJoin turn attempts InputAdmitted. The failure must be DAG_PROTOCOL_INVALID and leave
   tail, checkpoint and outbox unchanged. Removing purpose validation makes the case fail.
3. The last node joins without a Terminal fact. The rejected append must leave the old head
   and no new events, then the same turn can atomically join plus terminalize. Removing atomic
   closure makes this case fail.

Positive and isolated-mutation runs cover InMemory, real PostgreSQL and real SQL Server.
The dated review request records exact reports, counts and mutation dispositions. These
tests strengthen correct existing rules; they do not change the approved lease/data plane.

## Evidence maintenance

The Task 2.1 source-set assertion now checks its actual immutable checkpoint rather than
claiming future pumps are part of that old no-pump target. A new source-pinned Task 2.2
candidate binds this artifact and its exact live source set, without self-approval.
The CLAUDE.md sentence is updated from typed-identity-only to the checkpointed data plane
plus this pending pump slice. Its Task 7.3 row/digest and existing block pin are refreshed in
the same target. Declaration accounting and package source provenance are recomputed, not
relaxed; historical current-match pins are refreshed to their exact maximal sets.

Final validation, copied-index tree/blob checks, clean committed rehearsal and cleared-freeze
evidence rehearsal belong to the combined review request. Main stays at the evidence base
with this whole target uncommitted. There is no Task 2.2 activation checkbox.
