# Runtime-view source candidate — Tasks 2.1–2.4

Date: 2026-10-04
State: implemented candidate, independent source approval/checkpoint pending.
Registry: ApprovedPending; permanent Complete promotion is a separate review.

## Authority and bounded scope

The approved atomic transition is checkpoint
`a0da21ba9597e3864a3d4134120fbb0138417bd7`, tree
`572d86f6801e36a0611227386939e4dcbb1ab885`, direct-child evidence
`6c8bcd02bf6c747800c17dbc27afce53d24efbc1`, and checkbox-only activation/base
`13fe5b996e4758e383ea6ac68d86bdaf10b940b8`. Its immutable verdict is 9,749
bytes, SHA-256 `cce8c5195f559f1624a002ef4870c32ccdb445163aec9f552dcbfc5245de7e35`.
The source gate verifies the actual checkpoint/tree, direct-child evidence,
verdict addition and one final APPROVE line, not just a task checkbox.

This target adds exactly `OrcaCore.Dag -> OrcaCore.Dag.Hosting`, the already
canonical internal runtime view and the Hosting mapping adapter. The prior
eight grants, authoring six-member boundary, direct package edges and public
API baselines are unchanged. No codec, reflection bridge, public implementation
metadata, test friend, child start, durable input commit or DAG runtime is added.
The adapter is not yet wired into the coordinator: later reshape 8.4/8.5 own
that bridge. Mapping evidence here uses already-detached, already-decoded values.

## Exact compiled boundary

The reviewed contract remains three internal type families and fifteen members:

- `WorkflowDagPlan<TRunInput>.GetRuntimeView()`;
- `DagRuntimeView<TRunInput>.Nodes` and `EvaluateMapping(...)`;
- `DagRuntimeNodeDescriptor`: Reference, AuthoredOrdinal, ChildDefinitionId,
  ChildDefinitionVersion, ChildFingerprint, InputType, OutputType, Dependencies;
- `DagMappedInputResult`: IsValid, Input, InputType, FailureCode.

The production Hosting assembly actually consumes all fifteen, and no other
non-public DAG type or member. The guard decodes PE type/member metadata,
resolves signatures against the target assembly, and compares the exact sets.
It scans TypeRefs independently of MemberRefs, including field/method signatures,
generic arguments, base types, interface implementations and attribute typeof
references. Constructors, fields and extra overloads are not implicitly allowed.
No product reflection or dynamically invoked delegate implements the seam.

Compiled permanent negative probes isolate eight forbidden type identities and
four forbidden member identities, each individually rejected. A public-only
reference is the green control. Temporary assemblies inherit the repository's
byte-exact `global.json`; no ambient SDK assumption or additional test friend is
needed. Test metadata/assembly inspection is not a product reflection bridge.

## Mapping behavior and null policy

Authoring captures child input/output types with typed generic parameters.
Typed mapper wrappers replace untyped delegate storage; Build never invokes them.
The view snapshots nodes and dependencies as read-only lists. Evaluation requires
exact plan-reference identity, snapshots the output-map structure, validates every
declared direct resultful output (including unused ones), rejects missing/foreign/
extra/wrong-typed entries and resultless outputs, then invokes the mapper once.
The bridge, not this structural snapshot, must detach mutable value objects.

OutputOf still requires a successful declared direct dependency. Present null is
valid only for reference or Nullable<T> outputs; absence and non-nullable null are
invalid. The same declared-type rule validates mapper input, including nullable
boxed values. Invalid access, shape or ordinary mapper exception (including a
mapper-thrown OperationCanceledException) returns DAG_INPUT_MAPPING_INVALID with
no input/type. OutOfMemoryException, StackOverflowException and AccessViolationException
are excluded from normalization. Only a synthetic catchable out-of-memory throw
is tested in-process; no claim is made to catch stack overflow or access violation.

The Hosting-level harness compiles the actual adapter source in a Hosting-named
executable. Its exact 34 executed assertions cover immutable snapshots, no eager
mapper invocation, descriptor values, valid mapping, foreign/missing/non-direct/
transitive access, incorrect declared output/input type, resultless outputs,
reference and nullable null/value cases, ordinary exceptions, mapper cancellation,
out-of-memory propagation, unused invalid dependencies and caller-map mutation.
The complete fixture is independently guard-source hash-pinned; dropping a case
cannot be disguised by retaining the aggregate assertion marker.

