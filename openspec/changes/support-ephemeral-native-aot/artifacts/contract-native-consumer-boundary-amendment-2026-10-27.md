# Proposed issue #13 public-consumer boundary amendment

Status: PROPOSED ONLY. No canonical/spec/registry change or public API addition is
approved or applied by this document. Task 2.4 remains open and task 2 uncommitted.

## Finding F1

The approved R1 consumer protocol requires typed incoming/outgoing event execution
in NS-10 on every public-package native cell. The selected public ephemeral
closure has no event-ingress service or publishing method. Its router is internal,
and Publish plus IWorkflowEventIngress belong to durable authoring/hosting, which
the approved issue explicitly excludes. Using a friend, reflection/private bridge,
durable package or a new unapproved export cannot satisfy that public-only proof.

Evidence:

- src/OrcaCore.Engine.Ephemeral/Facade/EphemeralWorkflowEventRouter.cs: internal
  class and routing methods; DI registration does not make the type public.
- src/OrcaCore.Abstractions/Facades/WorkflowDefinitionRegistryContracts.cs and
  WorkflowFacadeContracts.cs: public registration/start/handles expose no ingress.
- src/OrcaCore.Abstractions/Authoring/WorkflowAuthoringFacades.cs: Publish methods
  exist on DurableWorkflowBuilder, not EphemeralWorkflowBuilder.
- src/OrcaCore.Durable.Hosting/IWorkflowEventIngress.cs: durable ingress owner.
- docs/ephemeral-engine-developer-guide.md, Waits and external event ingress:
  external self-routing ingress is a durable-host capability.
- Exact existing public API baselines contain no ephemeral ingress export.

## Recommended minimal clarification (requires explicit approval)

This is a deliberate qualification-scope adjustment: it removes an impossible
external execution requirement for nonexported capabilities. It is not a product
fix and must not be represented as the original full event requirement passing.

Replace only the NS-10 public-native acceptance wording with:

> NS-10: Execute the currently public ephemeral operations: typed inbound event
> construction/materialization, typed StepResult.WaitForEvent parking and physical
> cancellation/join, timer delay/retry backoff, transient-pool acquisition/grant,
> root Parallel and bounded ForEach with ordering, ownership and failure aggregation.
> Actual event delivery/resumption and outgoing event publishing are not exported
> by the selected ephemeral packages and receive no native support credit. Their
> existing engine tests remain required regression evidence; those tests do not
> substitute for public-package native execution. A future public ingress/publish
> capability and its native qualification require a separately approved contract.

The six-cell claim must explicitly name that boundary. The matching event-related
wording in the current design, quality requirement, numbered acceptance criteria,
guides, consumer coverage map and guard/provenance pins must agree. Native NS-07,
NS-08 and NS-11 prove public materialization rejection and retained-instance state
without claiming a nonexistent public delivery path. Source-level internal event
regressions remain required; no existing assertion is removed or reclassified green.

Preserve all AOT-01 through AOT-16 assertions, all twelve NS groups and mandatory
F1/P3/amendment subcases, default disabled reflection, exact public catalogue,
fixed codec, native host matrix and individual diagnostic gate. No product source,
new public API, dependency, friend, root, suppression or runtime switch is proposed.
Typed wait/pool native assertions still need to be added before qualification.

Alternative: retain the current full event requirement and design/review a new
public ephemeral ingress/publish catalogue before implementation. That is a larger
feature, outside the currently approved source catalogue, not a harness repair.

## Proposed application gate

Only explicit approval may apply this wording. Then independently review the
amended contract/canonical/coverage target before hosted support qualification.
The current matrix gate deliberately reports NS-10 Incomplete, so passing the
available probes cannot silently approve this amendment or the whole target.
