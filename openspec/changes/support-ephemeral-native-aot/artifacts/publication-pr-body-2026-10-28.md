The public ephemeral journey previously failed after trimming/AOT removed
runtime-discovered metadata. This change adds process-wide generated JSON metadata
and closed typed dispatch, qualifying the bounded public recipe on .NET 10.
Refs #13.

Applications register the ephemeral role, register one default System.Text.Json
Metadata/Default context, then author definitions and build the provider. The
additive public surface is WorkflowJsonMetadata.Register<TContext>, accessibility
of the existing sealed strong-string converter factory, and the approved interface
metadata annotation on StepExecutionThrottle.For<TStep>. Missing metadata reports
SFE-TYPE-003. The explicit .NET 10 omittable synthetic-initializer restriction
reports SFE-TYPE-002.

Codec corrections preserve write-only admission and validate actual serialized
values from included fields/properties. Unsupported runtime subtypes/collections
reject in both modes instead of silently losing data. Valid bytes/fingerprints,
getter counts, key ownership, physical settlement, retention and shutdown remain
covered by regressions.

[Native run 40762764617](https://github.com/dekrevedko/Workflow-orca/actions/runs/40762764617)
passes all six cells: JIT 82/82, full trim 55/55 and Native AOT 55/55 on each of
Windows Server 2025 win-x64 and Ubuntu 24.04 linux-x64, 384 total. Source, feed and
harness identities agree. The original journeys, twelve NS groups and thirteen
mandatory subcases remain. Trimmed/AOT JSON reflection is disabled; AOT dynamic
code is unavailable. The 24 individually classified diagnostics are the rejected
handwritten-context counterexample; no supported product-path warning is hidden.

Validation: Release warnings-as-errors 0/0, OpenSpec 25/25 and Infrastructure
309/309. Unchanged corrected-source evidence includes 969 unit tests, 188 provider/
integration tests, fifty consecutive unfiltered dotnet test stress runs and 79.3357%
CI engine line coverage. Scoped ReSharper reports zero errors and no new findings.
The approval binding resolves corrected checkpoint d8c55f3 and its sole review
child 25fa36c through Git, with direct user approval recorded separately from the
author self-review. PR CI must pass before merge.

Qualification covers OrcaCore/Core/Ephemeral typed values/wait parking, physical
cancellation/join, timers/retries, transient pools, Parallel and bounded ForEach.
Live event delivery/resumption, outgoing publishing, other engines/RIDs, broad
package badges and performance claims remain outside native credit. Released
preview.2 and issue #3's earlier unsupported assessment remain historical. No new
package version, release or registry publication is part of this PR.

Merge with a merge commit.
