# Planned Orleans-hosted durable engine variant

This directory records a possible future hosting variant. It is not a first-release package,
implementation task, or authorization to add source. Any Orleans work begins with a separate
OpenSpec proposal and independent approval against the then-current contracts.

## Required starting boundary

A future Orleans host must preserve the selected OrcaCore contract rather than revive provisional
surfaces:

- application authoring, typed definitions, instance handles, events, and results remain owned by
  PackageId/assembly `OrcaCore`;
- the single ordinary `Wait` is cold-capable: reaching it commits the active-wait fact and returns
  the grain turn; a later event or timer starts a new turn;
- event delivery retains exactly the instance and correlation routes, non-buffering `NoActiveWait`
  behavior, and registration-time ambiguity rejection;
- payloads and detached state use the fixed `orcacore-json-v1` codec;
- the Orleans adapter may consume reviewed advanced provider/runtime contracts but must not expose
  internal aggregate, interpreter, command-processor, or compiled-plan types as public seams;
- hosting registration must be role-specific, programmatic, and mutually exclusive with another
  engine owner in the same service collection;
- deferred capabilities remain absent until their own reviewed amendment re-enters them through the
  [future-capability registry](../specs/13-phasing-and-open-questions.md#134-future-capability-registry).

## Work required before implementation

1. Propose the exact package/assembly owner and one-way dependency graph.
2. Define the Orleans activation, storage, reminder/timer, event-delivery, and host-lifecycle ports
   without changing ordinary application signatures.
3. Map every new requirement to the existing durable-runtime and provider invariants, especially
   atomic commit, start idempotency, inbox conflict classification, fixed-codec detachment, and
   replacement-host recovery.
4. Add clean package-consumer fixtures and multi-silo restart tests before making the package part of
   the first-release manifest.
5. Obtain independent planning approval before adding source projects or implementation tasks.

The superseded 25-file pre-v1 Orleans plan is preserved unchanged at
[`../archive/plans/orleans-engine-pre-v1/README.md`](../archive/plans/orleans-engine-pre-v1/README.md).
