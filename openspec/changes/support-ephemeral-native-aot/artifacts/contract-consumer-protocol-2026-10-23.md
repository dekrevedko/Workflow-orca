# Issue #13 future native support protocol

Planning only. No new consumer, source or native execution is claimed here.
Base 3b7f98a; historical assessment run 39721105482 at b92c5cc remains unchanged.

## Required deployment cells

| Native host | RID | Publications executed |
|---|---|---|
| Windows Server 2025 x64 (windows-2025) | win-x64 | self-contained untrimmed JIT; self-contained full-trim JIT; Native AOT |
| Ubuntu 24.04 LTS x64 (ubuntu-24.04) | linux-x64 | same three flavors |

Use net10.0 and the recorded resolved repository SDK. Keep properties in the
consumer project/record, including PublishTrimmed, TrimMode=full, PublishAot,
TrimmerSingleWarn=false and actual JSON reflection feature status. Trimmed/AOT
leave JsonSerializerIsReflectionEnabledByDefault unset and assert false in the
running artifact. Never substitute true, private descriptors or assembly roots.
Execute the published native process directly, no dotnet run/cross-run.

Consumers are outside repository build props and use only PackageReferences,
fresh isolated caches and an explicit exact candidate feed. Record all twelve
distribution hashes but claim only the actual selected three-package closure,
plus framework/hosting dependencies. Keep report serialization separate from
workflow codec metadata. Record actual native image, SDK/runtime/ILCompiler,
toolchain, commands, warning output, source/package/harness identity and scenarios.

## Scenario coverage

Retain every assertion from the immutable issue #3 task-2 protocol's AOT-01–16.
Use a new success record rather than rewriting its expected failures. JIT's old
16/16 is a control; default trimmed/AOT must now pass all sixteen. Keep negative
scenarios negative; success means their expected rejection assertion passes.

Additional acceptance IDs, each run on both hosts in all three flavors:

| ID | Required behavior and evidence |
|---|---|
| NS-01 | Startup: role registration statically installs authoring, generated context is installed before first use, definition/register/start succeeds without DI resolution during setup. Fresh-process missing-install control fails actionably; JIT missing-engine control stays intact. |
| NS-02 | Exactly one context: two providers/repeated calls use one context, no repeated constructor or captured provider/scope; different context rejects before constructor/mutation. Verify supplied context options/customization rejection. |
| NS-03 | Seal: first successful or failed authoring/type/codec lookup seals mode; late first registration after legacy use rejects, same-type generated registration remains a no-op, no public reset/disposal. Legacy JIT control uses no registration. |
| NS-04 | Concurrency: race registration against first use and another registration; barriers prove whole-context publication or explicit rejection, never partial metadata/reflection fallback. Reentrant constructor fails without deadlock. No sleeps. |
| NS-05 | Failed configuration: throwing constructor/invalid options publish nothing, preserve cause, permit valid retry before first use; both codec copies observe the same authoritative state. Use fresh subprocesses for isolation. |
| NS-06 | Fixed-byte parity: legacy JIT golden corpus equals generated JIT/full trim/AOT bytes and input/definition fingerprints, including strong IDs, null/nullable/enum/value DTOs, ordered lists/maps and nested failure causes; verify detachment. |
| NS-07 | Missing root/transitive metadata: correct SFE-TYPE-003/WorkflowDefinitionException/TryBuild details; zero user-step execution, commit, key binding or admission. Retrying with unsupported shape retains SFE-TYPE-002 rather than relabeling it. |
| NS-08 | Dynamic value/codec negatives: absent runtime type, custom converter, serialization-only context, cyclic/unapproved polymorphic/inaccessible shape, forbidden collection and oversized value reject before commit/event consumption; existing waits and accepted instance remain intact. |
| NS-09 | Closed generic execution: named and delegate steps plus query/output for reference, struct, nullable and enum values; selectors/snapshots/merges execute without runtime-only generic code or reflective constructor/property discovery. |
| NS-10 | Typed incoming/outgoing event and StepResult wait, timer delay/retry backoff, resource grant, root Parallel and bounded ForEach; verify ordering, root-only composition, physical ownership and failure aggregation. Every selected dispatch/risk category has an executable path. |
| NS-11 | Retention and shutdown controls under generated metadata: pins/key expiry, Closed drain, acceptance race/abort at Stopping, truthful Incomplete, physical settlement/Completed and no post-Completed user/DI/commit work. |
| NS-12 | Exact public compile/package shape: metadata method available from package OrcaCore, no internal bridge/extra public API, generated bootstrap first-use in a fresh process, multiple providers and missing-engine recipe checks. Required CI uses dotnet test. |

Every required ID has explicit assertions and per-cell outcomes. Zero scenarios
may be omitted because an earlier one failed. A runtime failure keeps the support
target incomplete; it is not an accepted Unsupported outcome for this issue.
Extra subtype/collection closures are documented application prerequisites,
never unbounded runtime discovery. An inability to preserve bytes or support a
promised path requires contract amendment before continuing that implementation.

## Diagnostic and claim gate

Enumerate every trim/AOT/source-generator warning individually, including new
warnings reached after old blockers are removed. Map the previous 90-risk source
inventory to current sites and record downstream audit additions. Eliminate
OrcaCore-attributable supported-path findings. Other toolchain/dependency findings
need an exact cause, safety proof and independent approval, not suppression.
Do not add IsAotCompatible/IsTrimmable or public annotations outside the catalogue
without a reviewed whole-exposed-surface assessment. No all-package badge follows
from passing this finite recipe. Keep old assessment/release facts dated.

Normal OpenSpec/Release/Infrastructure, required unit/provider/integration and
coverage gates accompany native evidence. Runner changes are separate work.
No numeric performance gate or faster/smaller claim: optional matched measurements
must keep process memory and managed estimates distinct. Preserve compact raw
receipts and remove owned caches/publishes/build directories after verification.
