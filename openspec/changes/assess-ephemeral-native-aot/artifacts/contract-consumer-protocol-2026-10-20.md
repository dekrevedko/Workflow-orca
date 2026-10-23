# Issue #3 public consumer and measurement contract

Status: proposed assessment protocol; no executable, warning measurement or
compatibility result is produced by this contract. Product API amendment: none.
No new type/member/attribute/option/fingerprint/dependency/friend/serializer hook.

## Native deployment matrix

| Host | RID | Flavors |
|---|---|---|
| Windows 2025 x64 native VM/host; record actual OS build and VC++ workload | win-x64 | untrimmed self-contained JIT; trimmed self-contained JIT; Native AOT |
| Ubuntu 24.04 LTS x64 native VM/host; record kernel, release and compiler/native libraries | linux-x64 | the same three flavors |

Target net10.0 with the repository-resolved SDK; record exact SDK and framework/
runtime/ILCompiler packs. PublishAot=true belongs to the Native AOT consumer
configuration; trimmed JIT uses PublishTrimmed=true/TrimMode=full and PublishAot=false;
the self-contained JIT control has PublishAot=false/PublishTrimmed=false. Use
separate restore/intermediate/publish directories, no incremental leftovers,
no ReadyToRun substitute and no cross-OS qualification. Record all effective
properties, including SDK-default JSON-reflection and GC/tiered-JIT settings.
Do not force JSON reflection on to hide deployment incompatibility.

The ordinary external project is outside repository build-props discovery. Its
public references are OrcaCore and OrcaCore.Engine.Ephemeral plus explicitly pinned
Microsoft hosting/DI packages as needed; Core is only a transitive dependency.
No direct Core API use, ProjectReference, friend access, private reflection or
internal-type descriptors. Pack all twelve normal packages, but inventory only
the actual three-OrcaCore-package consumer closure plus actual .NET dependencies.
Use explicit candidate-feed/nuget.org sources and an isolated cache. Retain hashes
of nupkgs, assets/lock data, executable/config/runtime/native files and source/base.
Execute the published app directly with its matching RID launcher.

## Fixed scenario catalogue

Every platform/flavor reports all sixteen IDs and their precise outcome/cause.
The fixture types and exact generic closure are recorded as part of the source
target; reference DTO and value-type journeys are both mandatory. Every step body
is a named public IStep implementation constructed through public DI registration.
Selectors/merges remain the ordinary authored delegates; replacing named steps
with lambda step bodies is not permitted. No internal router, engine or codec is
called by the consumer. Barriers/notifications synchronize actual observations;
bounded watchdog timeouts detect harness failure, not semantic scheduling success.
Cases needing an accepted handle before a blocking step use an initial public
Delay boundary and an app-owned controlled TimeProvider. That is ordinary public
DI, not an internal resume/router bridge; no public ephemeral event-ingress API
is invented. Wait for the accepted handle before advancing that clock/body.

| ID | Required public journey and observable assertion |
|---|---|
| AOT-01 | Register/start a reference-DTO typed workflow with a named DI step; typed output and terminal outcome are correct and the dependency sentinel was used. |
| AOT-02 | Run typed value input/state/output through a named step and ReplaceState; the concrete value-type generic instantiations and returned value are exercised. |
| AOT-03 | Query reference state and typed output and mutate caller copies; later observations remain detached from those mutations. |
| AOT-04 | Cancel one output waiter while a barrier-controlled body remains active; it cancels only that observer, and another waiter receives normal output after release. |
| AOT-05 | Request workflow cancellation on an accepted instance; observe asynchronous cancellation through a callback barrier, expected terminal/output-unavailable behavior and actual body settlement. |
| AOT-06 | Close via ApplicationStopping while an initialized pending start is running; it drains to Accepted, while a new start is refused and dependencies remain alive until later StopAsync. |
| AOT-07 | A controlled noncooperative body first produces Incomplete without disposing dependency owners; release it, reobserve Completed, then dispose without later sentinel access. |
| AOT-08 | An eligible failed named step retries to success at maxAttempts=2, preserving StepOperationId and exposing attempt numbers 1 then 2. Use a known eligible public failure from existing retry controls. |
| AOT-09 | Root Parallel uses two named branches and barriers, typed ordered branch results and one replacement-state merge under the host execution-path bound. |
| AOT-10 | Root ForEach snapshots three finite items with node/host concurrency bounds, preserves index order and never exceeds the lower admitted concurrency bound. |
| AOT-11 | ForEach WhenAllOutcomes includes a controlled failed item, typed failure occurrence/index and ordered successful item outcomes; no suppressed success or missing failure. |
| AOT-12 | Registration rejects an app-defined converter on a declared workflow payload through the existing invalid-definition/codec-unsupported classification; no body executes. |
| AOT-13 | A supported declared graph with an actual reference cycle fails at the ordinary public value boundary with the existing codec error, without accepted success/partial commit. |
| AOT-14 | A supported input whose encoded value exceeds the fixed 256 KiB value limit fails at the public boundary; byte accounting and failure family match the JIT control. |
| AOT-15 | Round-trip caller-created strong values (including case/whitespace and non-ASCII text) inside supported typed values; their exact ordinal values survive without app converters. |
| AOT-16 | Build two independent provider roots/scopes, stop one and use the other, then join/dispose in order; root/scoped/external sentinels prove successful ownership and root independence. |

