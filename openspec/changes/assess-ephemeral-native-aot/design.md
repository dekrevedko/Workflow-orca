## Context

Issue #3 is compatibility research on master 7895262, after issue #2's shutdown
merge. Its outcome may be unsupported. This target contains no consumer or product
implementation. The static source record and protocol under artifacts/ define
what later executable evidence must establish; an unchanged green Release build
does not test trimming or AOT.

## Goals / Non-Goals

Goals: a reproducible six-cell native assessment, complete warning and scenario
accounting, fixed-codec preservation, exact dependency closure and comparable
measurements. Keep one contract review and one coherent final assessment review.

Non-goals: product-wide AOT enablement; replacement serialization; new public
signatures/attributes/options; marking libraries compatible; private rooting or
reflection bridges; dynamic DLL loading; durable/provider/DAG qualification;
cross-OS AOT compilation; benchmarking claims across unrelated machines.

## Decisions

### 1. Assessment protocol, not a runtime promise

Add four requirements to quality-and-verification without changing an existing
canonical line. New public API catalogue: empty. All twelve API/compile/package
baselines, product source, dependency/friend graph and runtime role fingerprints
remain unchanged in this contract and the initial consumer/lane implementation.
Compatibility fixes, including annotations that alter the public contract, need
a new reviewed amendment. Reporting unsupported behavior is a valid research
outcome; pretending blocked scenarios passed is not.

Alternative considered: immediately remove reflection or add an AOT serializer
option. Rejected because it implements unapproved semantics and violates the
fixed-codec boundary. Consumer report source generation is separate app-owned I/O.

### 2. Three deployment flavors on each native OS

Use net10.0 with the repository's resolved SDK and exact candidate package version.
Pin the later runner labels to windows-2025 x64 and ubuntu-24.04 x64, and record
their actual image/toolchain identity. A native VM qualifies; a Docker/WSL run does
not fill the Ubuntu cell. The RID is win-x64 or linux-x64 on its matching host.
The baseline is self-contained untrimmed JIT, the second control fully trimmed
JIT, and the experiment Native AOT. All execute their published artifact directly.
Do not substitute ReadyToRun, dotnet run, or a successful cross-publish.

The external project lives outside the repository build-props hierarchy, has
PackageReferences only, an explicit NuGet.Config and a fresh isolated restore.
Candidate packages are packed from the reviewed source and hashed. Consumer
reference closure is the three OrcaCore packages plus separately enumerated .NET/
hosting dependencies, not the twelve-package distribution. Record publish switches,
runtime/ILCompiler/framework-pack identity and native loaded/static dependencies.
The protocol fixes sixteen public journeys and comparable sample phases.

### 3. Detailed diagnostics with an honest consistency gate

Discover using detailed dependency warnings (TrimmerSingleWarn=false), retain raw
stdout/stderr/binlog and native-tool errors, and assign every occurrence a stable
identity in the diagnostic ledger. Capture runs may preserve warnings as warnings
at the consumer only. Do not change library/compiler policy, suppress warnings,
root entire OrcaCore assemblies or force reflection-default switches. Static risks
are candidates for investigation, never observed IL warning counts.

A local requirement must be bounded, public and reproducible: name the exact app
types/generic closure, descriptor/annotation or deployment prerequisite, explain
why it is sufficient, and re-run those artifacts. Rooting private implementation
types is a forbidden internal bridge. A local requirement does not excuse a
RequiredDynamicCode warning in an unbounded actually used path. Unsupported
findings name all affected cells; correcting one requires a rerun with its absence.

Assessment automation validates complete records and expected supported/unsupported
outcomes. New findings, behavior mismatches, broken JIT controls, missing native
hosts or unclassified warnings fail. A known publish failure may be an explicitly
unsupported result with blocked downstream cases, never a native functional pass.
No baseline is silently updated after a mismatch. Independent final review judges
the evidence and scope before any publication of conclusions.

### 4. Preserve existing ownership and codec behavior

Use AddOrcaCoreEphemeralEngine, typed Workflow.Ephemeral definitions/handles,
named IStep implementations, public queries/waits/commands and the existing
IEphemeralWorkflowHostLifetime. Maintain barrier-based asynchronous observations,
caller-local cancellation and Incomplete retention of the actual owner. Fixtures
include ordinary reference DTOs and value shapes, strong values, detached results,
retry coordinates, bounded root composition and negative codec paths. Existing
runtime tests remain controls but are not substituted for deployed journeys.

The fixed codec uses runtime Type inspection and its own serializer options. An
application JsonSerializerContext used for report formatting cannot reach those
private options. Failure of an AOT codec path is recorded as unsupported until a
separately reviewed product solution exists, rather than opening a serializer hook.

### 5. Measure a defined process, not a slogan

The future runner launches a fresh process per sample and uses ready/quiescent/
finish markers. Startup is external launch to a flushed ready marker; user+kernel
CPU is sampled before exit; peak and quiescent process memory and in-process heap
estimates have distinct names. Keep the host/provider alive through quiescent
sampling and only dispose after a successful physical join. Run three warm-ups
and twenty recorded executions per flavor on the same host allocation, interleave
flavors, retain raw values/failures and report medians/ranges. Hardware/cache/tiered
JIT/GC settings and timing resolution accompany results. No forced GC or numeric
performance gate is proposed. Unsupported artifacts have unavailable metrics.

## Risks / Trade-offs

- Runtime generic construction, private metadata lookup and JSON inspection may
  block deployment -> preserve exact diagnostics and scenario coverage, allow a
  reviewed unsupported result, and keep remediation outside this assessment.
- A harness can fail before the library -> keep a report/marker self-check and
  distinguish toolchain, consumer, dependency and OrcaCore failures.
- Zero warnings do not prove execution -> run every published journey; declared
  results cover only the tested type/package/RID/configuration closure.
- Trimming can move failure to a later phase -> record per-scenario outcomes and
  causes; no missing rows or automatic unsupported-baseline acceptance.
- VM noise and OS memory definitions differ -> matched/interleaved comparisons,
  raw samples and explicitly named platform counters, no universal ranking.
- Disk use from six publishes and caches -> run in disposable short native paths,
  preserve compact evidence, clean owned builds/caches after verification without
  touching unrelated worktrees, shared caches or blocked paths.

## Migration Plan

1. Review this exact green contract with provenance prerequisites. Leave it
   uncommitted until approval; checkpoint its manifest, then a verdict-only child.
2. Prepare one coherent final target: append the four exact canonical blocks,
   introduce numbered owners/acceptance and truthful guide/source-map status,
   bind contract authority, implement the external consumer/assessment lanes and
   collect both native-platform evidence. No product or public-contract change.
3. Independently review that whole target, including any unsupported conclusion,
   then checkpoint and preserve the verdict separately. Publish/PR/merge/issue
   closure remain separately authorized. A missing native host keeps work incomplete.
4. Propose compatibility remediation separately if warranted by observed evidence.
   Issue #1 archival and Core lane consolidation remain separate maintenance.

## Open Questions

No unresolved contract choice. Actual warning identities, executable outcomes,
native dependency closure and measurements are empirical unknowns for the later
approved assessment, not reasons to invent a support promise in this contract.
