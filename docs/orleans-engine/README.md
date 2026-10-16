# Future Orleans-hosted durable engine boundary

This is the single active note for a possible future Orleans hosting variant. It is not a
first-release package, implementation task, or authorization to add source. Any Orleans work starts
with a new OpenSpec change and independent approval against the then-current contracts.

## Required starting boundary

A future Orleans host must preserve the selected OrcaCore contract rather than revive provisional
surfaces:

- PackageId/assembly `OrcaCore` continues to own application authoring, typed definitions,
  instance handles, events, and results;
- the single ordinary `Wait` remains cold-capable: reaching it commits the active-wait fact and
  returns the grain turn; a later event or timer starts a new turn;
- durable ingress keeps caller-created globally unique `EventId` values, direct, correlation,
  definition-fanout, and start-or-deliver routes, retained pre-wait acceptance, registration-time
  ambiguity rejection, and source acknowledgement only after `Accepted` or `Duplicate`;
- authored `Publish` commits a workflow-event outbox record atomically with workflow progress, and
  the application-registered `IWorkflowEventDispatcher` dispatches it; continuation records remain
  runtime-internal and never reach the application dispatcher;
- payloads and detached state use the fixed `orcacore-json-v1` codec;
- `OrcaCore.Engine.Durable` retains interpreter/runtime ownership,
  `OrcaCore.Durable.Hosting` retains durable hosting, ingress, and dispatcher-port ownership, and
  each durable provider package retains its own storage registration;
- an Orleans adapter may host reviewed durable seams and own only Orleans-specific activation,
  transport, and lifecycle integration; it must not expose internal aggregate, interpreter,
  command-processor, compiled-plan, or provider implementation types as public seams;
- registration remains role-specific, programmatic, and mutually exclusive with another engine
  owner in the same service collection; and
- deferred capabilities remain absent until their own reviewed amendment re-enters them through the
  [future-capability registry](../specs/13-phasing-and-open-questions.md#134-future-capability-registry).

## Work required before implementation

1. Create and independently approve a new OpenSpec change before adding projects, package
   references, migrations, or implementation tasks.
2. Propose the exact package/assembly owner and one-way dependency graph without moving the
   existing application, durable-runtime, hosting, dispatcher, or provider boundaries.
3. Define Orleans activation, storage, reminder/timer, event-delivery, and host-lifecycle adapters
   without changing ordinary application signatures or the four-route ingress union.
4. Map every new requirement to the existing durable-runtime and provider invariants, especially
   atomic commit, start idempotency, retained inbox ownership, outbox dispatch, fixed-codec
   detachment, and replacement-host recovery.
5. Add clean package-consumer fixtures and multi-silo restart tests before proposing any package for
   a release manifest.

The superseded 25-file pre-v1 Orleans plan is preserved unchanged at
[`../archive/plans/orleans-engine-pre-v1/README.md`](../archive/plans/orleans-engine-pre-v1/README.md).