The source target binds every expected public result/error to current doc-17/
fixed-codec/retry/ownership rules and existing controls. A failure is not accepted
merely because JIT and AOT both exhibit it. JIT must first match those expectations.
No serializer replacement, private rooting, dynamic plugin or durable provider
fixture is allowed. App-owned report JSON may use Utf8JsonWriter or its own source
generation; doing so does not configure the engine's private codec.

## Diagnostic ledger and assessment result

Retain every discovery and final stdout/stderr diagnostic and publish exit code,
with compiler/trimmer/AOT/native linker errors distinguished. Expand library
warning summaries with TrimmerSingleWarn=false. One ledger occurrence contains:
run/cell ID, package/assembly version+hash, diagnostic code/severity, exact message,
normalized and original location/member, occurrence count, affected cases,
disposition, rationale and proof reference. No static-risk row is an observed IL
warning. Native-tool/environment errors without an IL code still get a row.

Corrected means the exact final publish no longer emits the finding and scenarios
pass. Unsupported names the exact failing deployment/cases and preserves their
failures. LocalRequirement names a bounded consumer-owned public type/generic root
or deployment prerequisite, why it suffices, its evidence, and the narrower claim;
no entire product assembly or private member can be rooted. Unclassified, changed
or missing diagnostics fail closed. A local condition is not general library support.

Each scenario has Passed, FailedBehavior, PublishFailed or BlockedUpstream with a
causal parent. Platform/flavor result is AssessedCompatible, AssessedConditional,
AssessedUnsupported, BlockedEnvironment or NotRun. All sixteen executed passes,
no unsupported finding and complete classified diagnostics are required for a
compatible/conditional cell. A missing native host or invalid JIT control prevents
a complete green assessment. A reproduced Unsupported cell may be a green record-
validation outcome but remains a failed compatibility result; no blocked scenario
or nonexistent executable is credited. Unknown mismatches stop the lane.

## Measurement protocol

For each executable flavor use three retained warm-up runs excluded from the
summary denominator, then twenty retained measured runs of the same fixed
successful functional workload: AOT-01, 02, 03, 08, 09, 10, 11, 15 and 16.
Interleave flavors within twenty rounds on the
same host allocation; record order, CPU/VM/memory limits, OS image, native/toolchain,
runtime/GC/tiering settings, timer resolution and workload/input hashes. The full
functional catalogue runs separately, so negative/unsupported cases do not silently
change the measured workload. If a flavor cannot run that exact workload, metrics
are unavailable with that reason. Preserve every failure/censored sample.

An external runner measures process creation to a flushed ready marker (engine and
definitions initialized); a pre-Main failure is distinguished from library setup.
The child runs the fixed workload, joins the engine physically and emits a
quiescent marker while the real provider remains alive. Sample before owner
disposal/process exit; then acknowledge release and exit. Record total user+kernel
process CPU milliseconds, peak and quiescent RSS/working set, and platform-specific
Windows private committed bytes or Linux private resident bytes (not equated).
The child emits GC.GetTotalMemory(false) as an estimated managed value and
GC.GetGCMemoryInfo().HeapSizeBytes as last-collection heap size, with collection
counts and sampling phase. Zero last-GC heap size is not zero managed memory.
Do not force GC, kill a body to claim Completed, equate CPU and elapsed time,
or call a fresh-process run cold-file-cache without controlling the cache.

Record raw bytes/ms counters and availability reasons; summaries give medians and
ranges plus sample counts. No absolute speed/memory budget or cross-OS ranking
is a support gate. Measure untrimmed JIT on both hosts even if native publication
is unsupported; absence of native metrics stays unavailable rather than zero.
