# Framework grounding for the proposed assessment

Checked 2026-10-20. These sources explain tool behavior; they are not OrcaCore
qualification evidence. Every native result, closure and measurement is still
unknown and belongs to the later approved consumer/lane target.

- [Native AOT overview](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/):
  publication is target-specific and self-contained. Windows needs the C++ toolchain;
  Ubuntu needs its native compiler/library prerequisites. Library compatibility
  properties enable analyzers and make compatibility declarations, so this contract
  does not turn them on. Six matched native-host cells are this proposal's bounded
  choice, not a claim covering every OS supported by .NET.
- [Prepare libraries for trimming](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/prepare-libraries-for-trimming):
  a self-contained consuming app exposes dependency analysis unavailable when
  building a library alone. Analyzer enablement and declaring a library trimmable
  are distinct. The later lane retains detailed dependency findings and tests the
  actual external app; today's normal Release build proves neither trimming nor AOT.
- [AOT warning guidance](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/fixing-warnings):
  runtime generic construction can require code absent from the native image.
  A warning is a compatibility finding, and actual execution still matters.
  Our fixed diagnostic ledger and explicit unsupported outcomes prevent a warning
  suppression from being treated as a passing deployment.
- [System.Text.Json source generation](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation):
  generated metadata is supplied through particular serializer overloads/options.
  In this repository the engine codec owns its private options. An app's report
  context cannot configure those calls; this is a source-grounded inference, not
  proof that every codec path fails under AOT. No serializer hook is proposed.
- [.NET 10 supported OS list](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md):
  framework platform support is independent of OrcaCore qualification. The proposal
  deliberately fixes Ubuntu 24.04 LTS and Windows 2025 x64 native hosts and records
  actual images/toolchains, avoiding moving ubuntu-latest labels and cross-OS claims.

Alternatives remain explicit: ReadyToRun is not Native AOT; a JIT success is a
control, not native proof; whole-product rooting/private-member descriptors and
reflection-default overrides are not a transparent fix for the fixed codec.
