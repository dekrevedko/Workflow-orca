# Task 2.1 protocol/provider implementation and review boundary

Date: 2026-10-29. Candidate implementation, not independent approval.

## One larger, bounded review

Base: `0a8e375fbcf5005a2a2f491cf24dfbbd9d64c580`, after the independent
stream-identity, package-advisory and fingerprint-amendment checkpoints and the
merged native-metadata and preview.3 publication/release-gate side work. The DAG
branch was fast-forwarded from `82811cd` without creating a DAG checkpoint;
its uncommitted work and unrelated SQLite planning were preserved. This target combines the remaining protocol
and three provider data planes with the dated Released erratum. It does not
implement Task 2.2 pumps, Task 2.3 Hosting bridge/evaluation, role registration,
public DAG execution or their end-to-end certification. Tasks 2.1–3.2 stay open.
Unrelated `add-sqlite-durable-provider` planning and preview.3 release work are
excluded; no publication, push, archival or checkpoint is authorized by this file.

## Implementation trace

| Boundary | Implementation and permanent evidence |
| --- | --- |
| Opaque fingerprint rehydration | Protocol records store canonical uppercase 64-character text. No new constructor, factory, friend, reflection or codec permission. `DagStreamIdentitySliceGuards` binds the actual fingerprint checkpoint, its tree and direct-child verdict addition. |
| Closed framing | `DagRunRecordCodecV1` validates eight record kinds, fixed field sets/order, coordinates, graph acyclicity and committed-byte hashes. Core codec tests exercise unknown/duplicate/missing/noncanonical frames, defensive copies, mutated init properties and unrepresentable storage text. |
| Atomic data plane | `DagRunStoreRules` independently reduces every proposed record and compares the supplied checkpoint with the result before persistence. One fact plus optional terminal closure is allowed per turn; creation is the only unfenced append. Compact operation receipts bind logical intent and make exact replay independent of a subsequently expired fence. |
| Persistent ownership | Six first-create DAG tables, separately discriminated stream identity, exact start-key digest plus text collision check, immutable graph/input and per-run creation sequence. PostgreSQL uses a per-identity transaction lock; SQL Server participates in its existing workflow/document transaction lock. Both use real database time after acquiring locks, with instance-local controlled clocks for deterministic storage tests. |
| Fencing | Stable FIFO request identities, one live request per owner/run, five-second renewable lease, fifteen-second absolute turn deadline, one committed mutation, stale version/fence rejection, cancellation/expiration and idempotent release. The only catalog addition beyond the fingerprint refinement is `Released = 6`. |
| State reduction | Authored-order admission, deterministic run-GUID/ordinal child identity, independent branch progress, transitive failure blocking, cancellation barrier, integrity/poison reconciliation and terminal closure. Cross-aggregate GUID collision is rejected in either creation direction. |
| Notifications and maintenance | Addressed ready notices are isolated from unaddressed business records; observer expiry/unsubscribe prunes only eligible unclaimed non-poison observer notices. Claimed notices, poison and business records are retained. Recovery discovery is creation-sequence ordered and paged; it does not scan historical events. |
| Pressure | Internal DAG, observer and external records remain separate in every outbox state. DAG heads/checkpoints contribute to aggregate counts. Seven existing approved observable-gauge names project the snapshot with existing low-cardinality tags; no new public metric key or mapper-thread gauge API is added. |
| Fail-closed external lane | The existing external pump excludes the four reserved DAG kinds and releases any wrong-lane result even when explicitly selected. It never acknowledges them or sends them to `IMessageDispatcher`. This safety rider creates no new pump. |

`DagRunCertification` builds wire records, checkpoints and operation hashes without
calling provider-private builders. The same eight behavioral checks run against
InMemory, PostgreSQL and SQL Server: creation/replay/discovery, fenced append and
observers, FIFO/deadlines, failure closure, observer maintenance, cross-instance
concurrent lease acquisition, cancellation/integrity, and cross-kind identity.
The last two native providers use real containers, not substitutes.

These are data-plane contributions to DG-P01–05/10–12/17–18/20. They are **not**
claims that whole DG-P rows are complete: runtime crash interleavings, child
materialization, scheduling, slow/nonterminating mapper isolation, role-free
delivery, shipped Hosting behavior and DG-P06–09/13–16/19/21–24 still require the
later reviewed runtime/bridge targets. The fourteen ExpectedRed scenarios remain.

## Carried review findings

- Task 1.4 must be complete; its checkbox is no longer normalized away.
- Workflow-only append rejection is tested with observable inbox/outbox effects,
  a global event count and an already-cancelled token. The test does not merely
  inspect an unrelated workflow stream after a late rejection.
- `CLAUDE.md` names the completed transition checkpoint/evidence/activation rather
  than claiming independent review is still pending. Its Task 7.3 row and digest
  are refreshed together.

## Guard ownership after the merged side work

Older amendment guards still compare every unrelated registry field. A narrow
projection permits only this separately checked DAG candidate and its actual
fingerprint approval. The new guard pins the complete changed/new `src/` path set,
the catalog/signatures/erratum and this disposition, plus the exact private source
links and exclusions. New public baselines are compared against compiler output.

The native codec guard retains byte pins on its three codec files and harnesses.
It verifies all recorded qualification-source hashes against their actual Git
commit, and retains live pins on the application codec/Core/ephemeral boundary.
Later provider/protocol source does not inherit native qualification. This removes
an accidental freeze of all future `src/` edits without altering the approved
native packet, historical source, release authority or qualified feature scope.

Private reducer/framing sources are compiled into their exact provider owners,
not exported from the contract assembly. The declaration crosswalk and the
production compile-exclusion ledger disclose those links. SQL Server's existing
logical outbox remains in its transaction-locked state document; the erratum names
this physical disposition explicitly for review rather than claiming an additional
relational outbox or transport.

## Validation authority

The review request records final commands, counts, focused mutation diagnostics,
content conventions and tree anchors. Passing those checks is execution evidence,
not approval of this candidate, its erratum, future native qualification or a
release. No completed task, immutable contract, old verdict or canonical spec is
rewritten by this target.
