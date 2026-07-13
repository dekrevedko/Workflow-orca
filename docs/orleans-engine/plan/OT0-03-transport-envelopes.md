# OT0-03: Transport envelopes for grain calls

**Difficulty**: Sonnet        **Depends on**: OT0-02
**Spec**: OE-060 (via OE-002: PR-016)        **AC**: OE-AC-041

## Goal
The serialization boundary: Orleans-serializable envelope records that carry any durable
command (and its result) across a grain call as STJ JSON, keeping Orleans attributes out of
domain assemblies.

## Read first
- `v3-gpt/src/OrcaCore.Engine.Durable/Execution/DurableCommandProcessor.cs` — only the
  `StartWorkflowCommand` and `DeliverEventCommand` overloads and those two command types
  (the processor has ~30 overloads — the codec covers ONLY the v1 kinds below; later
  phases add kinds as they need them)
- `v3-gpt/src/OrcaCore.Abstractions/Providers/ProviderPorts.cs` — `IWorkflowPayloadSerializer`
- Existing STJ setup: the serializer/context types used by `Engine.Durable` for event
  payloads (locate from DurableCommandProcessor's usings; read at most 2 files)
- [01-architecture.md](../01-architecture.md) §4

## Deliverables
In `v3-gpt/src/OrcaCore.Engine.Orleans/Transport/`:
- `WorkflowCommandEnvelope` — `[GenerateSerializer]` record with explicit `[Id(n)]` on
  EVERY member (stable forever; add-only evolution): `[Id(0)] InstanceId (Guid)`,
  `[Id(1)] SchemaVersion (int)`, `[Id(2)] CommandKind (string discriminator)`,
  `[Id(3)] CommandJson (string)`.
- `WorkflowCommandResultEnvelope` — same `[GenerateSerializer]`+`[Id(n)]` discipline,
  mirroring `DurableCommandResult` outcome data as JSON + discriminator + schema version.
- `WorkflowCommandCodec` (internal, static or injectable) — `Encode(command)` /
  `Decode(envelope)` for the **v1 kinds only: `StartWorkflowCommand`,
  `DeliverEventCommand`**; `Encode/DecodeResult` for results. The codec is add-only by
  design: O2 adds `FireTimerCommand`, O3 adds management kinds — each phase extends the
  switch and the round-trip test list in its own task. STJ only; explicit discriminator
  switch — no reflection-based polymorphism. Decode fails fast with diagnostics on unknown
  kind AND on `SchemaVersion` newer than supported.

## Tests to write FIRST
In `v3-gpt/tests/OrcaCore.Engine.Orleans.Tests/Transport/CommandCodecTests.cs`:
1. `EveryCommandKind_RoundTrips` — for each v1 command kind: encode → decode →
   value-equal (or field-equal) to the original. (Later phases append their kinds here.)
2. `Result_RoundTrips` — a representative `DurableCommandResult` round-trips.
3. `UnknownKind_Throws` — decoding an unknown discriminator fails fast with a clear message.
4. `NewerSchemaVersion_FailsFast` — envelope with `SchemaVersion` above supported → clear
   error, never a silent partial decode.
5. `Envelope_SurvivesOrleansSerializer` — `[Trait("AC","OE-AC-041")]` — round-trip an envelope through the TestingHost
   echo pattern (extend `IEchoGrain` or add `IEnvelopeEchoGrain`) proving `[GenerateSerializer]`.
6. `OldShape_StillDecodes` — rolling-upgrade guard: an envelope serialized without any
   not-yet-added optional member decodes correctly (pins the add-only `[Id]` discipline;
   grows with every future envelope change).

## Implementation notes
- Assumption: durable command records are STJ-serializable as-is (plain records + typed
  ids with existing converters, e.g. `InstanceIdJsonConverter`). If any command resists
  clean STJ round-trip, STOP and record `blocked` in PROGRESS.md — do not invent surrogate
  shapes inline.
- Keep the codec exhaustive-switch style so a future command kind fails compilation or the
  `UnknownKind` test, never silently.

## Out of scope
Grain interfaces/implementations (OT1-01), management commands not yet accepted by
`ProcessAsync`.

## Definition of done
- [ ] All listed tests green; solution builds zero-warning
- [ ] No Orleans attribute outside `v3-gpt/src/OrcaCore.Engine.Orleans/`
- [ ] PROGRESS.md updated; committed as "OT0-03: transport envelopes (OE-060)"
