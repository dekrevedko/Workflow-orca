# Issue #13 task 2.3 private dispatch evidence

Task 2 remains uncommitted on codex/issue-13-native-aot-trimming, HEAD
10aec46e4a1f2f0771f6a5418b4dc6ef3ff3b5fd. The approved task 2.2 baseline is tree
2e276cbeddf908ff41d75c762582fec80e6737b8. Task 2.3 remains open because the public
throttle metadata boundary needs an explicitly approved catalogue amendment.
Task 2.4 and all publication gates remain untouched.

## Changes and behavioral evidence

- Direct existing internal constructors replace fingerprint, handle, public
  exception/value, provenance and failure-converter constructor discovery.
  Full-trim start originally failed at the missing fingerprint constructor;
  its exact golden corpus now passes.
- A private Core visitor carries WorkflowDefinition<TState> to a per-host typed
  engine adapter for preflight, registration, StartAsync<TInput> and event
  delivery. Actual Native AOT originally failed at definition property lookup;
  the new adapter reaches execution without a new friend grant or public bridge.
- Root/branch authoring binds closed state/step/snapshot and Func operations
  before type erasure. Delegate identity remains unchanged for fingerprints;
  ConditionalWeakTable bindings avoid permanent retention of application
  callback closures. Type-indexed bindings contain closed operations only.
  Unbound Generated operations fail instead of entering a reflection fallback.
- Typed result/outcome factories retain order, schema validation, null results,
  detached failures and exception envelopes. Typed item arrays, partitioners
  and ForEachItemInput<TItem> replace runtime generic/array construction and
  Index/Items lookups. The extended native tracer was red before replacement
  and green afterward. The 256 KiB snapshot bound still precedes item bodies.
- Internal virtual wait dispatch reads both sealed wait-result records directly.
  Original dynamic-wait, resumed-event, timer, retry, resource/throttle, timeout,
  cancellation, retention, pin and shutdown assertions are unchanged. All six
  CI unit projects pass; no earlier runtime test file drifted from task 2.2.
- Generated codec validation traverses fixed metadata and DictionaryEntry values,
  rather than discovering members/KeyValuePair properties. Product converters
  serialize nested failures through explicit JsonTypeInfo overloads. All
  existing codec bytes/fingerprints, failure and negative controls remain green.

Two additional review findings were fixed before freezing. The nonpublic
JsonInclude control passed in legacy mode but failed in Generated mode because
my first graph rewrite called its getter twice; metadata visibility now retains
the old public-member graph boundary, with no property-name invocation. A second
control showed missing internal TItem[] metadata was discovered too late. It now
fails TryBuild with SFE-TYPE-003 and zero Init/item effects. Internal snapshot
metadata prerequisites do not enter schema/fingerprint inputs.

## Frozen-source checks

| Check | Result |
|---|---|
| OpenSpec strict | 25/25 |
| Release warnings as errors | 0 warnings / 0 errors |
| Infrastructure, CI engine-only coverage filter | 301/301 |
| Six CI unit projects, coverage | 969/969 |
| Public PackageReference-only process cases | 38/38 |
| Local Windows full-trim selected package probes | 6/6 |
| Local Windows Native AOT selected package probes | 6/6 |
| ReSharper solution / explicit consumer graph | 0 errors in both |

The six native/trim probes cover closed dispatch (including throttle validation
and nullable/enum/struct results), reference-state ownership/drain/abort in both
retention modes, golden bytes/fingerprints, nested failures/size bounds,
nonpublic getter parity and early missing-array admission. JSON reflection is
false by default in both publications; Native AOT reports dynamic code false.
The normal consumer restored all product packages from the exact local feed
into a fresh owned cache without fallbacks, and every cached product DLL was
byte-checked against its archive. Native/trim SDK tooling reused the shared
NuGet cache read-only as a fallback; the three product packages stayed in the
owned primary cache with their exact verified bytes. This is a development
probe on Windows 11, not task 2.4 Windows Server/Ubuntu native qualification.
No source implementation or support approval is inferred from these results.

The old 90-site risk inventory is untouched. Every row has an individual current
disposition; 14 additional sites include static bootstrap/metadata attribution,
typed bindings, early snapshot admission and the missed public throttle check.
Analyzer records are retained individually: 28 source-AOT, 9 package-AOT and 5
package-trim (42 records, four observations of the one unresolved IL2090
boundary). Other records are the fenced unconfigured legacy path or deliberate
handwritten-resolver negative setup. No NoWarn, blanket rooting, reflection
switch, compatibility badge or new public annotation was introduced. Diagnostic
publish used TreatWarningsAsErrors=false to collect evidence, so it is not a
warning-clean native qualification claim.

ReSharper completed with build enabled and Any CPU. One earlier concurrent
inspection/test attempt encountered file locks and is excluded. An earlier
coverage run instrumented the DAG assembly because I omitted CI's include
filter; that harness failure is excluded. The reported final run was serialized
and uses [OrcaCore.Engine.*]*. Compile/setup mistakes in development probes are
not counted as behavior-red evidence.

Provenance is unchanged from task 2.2: 206 rows, 195 synchronized / 8 superseded /
3 outside / 0 pending, with 24 predecessor artifacts. Canonical requirements,
the approved public catalogue and all 12 API baselines stay byte-identical to
task 2.2. Registry source/native/whole-target approval remains unset. The exact
source allowlist expands only to the named dispatch owners; package source pins
change only for OrcaCore, OrcaCore.Core and OrcaCore.Engine.Ephemeral. Test source
accounting is 366 files / 1,510 declarations, active 212 / 822, with 23 out-of-band
fixture files and all 866 retired declaration rows unchanged and uncredited.
Compact commands, raw reports, TRX/coverage summaries, package identities and
red/green logs remain under .artifacts/publishing-validation/issue-13-task-2-3/.
