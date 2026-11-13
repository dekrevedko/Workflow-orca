# Task 2.1 protocol/provider errata and encoding disposition

Date: 2026-10-29. Proposed for the combined Task 2.1 source review, not independently approved.

## Preserved authority

The RC-3 contract, its API catalog/signatures, the canonical-transition addendum,
and the fingerprint amendment remain byte-exact history. The fingerprint amendment
is consumed through checkpoint `a6f02fa5be9c31db232e3582d2f77d35acf86639`, evidence
`3c2cd9606db6e4380b148c5f8dd3503e367f255f`, and its 2026-10-15 APPROVE verdict.
No canonical requirement, friend, public fingerprint factory, business codec,
new runtime pump, or Hosting bridge is added here.

## One additive lease outcome

The approved release port returns `DagRunLeaseResultV1`, but its outcome enum named
no successful release. Append `Released = 6` to `DagRunLeaseOutcomeV1`; preserve
Granted=0, Queued=1, OwnerBusy=2, StaleFence=3, NotFound=4, Terminal=5.
An exact release returns Released with the supplied fence, its retained queue
sequence and current stream version, and null expiry/deadline. Releasing that same
historical grant again returns Released without revoking a newer grant. A foreign
owner/request/token returns StaleFence; a missing run returns NotFound.

The candidate catalog records the sole additional key. Cumulative original-base
totals become **7 / 193 / 166 = 366**. The seven OrcaCore keys already shipped.
The codec type and its two methods were already among the approved 193 protocol
keys. An initial author concern that those three keys were omitted was disproved
by compiler extraction; there is no codec API/count erratum.

## Private implementation reuse

The three owning providers compile the same SDK-free private state/lease reducer
and checkpoint/operation framing sources. SQL providers additionally compile one
private DbConnection transaction implementation. These sources are excluded from
the Provider.Abstractions assembly, explicitly linked into the owning provider
projects, and included in package-source provenance. This introduces no public
helper, new assembly/package edge, friend, or reflection bridge.

SQL Server retains its existing single logical outbox in the transaction-locked
workflow state document. Its three new nullable OutboxWrite fields are persisted
there; there is no second transport or duplicate outbox authority. The six DAG
aggregate tables are relational. DAG heads/events/receipts/nodes and the existing
outbox document update share one SQL transaction and the existing workflow lock.
PostgreSQL adds those three nullable columns to its existing first-create outbox.
Both schemas remain complete first-create definitions, not ALTER migrations.
This is the physical-storage disposition of the contract's shared-outbox rule,
not permission to route a DAG record to an external dispatcher.

## Deterministic operation framing

For the otherwise abstract operation-fingerprint recipe, this implementation
uses fixed-order UTF-8 JSON without BOM. The root has records then outbox arrays.
Each record uses the closed DagRunRecordCodecV1 projection, recursively omitting
streamVersion, createdAt, updatedAt, completedAt, readyAt and startedAt. Logical
operation/run identity, graph, binding, business bytes, kinds and outcomes remain.
Outbox items sort ordinally by OutboxRecordId text, each with id, kind, payload
(base64), owner, recipient, subscription in that order, including explicit nulls.
The lowercase SHA-256 binds those exact bytes. Thus retries can change expected
version/fence and observation timestamps but not the operation's business intent.
The public provider tests independently construct this framing rather than
calling provider-private hashing/reduction helpers.

The checkpoint frame uses aggregateKind, version, runId, streamVersion, snapshot;
its source-generated closed snapshot projection is hashed as exact UTF-8 bytes.
Unknown, duplicate, missing and noncanonical fields fail closed, as do invalid
discriminators, graph/value hashes and inconsistent run/node coordinates.

## Deliberate boundary

This review combines the remaining protocol and all three provider data planes,
the Released erratum, and the carried round-100 findings. Task 2.2 runtime pumps,
Task 2.3 Hosting bridge/evaluation scheduling and their runtime certification rows
remain separate work. The existing durable diagnostics snapshot also projects
the seven approved DAG ObservableGauge names with the existing provider-name and
state tag keys; no public diagnostic constant or per-run tag is introduced. The
total business-outbox gauge includes continuation, internal DAG and external work,
excluding addressed observers. Provider pressure fields and BCL listener assertions
are not evidence that the not-yet-implemented pumps or bridge work. Task 1.4 is locked
complete; source/review/final-closeout checkboxes retain their existing authority.

The existing external pump and its hosted claim selector now exclude all four
reserved DAG kinds. Even a caller selecting them explicitly, or a provider ignoring
the selector, cannot send them to IMessageDispatcher: the pump releases the claim
without acknowledging it. This minimum fail-closed routing inclusion prevents
data loss between the provider checkpoint and Task 2.2. It implements no new pump,
child start/join/cancel delivery, ready sink, DAG-role registration or bridge.
