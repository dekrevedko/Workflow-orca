# Ephemeral Native AOT and trimming support

Status: **published in 0.1.0-preview.3**. All twelve distribution packages are
verified on NuGet.org and GitHub Packages. The selected ephemeral recipe passed
all six cells, 384/384 cases, in [exact-asset qualification 40998543857](https://github.com/dekrevedko/Workflow-orca/actions/runs/40998543857).
See [the publication record](releases/0.1.0-preview.3/published-2026-10-29.md).

The 2026-10-28 preparation status was **source-qualified; release pending**.
Its historical source qualification requires the corrected
issue #13 source at checkpoint `d8c55f32741f43523e528dd45ad49e6a0fbe7837`,
followed by review-only child `25fa36c2ab79153e93c68f0d598779198c50cdbb`.
The user approved checkpointing after review; the author review is not external
approval. The exact authority and Git chain are recorded in
[the approval binding](../openspec/changes/support-ephemeral-native-aot/artifacts/publication-codec-approval-binding-2026-10-28.json).
The corrections preserve write-only admission and validate actual serialized
member values without extra getter reads. Unsupported runtime values in included
fields/properties now reject in both modes rather than silently losing data.
Published `0.1.0-preview.2` does not contain `WorkflowJsonMetadata` and keeps its
earlier unsupported trimmed/AOT assessment. Preview.3 is published from commit `6b4ab952376de366a102e236b5e2b5b1ddfade41` after PR #15's merge;
issue #13 remains closed. Its release package hashes match all six native reports,
and all 360 product/build inputs match the released source. The
[release preparation](release-0.1.0-preview.3.md) remains a dated planning record.

## Qualified profiles

[Native run 40762764617](https://github.com/dekrevedko/Workflow-orca/actions/runs/40762764617)
used one exact candidate feed, external PackageReference-only consumers, fresh
caches and real native machines:

| Host / RID | Self-contained JIT | Fully trimmed JIT | Native AOT |
|---|---:|---:|---:|
| Windows Server 2025 / win-x64 | 82/82 | 55/55 | 55/55 |
| Ubuntu 24.04 / linux-x64 | 82/82 | 55/55 | 55/55 |

All sixteen original journeys, twelve NS groups and thirteen mandatory subcases
per cell pass. Additional legacy controls account for the larger JIT count.
All 384 cases bind the same corrected source, feed and harness. Original golden
and strong-value corpora match the earlier approved baseline at 8566986 / 1ad2a59
and [run 40629471843](https://github.com/dekrevedko/Workflow-orca/actions/runs/40629471843),
whose dated records remain unchanged.
Trimmed/AOT JSON reflection stayed at its SDK-default disabled value; AOT also
reports dynamic code unavailable. The 24 individually reviewed diagnostics are
the deliberate handwritten-framework counterexample, which product registration
rejects before publication; no supported OrcaCore-path warning remains. New or
changed diagnostics fail the regression gate.

The selected closure is `OrcaCore`, `OrcaCore.Core` and
`OrcaCore.Engine.Ephemeral`, with its actual hosting/framework dependencies.
Use .NET 10 and the recorded SDK/toolchain; the source pin is in `global.json`.
The twelve-package distribution inventory does not qualify the other engines.
No package-wide `IsAotCompatible`/`IsTrimmable` badge, performance advantage,
other RID/architecture/OS, plugin or dynamic-assembly support is claimed.
Windows 11 preflight, WSL, containers and cross-publication do not fill native cells.

The public recipe covers named/delegate steps, value/reference/nullable/enum
closures, detached typed values, typed wait parking/cancellation/physical join,
timers, retries, transient-pool grants, root Parallel and bounded ForEach.
**Live event delivery/resumption and outgoing publishing receive no native credit**:
the selected public ephemeral closure exports neither capability. Their internal
engine regressions remain required; a future public capability needs a reviewed
contract and its own package-consumer proof.

## Build a candidate package consumer

Use [the local pack scripts](packages.md#build-local-packages-immediately) against
the approved source, with a private unused label such as
`0.1.0-preview.native13.local.1`. Keep all twelve feed files together and reference
only the engine role needed by the application. Keep the selected OrcaCore
packages at the same version; this label is not an existing registry release.

For example, create a console project outside repository MSBuild props, use an
explicit NuGet.Config pointing to that local feed plus NuGet.org, and set the
version to the one actually packed:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="OrcaCore.Engine.Ephemeral" Version="0.1.0-preview.native13.local.1" />
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="10.0.9" />
  </ItemGroup>
</Project>
```

Register the role, then the process-wide metadata, then author definitions. Build
the provider afterward. The role statically installs Core without resolving DI
services or constructing business steps. This small program returns `42`:

```csharp
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore;
using OrcaCore.Hosting;

var services = new ServiceCollection();
services.AddOrcaCoreEphemeralEngine(new EphemeralEngineHostOptions
{
    StructuredExecution = new()
    {
        MaxConcurrentExecutionPathsPerInstance = 2,
        StepThrottles = []
    },
    TransientPools = []
});
WorkflowJsonMetadata.Register<AppJsonContext>();
var definition = Workflow.Ephemeral<DemoState>(DefinitionId.New(), DefinitionVersion.Initial)
    .Init<int>(number => new DemoState(number, EventName.Create("demo")))
    .End(snapshot => snapshot.Value.Number).Build();

var provider = services.BuildServiceProvider();
var lifetime = provider.GetRequiredService<IEphemeralWorkflowHostLifetime>();
try
{
    var definitionHandle = provider.GetRequiredService<IWorkflowDefinitionRegistry>()
        .Register(definition).GetHandleOrThrow();
    var instance = (await definitionHandle.StartOrGetAsync(
        42, StartIdempotencyKey.Create("demo"))).GetHandleOrThrow();
    Console.WriteLine(await instance.WaitForOutputAsync());
}
finally
{
    var shutdown = await lifetime.StopAsync(Timeout.InfiniteTimeSpan);
    if (shutdown is EphemeralHostShutdownResult.Completed)
        await provider.DisposeAsync();
    else
        throw new InvalidOperationException("Retain the provider/dependencies and re-observe shutdown.");
}

public sealed record DemoState(int Number, EventName Label);

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(DemoState))]
[JsonSerializable(typeof(int))]
public partial class AppJsonContext : JsonSerializerContext;
```

The publication-preparation check builds and runs this exact sample in JIT with
a fresh package cache. The six native cells above refer to the versioned
assessment consumer, not a six-cell execution of this new snippet.

This finite demo joins shutdown before disposing its provider. Real applications
need an explicit dependency owner capable of retaining it after `Incomplete`;
use the [three shutdown/drain recipes](ephemeral-engine-developer-guide.md#shutdown-drain-recipes).
`ApplicationStopping` closes admission while accepted work drains; only engine
hosted/direct stop cancels and joins the remainder. Metadata does not alter those
rules, retention pins, settlement, start-key expiry or DI dependency ownership.

Choose the profile on the matching native host. For Windows x64:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishTrimmed=false -p:PublishAot=false -o out/jit
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishTrimmed=true -p:TrimMode=full -p:PublishAot=false -p:TrimmerSingleWarn=false -o out/trimmed
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishAot=true -p:TrimMode=full -p:TrimmerSingleWarn=false -o out/aot
```

On the qualified Ubuntu host substitute `linux-x64` and use the prerequisites in
[the manual native workflow](../.github/workflows/ephemeral-native-support.yml).
Do not set a reflection-enabling property, suppress warnings or root private/
whole assemblies to make an unsupported application pass. Broader application
models and changed toolchains need their own diagnostic review and execution.

## Process and type boundary

Combine every application input/state/output/event-value/item/result/collection
and declared derived closure into one ordinary generated Metadata or Default
context. Use default settings, no custom resolver, serialization-only context or
explicit converter options. Keep product strong-value attributes; the existing
public sealed `StrongStringValueJsonConverterFactory` lets the framework generator
reference those attributes. Do not add that factory to context converter options.

The registry is shared by both codec copies and all providers in the process.
First successful registration publishes atomically. Repeating that same CLR type
is a no-op, including after sealing, without constructing another context.
Different types, reentrant registration and registration after a legacy first-use
seal fail explicitly. Invalid construction/options publish nothing and permit
valid retry before workflow use; construction failures preserve their cause.
The registry does not capture or dispose a provider/scope, and host stop cannot
reset it. There is no public reset; a corrected sealed configuration needs restart.

`orcacore-json-v1` options, common-shape bytes/fingerprints, product converters,
limits, detachment and existing rejection rules stay fixed. The finite generated
closure adds the explicitly approved .NET 10 omission restriction: reject a
reachable JSON member when `AssociatedParameter.IsMemberInitializer` is true
and its `JsonIgnore.Condition` is present and not `Never`. For example, an init-only
Count initialized to 42 with WhenWritingDefault is rejected for both submitted
zero and seven. Use an ordinary setter, a genuine JSON-constructor-bound member,
or remove that omission policy. Ordinary positional records without omission,
mutable/constructor-bound controls and unconfigured legacy JIT remain supported.
An unused excluded type in an otherwise valid context does not reject registration.

- `SFE-TYPE-002`: unsupported declared shape, including that Generated-mode rule.
- `SFE-TYPE-003`: missing root/transitive generated metadata; add the finite closure
  and register before first use, or restart after correcting a sealed configuration.
- Build keeps `WorkflowDefinitionException`; TryBuild keeps its validation shape.
  Direct value boundaries throw `NotSupportedException` before mutation/consumption.
- Baked string-enum policy, explicit converters and handwritten renamed-property
  contexts reject even with otherwise-default runtime options; valid retry works.

Generic helpers forwarding `TStep` into `StepExecutionThrottle.For<TStep>` must
propagate its approved `DynamicallyAccessedMembers(Interfaces)` requirement, as
shown in [the engine guide](ephemeral-engine-developer-guide.md#issue-13-native-metadata-path-source-qualified-release-pending).
The concrete call syntax, capacity-first validation and physical throttle ownership
are unchanged.

The [task-2 evidence](../openspec/changes/support-ephemeral-native-aot/artifacts/task-2-5-validation-evidence-2026-10-27.md)
and [immutable native records](../openspec/changes/support-ephemeral-native-aot/artifacts/native-task-2-4-2026-10-27/index.json)
identify exact source, package, harness, host and diagnostic facts. Future package
publication must bind its new version/source to those qualified operations; this
guide does not make preview.2 pass or grant an unexecuted profile support.
