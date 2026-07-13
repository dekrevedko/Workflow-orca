# OT1-03b: PostgreSQL start reservation

**Difficulty**: Haiku        **Depends on**: OT1-03a — pure provider work; may run in
parallel with OT1-03/04/05/06; must complete before Phase O1 exit
**Spec**: OE-042 (durability layer), PR-024        **AC**: none new (certification traits from OT1-03a)

## Goal
The PostgreSQL provider implements the atomic start-reservation semantics introduced by
OT1-03a, proven by inheriting the certification tests added there — the reservation joins
the provider's existing start-commit transaction.

## Read first
- The port/materialization shape landed by OT1-03a (its PROGRESS.md line names the files;
  read the port + materializer, ≤2 files)
- `src/OrcaCore.Providers.PostgreSql/` — the event-store commit transaction (locate
  the append/commit implementation; read ≤2 files)
- `tests/OrcaCore.Providers.PostgreSql.Tests/` — how the certification suite is
  inherited (read the suite-inheriting class, 1 file)

## Deliverables
- Reservation persistence inside the provider's existing commit transaction (same
  transaction as events/checkpoint per PR-020 — not a second round-trip), plus the
  reserve-or-return read path. Schema addition (table or column) follows the provider's
  existing migration/DDL pattern.

## Tests to write FIRST
- None new: the OT1-03a certification tests (`StartReservation_ConcurrentReserves_OneWinner`,
  `StartReservation_IsAtomicWithStartCommit`) must go green via the inherited suite
  (Testcontainers). Add a provider-local test only if a PostgreSQL-specific edge appears
  (e.g. unique-violation mapping to the winner-read path).

## Implementation notes
- Raw Npgsql per stack decisions; a unique constraint on the idempotency key with
  ON CONFLICT winner-read is the expected shape — but follow the OT1-03a-approved port
  contract, not this hint, if they differ.

## Out of scope
Any port/materializer change (escalate to the gate if the shape doesn't fit PostgreSQL);
other providers.

## Definition of done
- [ ] Inherited certification suite green against Testcontainers PostgreSQL
- [ ] Full `Providers.PostgreSql.Tests` suite green; zero warnings
- [ ] PROGRESS.md updated; committed as "OT1-03b: PostgreSQL start reservation (OE-042)"
