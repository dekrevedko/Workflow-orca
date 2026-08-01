Authoritative contract: [`reshape-developer-facing-interfaces`](../reshape-developer-facing-interfaces/proposal.md).
This change carries no new semantics of its own. It propagates already-approved reshape decisions
into the capability specs that the reshape's delta-scoped canonical synchronization did not reach.

## Why

`reshape-developer-facing-interfaces` task `10.14` synchronizes *"every approved **delta** into
canonical OpenSpec specs."* The task is delta-driven, so a capability with no delta directory is
invisible to it. Eleven of fourteen canonical specs were synchronized; three were never opened and
remained at the pre-reshape baseline `ba2478e`, where they still assert behavior the approved v1
contract removes. A fourth, already-synchronized spec retained a removed term inside a requirement
the sync did process.

Because canonical specs are normative, the repository currently states two different answers to the
same questions: whether events buffer before wait registration, whether definition-scoped fanout
exists, whether ambiguity is resolved at match time or rejected at registration, and whether the
payload codec is replaceable. The Section 7 independent exit review is premised on a coherent
canonical baseline, so these must resolve before that review concludes.

No reverse check exists today that asks, for every canonical spec, whether it still holds under a
newly approved contract. Adding one is part of this change.

## What Changes

- Replace pre-registration event buffering with the approved non-buffering signal-stream contract.
  Delivery to a live instance with no matching active wait returns `NoActiveWait` and does not
  consume the `EventId`, so the caller may redeliver. The closed public delivery result stays
  `Accepted`, `Duplicate`, `NoActiveWait`, `InstanceTerminal`, `EventConflict`, which admits no
  buffered, pending, or deferred-match value.
- Replace three-mode event routing with the approved two route names and four overloads.
  Definition-targeted fanout, broadcast delivery, and an ambiguous-match delivery result are absent
  from v1 and remain in the future-capability registry.
- Replace match-time ambiguity resolution with registration-time rejection. A second active wait for
  an already-active `(DefinitionId, EventName, CorrelationId)` triple fails with
  `AmbiguousWaitRegistrationException` before parking, which makes the competing-wait state
  unreachable and removes the need for a tie breaker. `WaitSequence` is retained as a persisted
  registration ordinal for ordering, recovery, and audit, and is explicitly not a match-time selector
  and not application-visible.
- Remove `WhenFirst` from wait-cleanup semantics; cleanup is driven by cancellation, failure,
  workflow deadline expiry, forced termination, and scope abandonment.
- Replace the replaceable payload-envelope and schema-resolution abstractions with the
  non-replaceable certified `orcacore-json-v1` codec. No serializer hook, envelope abstraction,
  schema-resolution SPI, or hosting override exists.
- Scope retention and purge to the provider and operator tier and state their absence from the v1
  application surface.
- Tier `IWorkflowStore` and `IMessageDispatcher` to `OrcaCore.Provider.Abstractions` and name
  `IDurableResourceGovernanceStore` as the separate expected-version append contract. These ports
  were the only provider contracts left without a tier or owner statement.
- Record the event-driven prototype as explicitly outside the first release. No prototype project
  exists under `src/`, and `repository-foundation` declares the first-release project list
  exhaustive without it. Future work requires a separate reviewed capability amendment.
- Remove the residual `WaitLong` citation from ephemeral-mode limitations and restate wait residency
  as a runtime and hosting policy, with the accurate durable-only capability set.
- Add a reverse-direction canonical-consistency step so a future change enumerates every canonical
  spec, not only its own deltas, before its synchronization gate closes.

## Capabilities

### Modified Capabilities

- `event-routing-and-waits`: Non-buffering delivery, two-route targeting, registration-time ambiguity
  rejection, and `WhenFirst`-free wait cleanup.
- `durable-persistence-and-outbox`: Fixed certified codec, provider-tier store ownership, and
  operator-tier retention.
- `event-driven-prototype`: Recorded as outside the first release with no v1 obligation.
- `state-driven-runtime`: Ephemeral limitations restated without the removed `WaitLong` concept.

## Impact

- Normative only. No source file is modified by this change. The code findings discovered while
  auditing these specs are recorded in
  `docs/review/developer-facing-interface-cross-capability-consistency-record-2026-07-31.md` and
  remain owned by `reshape-developer-facing-interfaces` tasks `7.17` and `7.19`, except the public
  definition-scoped fanout route, which no open task currently covers.
- The reshape's Section 7 freeze is unaffected by this change. Its deltas, tasks, and manifest are
  not modified here.
- `docs/specs/` has now been audited. It is substantially healthier than the OpenSpec tree: every
  removed-vocabulary occurrence is a negative guard or deferred-registry entry, and the non-buffering
  delivery rule, two-route targeting, wait projection, package split, and codec decision are all
  already correct there. On the four semantics harmonized above, **`docs/specs` held the right answer
  and the OpenSpec capability specs were the stale side.**
- Two behavioral models introduced by the 2026-07-28 amendment reached the OpenSpec specs and the
  capability matrix but never reached the numbered requirement files: the authoring-session
  lifecycle and `WorkflowFailure` occurrence provenance. Both are implemented (reshape tasks `4.16`
  and `5.11`), both are normative in OpenSpec, and neither has a requirement ID or acceptance
  criterion in `docs/specs`. Because `docs/review/README.md` judges code against both trees, these
  models are currently unverifiable from the acceptance-criteria side. Section 6 of `tasks.md` owns
  the correction; no `docs/specs` file is edited by this change.