## Both round-89 notes addressed

Current numbered/binding/guide status now names the approved atomic checkpoint,
and says compiled in this source candidate awaiting independent source approval.
Dated Decision 22 entries are unchanged; a new 2026-10-04 entry is appended.
The Task 8.0 map and all affected Task 7.3 rows/digest are refreshed together.

Schema 8 renames the misleading superseded contract array to
`historicalRuntimeViewContractArtifacts`. Its two paths/hashes and both artifact
files are unchanged: the original rejected contract is superseded; the approved
RV-remediation contract still governs. Both remain permanently source-pinned.
Proposed and approval history is retained; no prior rejected packet is rewritten.
Canonical specs and deltas are unchanged: provenance stays 180 rows / 47,347
bytes / `40d4d8c0b9144ea087d7b36d33df35d4736942ae615f50126f7ba136b20da8c1`,
173 synchronized, four superseded predecessors, three bootstrap-only, zero pending.

## Verification

Debug and Release non-incremental warn-as-error builds: zero warnings/errors.
Core 350, Ephemeral 79, Durable 99, Acceptance 37, Hosting 24, certification 96;
real PostgreSQL 101, SQL Server 72, Integration 11. Infrastructure 243/243;
fourteen documented ExpectedRed failures are reported separately. Focused source
guards are 3/3 in both configurations. OpenSpec strict 20/20. Twelve fresh packages,
eight green consumers and dag-hosting as the one existing expected red; green compile
fixtures and public baselines pass. This does not claim future DAG execution green.

The only counted additions are this guard's three Facts: physical 340 / 1,405,
active 192 / 717. Package-source recapture changes only Dag and Dag.Hosting.
Public API baseline files do not change. Frozen/committed rehearsal results and
exact anchors are supplied separately in the review handoff to avoid circular hashes.

Six compiling isolated negative controls are red at their intended assertions:

| Mutation | Intended detector |
|---|---|
| Suppress non-public TypeRef collection | Compiled sentinel identities missing |
| Neutralize the member subset policy | Forbidden-member self-test fails |
| Exclude mapper cancellation from normalization | Real adapter harness sees escaped OCE |
| Reject every successful null | Reference-null behavior assertion fails |
| Hosting typeof of internal DagValueTypes | Actual production TypeRef set differs |
| Hosting reads DagNodeRef.PlanToken | Actual production member set differs |

The first TypeRef suppression used a constant-false branch and was stopped by
CS0162 rather than the test. It was rerun with a compiling runtime predicate and
failed on the missing emitted identity. All target files were restored byte-exact
between controls. Main HEAD/index were never moved by these probes.

## ReSharper review

JetBrains InspectCode 2026.2.3.1 analyzed Dag, Dag.Hosting and the new boundary
guard against Release/net10.0, with repository global.json and matching builds.
The retained `task-2-runtime-view-inspectcode-2026-10-04.sarif` is the scoped report.
No CleanupCode or automatic refactor was run. Results: 59 findings, zero errors,
41 warnings and 18 notes. These are not dotnet compiler warnings.

Triage: naming suggestions follow existing local conventions; annotation-based
redundant null checks are deliberate CLR boundary defenses; private/primary-constructor
and qualifier suggestions are cosmetic; unused authoring/public members and Hosting
descriptor properties are contract or future bridge surface. An actually unused
retained Workflow field was removed. Public API, phantom generic output and reviewed
seam visibility were not weakened to silence the inspection. No functional finding
was left unidentified; the report remains available for independent scrutiny.

## Remaining authority gates

Tasks 2.1–2.3 are implemented; 2.4 stays open pending source review/checkpoint and
is deliberately unpinned to allow its later checkbox-only activation. Tasks 3.1/3.2
remain open and pinned. Complete promotion must bind the actual independent source
verdict, checkpoint/tree and direct-child evidence in its separate reviewed target.
No 8.4/8.5 bridge work starts before this source checkpoint is independently approved.
