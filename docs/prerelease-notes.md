OrcaCore `0.1.0-preview.3` for .NET 10. This prerelease adds bounded Native AOT
and trimming support for the ephemeral engine from
[issue #13](https://github.com/dekrevedko/Workflow-orca/issues/13) and
[PR #14](https://github.com/dekrevedko/Workflow-orca/pull/14).
APIs, provider schemas and persistence formats may change.

## Ephemeral Native AOT and trimming

- The selected public-package closure is `OrcaCore`, `OrcaCore.Core` and
  `OrcaCore.Engine.Ephemeral`. The qualified profiles are self-contained JIT,
  fully trimmed JIT and Native AOT on Windows Server 2025 / win-x64 and
  Ubuntu 24.04 / linux-x64. This does not qualify other engines, platforms,
  runtime identifiers or every API in the twelve-package distribution.
- Register application source-generated JSON contexts using
  `WorkflowJsonMetadata.Register<TContext>()` before authoring/registering a
  definition or starting engine work. Use Metadata or Default generation,
  the fixed-codec contract's supported declared shapes and strong-value converter
  catalogue. Install the ephemeral engine before definition registration in
  trimmed/AOT applications. The registry is process-wide and seals at first use.
- Trimmed and AOT consumers use disabled JSON reflection without enabling a
  runtime switch, blanket warning suppression, private rooting or serializer hooks.
  Typed dispatch replaces selected reflective/dynamic paths. Missing metadata
  reports `SFE-TYPE-003`; unsupported declared shapes report `SFE-TYPE-002`.
- Write-only admission and included-member validation match the fixed codec,
  preserving exact serialized bytes, fingerprints and getter counts. The explicit
  .NET 10 generated omittable-member initializer restriction still applies.
- [Native source qualification run 40762764617](https://github.com/dekrevedko/Workflow-orca/actions/runs/40762764617)
  passed 384 cases across six native cells with one exact candidate feed. This is
  source evidence, not proof that a later release package passed publication.
  Normal publication now also gates the exact release CI package feed on those
  six cells before uploading release assets or publishing either registry.
- Live event delivery/resumption and outgoing publishing receive no native credit.
  The [support guide](https://github.com/dekrevedko/Workflow-orca/blob/master/docs/ephemeral-native-support.md)
  documents startup order, metadata restrictions, qualification evidence and limits.
  Preview.2 retains its historical unsupported trimmed/AOT result.

## Packages and installation

The release assets contain all twelve `.nupkg` files and `SHA256SUMS`. After
publication succeeds, install `OrcaCore.Engine.Ephemeral` at `0.1.0-preview.3`
from NuGet.org or the authenticated GitHub Packages NuGet feed. Use the same
version throughout the selected closure. GitHub release downloads can also form
a directory feed; a release URL itself is not a NuGet feed. See
[installation instructions](https://github.com/dekrevedko/Workflow-orca/blob/master/docs/packages.md).

The same verified package files go to NuGet.org and GitHub Packages. Fresh-cache
registry consumers verify restore origin, source metadata and execution after
publication. Retention and cooperative shutdown from preview.2 are retained.
Durable DAG execution remains incomplete; this is not a stable release.
