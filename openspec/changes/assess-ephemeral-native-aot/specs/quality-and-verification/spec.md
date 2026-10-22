## ADDED Requirements

### Requirement: Ephemeral native publishing assessment uses an exact public consumer matrix
The project SHALL assess the selected ephemeral application journey with an ordinary
external executable referencing candidate NuGet packages and public APIs only.
The consumer SHALL use typed definitions/handles and named DI steps, without project
references, friend access, private reflection, dynamic DLL loading or durable providers.
Each source/package identity SHALL be tested on Windows x64 and native Ubuntu
24.04 LTS x64 in three Release self-contained flavors: untrimmed JIT, fully trimmed
JIT and Native AOT. Builds and execution SHALL occur on the matching native OS and
architecture; Docker, WSL, cross-publishing alone or a different Ubuntu release
SHALL NOT substitute for the Ubuntu execution lane. Native x64 VMs are allowed
when their environment and resource allocation are recorded.

The same consumer sources, input manifest, package versions and workload SHALL be
used across flavors. A fresh isolated restore SHALL bind exact package hashes,
transitive managed dependencies, SDK/runtime/compiler versions, publish properties,
OS/toolchain and runtime native-library closure. The selected OrcaCore closure is
OrcaCore, OrcaCore.Core and OrcaCore.Engine.Ephemeral; additional application-hosting
dependencies SHALL be inventoried separately. The twelve-package distribution
inventory SHALL NOT be confused with this consumer's resolved closure.

Assessment SHALL NOT add a product API, annotation, option, role fingerprint,
dependency, friend edge, serializer replacement or broad IsAotCompatible/IsTrimmable
claim. Product remediation SHALL require its own reviewed amendment. Contract
approval authorizes preparing the consumer/lane/evidence target, not a support claim.

#### Scenario: A package-only native consumer is evaluated
- **WHEN** the assessment runs for a named OS, architecture and publish flavor
- **THEN** its exact restored packages and published artifact are identified, the artifact executes on that native host, and the result cannot be credited from dotnet run, unit tests, a project-reference app or a publish command alone

#### Scenario: Only a Linux container or cross-published executable is available
- **WHEN** native Ubuntu 24.04 x64 execution evidence is absent
- **THEN** that required platform remains BlockedEnvironment or NotRun, never qualified or silently replaced

### Requirement: AOT and trimming findings remain explicit and non-promissory
Every discovery and final build/publish diagnostic SHALL be retained and accounted
by platform/flavor, diagnostic code, emitting assembly/member/location, message,
occurrence count and exact package/source identity. Assembly-summary diagnostics
SHALL be expanded with detailed dependency warnings; compiler, trimmer, AOT,
linker and native-tool errors SHALL remain distinguishable. Static source risks
SHALL NOT be presented as observed warning IDs or measured compatibility results.

Each finding SHALL be Corrected with a final rerun proving its absence,
Unsupported with affected scenario cells, or an explicitly justified LocalRequirement
with exact bounded scope, evidence and limitations. Unclassified findings,
unexpected diagnostics or changed counts SHALL fail the assessment consistency gate.
Blanket suppression, whole-product rooting, unchecked runtime-feature switches,
replacement codecs and private bridges SHALL NOT manufacture a compatible result.
Ordinary library warnings-as-errors policy SHALL remain intact; a separate consumer
diagnostic-capture invocation MAY retain nonfatal warnings without hiding them.

The report SHALL distinguish Passed/FailedBehavior/PublishFailed/BlockedUpstream
scenario cells and AssessedCompatible/AssessedConditional/AssessedUnsupported/
BlockedEnvironment/NotRun platform-flavor outcomes. Only fully executed passing
required scenarios with no unsupported or unclassified finding SHALL qualify the
exact cell, with any LocalRequirement stated as a condition. Environment failure,
a broken untrimmed-JIT control or unexpected behavior SHALL fail the lane. A
consistently reproduced unsupported deployment MAY pass the assessment-record gate
while its compatibility result remains unsupported. No result SHALL imply library-
wide, other-type, other-RID or other-version compatibility. Independent final review
SHALL approve the interpretation before publication.

The six base cells SHALL leave JsonSerializerIsReflectionEnabledByDefault unset.
Every trimmed/AOT base artifact SHALL be executed first with SDK defaults, recording
its effective JsonSerializer.IsReflectionEnabledByDefault value, phase, exact
exception/inner-exception types and messages, and public error code/diagnostics.
A definition rejection SHALL NOT be relabeled a codec failure merely because JSON
metadata resolution caused it. Publish failure SHALL retain its own actual cause.

Four additional diagnostic cells (trimmed and AOT on each OS) SHALL explicitly set
JsonSerializerIsReflectionEnabledByDefault=true. They SHALL retain separate artifact,
property and result identities and SHALL NOT replace or repair the default cells.
The switch is a candidate LocalRequirement, with all IL2026/IL3050 and other findings
individually accounted for. These extra cells SHALL be at most AssessedConditional
if every required scenario passes; unsupported or blocked cells remain so. No
product JSON options, TypeInfoResolver, codec hook or broad suppression is added.
Extra-cell measurements SHALL keep that condition and never imply default support.

