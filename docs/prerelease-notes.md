## Unreleased — DAG package verification

The candidate-package check runs a public DAG using five direct package references
and the in-memory provider. It reopens a run, verifies that a dependent node returns
`42`, and observes a child's cancellation from a second run handle. Full DAG
progression and SQL certification remain with the DAG track.
[Consumer](../tests/OrcaCore.DeveloperSurface.Guards/PackageFixtures/DagHosting/Program.cs) · [Package check](../eng/Test-Packages.ps1)

## Unreleased — SQL Server support

PostgreSQL is the v1 production storage target. The SQL Server package stays available as an experimental provider with real-database correctness certification and no qualified scale envelope. Its ordinary workflow and governance stores use shared documents. DAG storage uses separate relational tables.

## Unreleased — durable resource governance

Custom durable providers must implement `IDurableResourceGovernanceStore.LoadLatestAsync`.
Read the partition version and its latest full-state record from the same committed
snapshot. Return version zero and a null record for an empty partition. For a
positive version, the record sequence must equal that version. The existing
`LoadAsync` operation remains available for full-history inspection.

The runtime uses the latest v1 record for startup, management reads and mutation
retries. A damaged current record stops recovery. Use the full-history read to
check earlier record sequences and checksums. Experimental SQL Server still
transfers the whole JSON document on a read and rewrites it on a mutation.

## Unreleased — shared workflow value codec

Durable inputs, ephemeral detached state and DAG inputs now use the same
`orcacore-json-v1` check. Reconstructing a value with its declared type must
produce the same JSON bytes when encoded again. For example, if a private setter
causes `{"Value":17}` to decode as zero, the codec rejects a new submission
before binding its start key. A writable member or a matching constructor can
preserve 17.

**Durable initialization.** After a durable start binds its key, an `Init`
callback that throws or returns state the codec cannot round-trip now leaves
the instance `Failed`. Retrying with the same key and input returns that instance
with its recorded failure, including from another host. If the workflow deadline
expires before initialization finishes, the instance becomes `TimedOut`.

Both outcomes can occur before the first state checkpoint. In that case,
`GetStateAsync<TState>()` with the declared state type throws
`InvalidOperationException` because there is no readable committed state
checkpoint. Earlier builds could run `Init` after the deadline and store its
result, including a default value, in the timeout checkpoint. Use
`GetSnapshotAsync()` to read the terminal status and diagnostic.

**Existing durable stores.** A typed state or output query can fail when stored
bytes cannot round-trip. The query leaves the stored record intact. A running
workflow whose continuation repeatedly fails to load an incompatible value can
park with a poison diagnostic. Re-arming retries the same bytes. A well-formed
DAG run input that its installed definition cannot reconstruct makes the run
unavailable on that host.

If an earlier write already stored zero or omitted the business value, even a
successful read cannot recover the original 17. Recovery needs the original
input or another authoritative copy. For greenfield development, use a fresh
store with matched binaries and recreate affected runs from that source. This
change provides no automatic migration for older instances.

## Published 0.1.0-preview.3

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
