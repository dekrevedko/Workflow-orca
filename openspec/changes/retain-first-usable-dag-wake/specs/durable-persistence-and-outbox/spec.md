## ADDED Requirements

### Requirement: Addressed DAG observers retain their first usable wake
Each provider SHALL maintain at most one live provider-generated addressed ready notice per run, boot recipient and subscription. A successful registration or renewal at the committed head SHALL store its AfterVersion cursor and lease atomically with the version check. A behind-head request SHALL return AlreadyChanged with the current head and preserve the stored cursor and expiry. An ahead-of-head cursor or conflicting owner SHALL be rejected.

An append crossing a live subscription cursor SHALL atomically preserve its first Pending, Retryable or Claimed generated notice whose version is strictly greater than that cursor. Preservation SHALL retain the ID, payload, state, dispatch attempt and lease. If the live notice version is at or behind the cursor, the append SHALL reuse its ID, replace its version and payload with the committed version, reset it to Pending, clear its lease and preserve its last attempt. The old claim SHALL fail acknowledgement, release and poison both before and after reclaim. In the absence of a live notice the append SHALL create a fresh Pending notice and preserve dispatched and poisoned history.

Only provider-generated addressed notices qualify. Explicit writes, unaddressed progression and child work SHALL retain their independent delivery semantics. Replaying a committed operation SHALL produce no further wake. A collision with a generated candidate ID SHALL fail the entire append even when the notice would be retained. Expired or unsubscribed registrations SHALL generate no new notice. Delivery SHALL wake only the addressed observer for an authoritative read of the latest checkpoint.

#### Scenario: Cursor renewal wins before a concurrent append
- **WHEN** a claimed notice at version 2 exists, the subscription advances its cursor to head 3, and an overlapping append commits version 4
- **THEN** the version 4 notice replaces the obsolete claim under the same ID and only a new attempt can settle it

#### Scenario: Append wins before a concurrent cursor renewal
- **WHEN** a version 2 notice is claimed for cursor 1 at head 3 and an overlapping append reaches version 4 before renewal at version 3
- **THEN** the renewal returns AlreadyChanged(4), the original cursor and lease remain in force, the claim succeeds and the observer can read version 4

#### Scenario: Delivery and append serialize
- **WHEN** a useful claimed notice and a later append overlap
- **THEN** settlement first leaves historical evidence and causes a fresh notice, while append first preserves the useful claim for settlement

#### Scenario: Caller writes share a recipient
- **WHEN** an append includes an explicit addressed write for the same registration as a generated notice
- **THEN** the explicit write has its own delivery and identity, and a collision with the generated identity rolls back the append