#### Scenario: SDK-default JSON metadata resolution rejects a declared type
- **WHEN** a default trimmed/AOT executable fails before the first accepted workflow
- **THEN** its exact public rejection and exception chain are retained, later cases name that causal boundary, and separate switch-enabled discovery cannot turn this default cell green

#### Scenario: A switch-enabled diagnostic cell reaches later runtime paths
- **WHEN** the explicitly labelled extra artifact runs with JSON reflection enabled
- **THEN** later facade/generic/codec behavior and available measurements are recorded under that extra identity, with at most a conditional conclusion and every diagnostic classified

#### Scenario: Native publication fails at the fixed codec
- **WHEN** a reproducible diagnostic or runtime failure prevents a required native journey
- **THEN** the report preserves the failure and marks downstream native cases BlockedUpstream rather than Passed, while a green record-validation job makes no native-support claim

#### Scenario: A previously classified diagnostic changes
- **WHEN** a new code, location, message identity or occurrence count appears
- **THEN** the consistency gate fails until the actual finding is individually classified and reviewed; a broad NoWarn list cannot absorb it

### Requirement: Published compatibility evidence executes the selected ephemeral scenarios
The published artifacts SHALL execute the sixteen scenarios AOT-01 through AOT-16
in assess-ephemeral-native-aot's task-2-consumer-protocol-2026-10-21.md, covering typed registration,
reference and value payloads, named DI construction, detached state/output, local
output-wait cancellation, workflow cancellation, physical shutdown ownership,
retry identity/attempts, bounded root Parallel and ForEach behavior, and fixed-codec
registration/runtime failures. Each platform/flavor SHALL report all scenario IDs;
unreachable scenarios SHALL record the causal failure and SHALL NOT disappear.
Both untrimmed-JIT controls SHALL pass every scenario before native comparisons
are accepted. A missing required native platform prevents a complete assessment.

The nonreplaceable orcacore-json-v1 policy, declared-type/graph validation,
strong-value behavior, byte limits, detachment and established public failure
outcomes SHALL remain unchanged. App-owned JSON source generation MAY format the
assessment report; it SHALL NOT be credited as configuring the engine's private
codec. Required work SHALL use barriers/notifications and honor physical settlement;
local wait cancellation or logical terminal status SHALL NOT prove workflow stop.
DI providers/scopes/dependencies SHALL remain alive on Incomplete and be disposed
only after Completed and the owner's remaining hosted stops.

#### Scenario: A named step retries and bounded work completes
- **WHEN** the published executable exercises retry, Parallel and ForEach cases
- **THEN** public results, attempt coordinates, ordering and admission barriers meet the same approved semantics in each executable flavor without an internal helper or lambda-step substitute

#### Scenario: A value violates the fixed codec
- **WHEN** the published executable supplies an unsupported declared graph or a runtime cyclic/oversized value
- **THEN** the same public boundary and failure classification as the JIT control is observed, with no partial success or alternate serializer

#### Scenario: A body outlives a shutdown observation
- **WHEN** the noncooperative published case first returns Incomplete
- **THEN** the real dependency owner remains alive until the released body physically settles and a later observation returns Completed

### Requirement: Native publishing measurements preserve comparable runtime and ownership conditions
For each executable platform/flavor the assessment SHALL record artifact size and
dependency closure, external process-launch-to-ready elapsed time, total process
CPU time, peak and quiescent process memory, and explicitly named managed-heap
observations. Measurements SHALL use identical package/source/input identities and
the same machine allocation with isolated processes, a fixed workload, three
warm-up executions excluded from summaries followed by twenty recorded executions per flavor,
and interleaved flavor order. Warm-up outcomes SHALL still be retained. Raw samples,
units, sample/GC phase, host resources, runtime settings, run order and all failed
or censored samples SHALL be retained; medians/ranges SHALL name their denominators.
Fresh-process startup SHALL NOT be called cold-file-cache startup without such control.

CPU SHALL mean user plus kernel process CPU, not wall time or an unlabeled percent.
Windows private commit, Linux private resident bytes and RSS/working set SHALL use
their platform-specific names and SHALL NOT be equated. GC.GetTotalMemory(false)
SHALL be labeled an estimate; HeapSizeBytes SHALL be labeled last-collection heap
size with collection counts, not current process memory. Forced GC SHALL NOT be
introduced to make a headline comparison. Unsupported or unexecutable cells SHALL
report unavailable measurements with the causal reason, never invented zeroes.
The protocol SHALL impose no performance or memory ceiling or cross-machine ranking.

#### Scenario: AOT cannot produce an executable
- **WHEN** publication fails for a reproducibly unsupported cell
- **THEN** its startup/CPU/memory values are unavailable with that reason, while valid JIT controls and dependency/diagnostic evidence remain reportable

#### Scenario: JIT and AOT measurements are compared
- **WHEN** both exact artifacts execute on the same required platform
- **THEN** the report identifies equivalent workloads and every raw sample, separates process memory from managed-heap estimates, and makes no unsupported cold-cache or universal performance claim
