# Issue #3 native assessment result — pending independent review

Contract eea51c9 / verdict dbd829e. Product API amendment catalogue: empty.
The exact public-package consumer ran on native Windows Server 2025 Datacenter
and Ubuntu 24.04 LTS x64 VMs, four logical CPUs and approximately 16 GiB each.
They are separate allocations; no cross-OS performance ranking is justified.
Final native source `b92c5ccb47aa26a1e5a4a754d667bc98b9180489`, workflow
[39721105482](https://github.com/dekrevedko/Workflow-orca/actions/runs/39721105482).
The issue branch remains uncommitted; only the separately authorized temporary
validation branch was pushed. Native record consistency is green, not semantic
approval or a Native AOT support promise.

| Host | Flavor | JSON reflection | Scenarios passed | Warning identities / occurrences | Result |
|---|---|---|---:|---:|---|
| windows-2025 | jit | true (SDK default, property unset) | 16/16 | 0 / 0 | AssessedCompatible |
| windows-2025 | trimmed | false (SDK default, property unset) | 1/16 | 21 / 28 | AssessedUnsupported |
| windows-2025 | aot | false (SDK default, property unset) | 1/16 | 46 / 51 | AssessedUnsupported |
| windows-2025 | trimmed-json-reflection | true (explicit) | 1/16 | 21 / 28 | AssessedUnsupported |
| windows-2025 | aot-json-reflection | true (explicit) | 1/16 | 46 / 51 | AssessedUnsupported |
| ubuntu-24.04 | jit | true (SDK default, property unset) | 16/16 | 0 / 0 | AssessedCompatible |
| ubuntu-24.04 | trimmed | false (SDK default, property unset) | 1/16 | 21 / 28 | AssessedUnsupported |
| ubuntu-24.04 | aot | false (SDK default, property unset) | 1/16 | 46 / 51 | AssessedUnsupported |
| ubuntu-24.04 | trimmed-json-reflection | true (explicit) | 1/16 | 21 / 28 | AssessedUnsupported |
| ubuntu-24.04 | aot-json-reflection | true (explicit) | 1/16 | 46 / 51 | AssessedUnsupported |

All ten publications succeeded, but publication is not functional compatibility.
Both JIT controls pass every scenario. In each other artifact only AOT-12, the
intentional app-converter rejection, passes. Each of the other fifteen journeys
is attempted independently and records its actual failed boundary; downstream
behavior is not credited. There are 160 scenario rows, 40 passes (including eight
negative controls) and 120 observed FailedBehavior rows. No timeout or harness
ASSERTION is classified as product incompatibility in the final records.

Default trimmed/AOT artifacts report JSON reflection false. AOT-01 fails at
DefinitionBuild as OrcaCore.WorkflowDefinitionException / WF-DEFINITION-INVALID,
with SFE-TYPE-002: No serializer schema is registered for 'Data' and 'System.String'.
DefinitionCompilerOptions.cs:58 maps missing default JSON metadata to this public
schema rejection. The exact newline-separated diagnostics and stacks are retained;
they are not relabeled a codec runtime exception.

Reflection-enabled trimmed artifacts advance to Start and fail with
System.InvalidOperationException: Sequence contains no matching element, from
FacadeValueFactory.Construct / PayloadFingerprint's nonpublic-constructor lookup.
Reflection-enabled AOT artifacts instead fail at Register with
System.NullReferenceException in RegisterCore's runtime public-property lookup,
or at DefinitionBuild for ValueState/Int32[] metadata. No inner exception is
invented where none exists. The switch is a candidate LocalRequirement that is
insufficient here; these extras remain unsupported, rather than conditional passes.
No private roots, source-generated engine resolver or product fix was introduced.

The classification file has 268 platform/flavor warning identities accounting for
316 actual warning occurrences, plus 120 separately bound runtime findings. Every
identity has exact code/message/member/assembly/location/count and its own source-
path rationale, affected journeys and proof. The linked Core codec copy is correctly
attributed to OrcaCore.Core; its original OrcaCore.Internal copy is a different
assembly. Warnings describe retained paths and possible limitations; they are not
all asserted to have caused the first actual failure. Earlier discovery/harness
failures and the intermediate emitter correction remain retained and labelled.
Changed/new/disappearing findings fail closed unless individually reclassified;
Corrected requires a retained zero-count row and final proof, never suppression.

## Measurement observations

Only JIT executes the exact nine-positive-scenario fixed workload. Each platform
retains three warm-ups and twenty measured fresh processes, per-round order,
flushed ready/quiescent markers and owner release. For other flavors the same
workload fails, so startup/CPU/memory comparison values are unavailable with that
cause; they are not manufactured zeroes. Artifact closure/size is still recorded
for every successfully published artifact, including native binaries and symbols.
Rows below are n=20, median [minimum, maximum], in raw ms or bytes.

| Counter | Windows JIT | Ubuntu JIT |
|---|---:|---:|
| external launch to ready (ms) | 301.211 [294.757, 307.965] | 130.630 [127.884, 136.268] |
| cumulative user + kernel CPU before disposal (ms) | 812.500 [765.625, 890.625] | 384.566 [380.046, 390.079] |
| peak working set / RSS (bytes) | 54,304,768.000 [54,009,856.000, 54,435,840.000] | 76,113,920.000 [75,923,456.000, 76,394,496.000] |
| quiescent working set / RSS (bytes) | 54,304,768.000 [54,009,856.000, 54,435,840.000] | 76,113,920.000 [75,923,456.000, 76,394,496.000] |
| GC.GetTotalMemory(false), estimate (bytes) | 4,714,820.000 [4,611,896.000, 4,734,928.000] | 4,068,420.000 [4,030,424.000, 4,164,112.000] |
| last-collection heap size (bytes) | 0.000 [0.000, 0.000] | 0.000 [0.000, 0.000] |

Windows private committed bytes: 17,367,040.000 [16,883,712.000, 17,575,936.000].
Linux private resident bytes: 58,664,960.000 [58,503,168.000, 58,871,808.000].
Those are different OS metrics, not an interchangeable pair. Last-collection heap
zero with zero collection counts means no recorded collection, not zero managed
memory. No forced GC, cold-file-cache, faster/smaller or untested-type claim is made.
CPU is sampled at quiescence before owner disposal, not full subsequent teardown.
The retained bootstrap provider is alive at the sample after a real physical join;
per-scenario dependency sentinels also enforce shutdown/disposal order.

Both hosts resolve SDK 10.0.402 under the repository's latestFeature policy and
execute runtime 10.0.13. Reports include actual dynamic-code support, workstation
GC/latency observations, explicit tiering/environment overrides and emitted runtime
configuration; absent overrides are not claims about measured JIT tiers. Compiler/
linker versions, host image/OS/kernel, allocation/clock data, same twelve package
hashes, actual three-OrcaCore-package consumer closure and remaining managed/native
modules/imports are retained. No WSL, Docker or cross-publication fills a native cell.

The raw archives preserve every source record/log/sample/assets and artifact hash.
The catalogue binds every archive entry and both plain JSON native reports; rerun
the consistency gate against those two reports. The frozen review target will bind
the tested harness to both Git and current source and run ordinary repository lanes.
Assessment/canonical approval remains unset until the independent verdict. Any
remediation or publication is a separate gate.

The separate discovery-disposition ledger also accounts individually for 680 prior
warning identities / 790 occurrences and 300 prior runtime findings, including
unqualified Windows 11 development. Six timeout/assertion wrappers are explicitly
Corrected harness-cause reporting, with final compatibility still unsupported.
No original raw receipt is edited, and no old discovery is final qualification.

Plain review reports normalize only structural CRLF to LF for Git whitespace checks.
Original Windows JSON bytes remain exact in the raw archive and have a separate
rawSha256; the verifier requires exact CRLF-to-LF byte equivalence. Escaped public
exception messages and every parsed value remain unchanged, not reformatted.
