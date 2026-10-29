# Issue #13 converter catalogue amendment — external independent review verdict

**Date:** 2026-10-25
**Reviewer:** an independent reviewer (Claude), separate from the author's self-review in
`contract-converter-catalogue-amendment-independent-review-verdict-2026-10-25.md`, which this file
neither replaces nor edits.

**Target:**
- the eight paths in `contract-converter-catalogue-amendment-manifest-2026-10-25.txt`;
- base HEAD `10aec46`, approved R1 contract checkpoint `6122025`.

**Freeze.** I recomputed the content SHA-256 with the request's rule (ordinal paths; path NUL bytes
NUL): `477b88fde7bc4a1320cac82cdfa4fecab4e7c09c852fa3e517dd6f8a202cd8b5`. That is an exact match, and
HEAD was unchanged.

## Verdict: APPROVE the amendment (planning only)

There are no P1 or P2 findings against the amendment. This is the independent review. The amendment
itself still requires the owner's **explicit approval** before the accessibility change is made.

Neither this verdict nor that approval does any of the following:
- checkpoint task 2;
- mark task 2.2 complete;
- resolve task-2.2 F2 (the nested-failure corpus);
- advance task 2.3.

**After the owner approves:**
1. Make the single product change: `internal sealed class` becomes `public sealed class` in
   `StrongStringValueJsonConverterFactory.cs`.
2. Carry out the owner transitions the amendment lists, inside the same uncommitted task-2 target.
3. Run the matching API, compile and package tests, scoped ReSharper and the NS-02d/NS-06g–h
   controls.
4. Bring the green task-2.2 slice to the next independent review.

## What I verified

**The blocker is real, and the fix works.** I ran an independent minimal probe outside the
repository (SDK 10.0.402, `IsAotCompatible=true`, warnings as errors). A public sealed type in a
library carries `[JsonConverter(typeof(Factory))]`, and a separate app compiles a Metadata-mode
`JsonSerializerContext`.

| Factory accessibility | Result |
|---|---|
| internal | **SYSLIB1220 and SYSLIB1030**, as the author recorded |
| public, with an implicit parameterless constructor | 0 warnings, 0 errors; round-trips `{"Name":"catalogue-value"}` |

Runtime metadata registration cannot fix this compile-time failure. The catalogue change is the
minimal remedy.

**The exact source matches the table.**
- `StrongStringValueJsonConverterFactory.cs` and `StrongValues.cs` are byte-equal to HEAD. The
  factory is still `internal`.
- `CanConvert` and `CreateConverter` cover exactly the eleven tabulated types, with the stated
  Create/Parse mapping (`StepOperationId` and `LeaseProtectionToken` use Parse). Any other type throws
  the existing `NotSupportedException`.
- The factory is AOT-friendly: an explicit closed switch with one closed generic instantiation per
  type, no `MakeGenericType`/`Activator`, and a private nested converter.
- `EventName` and its peers are sealed classes, so System.Text.Json handles JSON null without calling
  the converter. "Existing reference-value null handling" is accurate.

**No new public namespace, and there is precedent.**
- `OrcaCore.Abstractions.Ids` already holds 20 public types in the OrcaCore API baseline, including
  public ID converters such as `InstanceIdJsonConverter`. Eleven attributed converters in `src/` are
  already public.
- Exposing this factory follows that precedent. It is not a replacement-codec hook: OrcaCore's fixed
  codec takes no application converter. Under NS-02d, listing the factory in context options is
  still rejected before publication or seal.

**The catalogue is complete for the ephemeral closure.** I enumerated every `JsonConverterAttribute`
in `src/`. The only other internal converters are:
- `DagStrongValueJsonConverterFactory` (OrcaCore.Dag);
- `InboxRouteKeyJsonConverter` (Provider.Abstractions);
- `ResourceGovernanceRecordJsonConverter` (Runtime.Protocol).

All three are outside the selected OrcaCore/Core/Engine.Ephemeral closure and are excluded by the
contract. No in-scope blocker remains uncatalogued.

**The planning edits are consistent.**
- The six revised files add scenarios and catalogue sentences only. The requirement identities and
  counts stay 2/2/1/2, and structured-fiber-execution is unchanged.
- NS-02d, NS-06g and NS-06h are supplemental subcases. The independent literal `"catalogue-value"`
  expectation prevents the golden tests from only checking the converter against itself.
- Default and Metadata generation are both required in every native cell.
- The approval boundary is explicit, and the owner-transition table names the exact guards, baselines
  and docs to reconcile, with no broad waiver.

**Lanes.** None were run beyond the probe. This is a planning-only amendment, and the author's strict
OpenSpec 25/25 result applies to these bytes.

## P2 (process) — The self-review file name implies independence

**Problem.** The author's self-review sits at
`…-amendment-independent-review-verdict-2026-10-25.md`, and the task-2.2 self-review uses
`task-2-2-independent-review-verdict-2026-10-25.md`. Both state in their text that they are
self-review.

**Required.** Before any registry, guard or approval-evidence path binds an "independent" verdict for
this amendment:
- the binding must cite **this** external verdict;
- self-reviews should be named `*-self-review-*` in future.

Do not rename the existing dated files. They are history. Record the distinction in the task-2.2
binding.

## P3 (optional)

- **P3-1 — Stated limit.** Generated contexts for DAG or durable types (`DagNodeId`, `DagRunId`,
  `InboxRouteKey`, `ResourceGovernanceRecord`) will still hit SYSLIB1220, because their converters
  stay internal. State this in the native-support limits, so the catalogue fix is not read as covering
  every public OrcaCore type.
- **P3-2 — Cleanup.** My probe left `E:/ocv208probe`, which contains only probe sources and build
  output. It can be deleted manually.
