# Framework/source grounding — 2026-10-23

These official sources informed the design; none is an OrcaCore compatibility claim.

- [System.Text.Json source generation](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation): generated JsonSerializerContext/JsonTypeInfo contracts provide statically known metadata. The context must cover collection/runtime-value closure as well as roots. OrcaCore must actually use the metadata internally; a context for the assessment's report output cannot affect its private codec.
- [Native AOT deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/): ahead-of-time closed code and native prerequisites, supported metadata limits, and AOT analysis. Runtime generic construction/dynamic code assumptions need explicit remediation; a successful publication does not establish workflow execution.
- [Preparing libraries for trimming](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/prepare-libraries-for-trimming): analyze both libraries and published test applications; EnableTrimAnalyzer permits analysis without prematurely asserting IsTrimmable. Whole-library annotations carry a broader claim than a selected consumer.
- [PublishTrimmed JSON default](https://learn.microsoft.com/en-us/dotnet/core/compatibility/serialization/8.0/publishtrimmed): trimming disables reflection-based JSON by default. The support protocol keeps that default and checks it at runtime.

Live source at base 3b7f98a confirms: DefinitionCompilerOptions uses
JsonSerializerOptions.Default.GetTypeInfo; both codec copies use static private
options; TypedAuthoringBoundary can Assembly.Load Core; the ephemeral facade
reconstructs identity and generics from objects/Types; StructuredInvocationCache
builds expression/delegate/context/snapshot paths; typed waits close an accessor
with MakeGenericType. Existing Abstractions friend access already includes Core
and Ephemeral, so direct product constructors and shared internal metadata need
no new friend. The source inventory in the approved issue #3 task-2 artifacts
contains 90 risk rows, including the schema-resolution site. This contract's
audit extends that inventory through every reached warning and downstream path.

Native run [39721105482](https://github.com/dekrevedko/Workflow-orca/actions/runs/39721105482)
is the historical failure/control evidence, not a validation run of proposed fixes.
No native product fix or new compatibility measurement was executed here.
