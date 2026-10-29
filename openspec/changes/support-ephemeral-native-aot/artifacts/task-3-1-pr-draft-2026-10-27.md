# PR draft — issue #13 (publication not authorized)

Title: Support the bounded ephemeral workflow recipe with trimming and Native AOT
Base: master
Head: codex/issue-13-native-aot-trimming

## Proposed PR body

The public ephemeral journey previously failed registration after trimming/AOT
removed runtime-discovered metadata. This change adds process-wide generated JSON
metadata and closed typed dispatch while preserving the fixed codec, fingerprints,
physical ownership, retention and shutdown contracts. Refs #13.

Applications register the ephemeral role, register one default System.Text.Json
Metadata/Default context, then author definitions and build the provider. The exact
additive surface is WorkflowJsonMetadata.Register<TContext>, accessibility of the
existing sealed strong-string converter, and the approved interface-metadata
annotation on StepExecutionThrottle.For<TStep>. No serializer hook, reset, new
package/dependency/friend or broad compatibility attribute is added.

The approved .NET 10 Generated-mode omission restriction rejects omittable synthetic
member initializers by declared shape (SFE-TYPE-002). Missing metadata is diagnosed
with SFE-TYPE-003 before effects. Unconfigured legacy JIT remains compatible.

[Native run 40629471843](https://github.com/dekrevedko/Workflow-orca/actions/runs/40629471843)
executes Windows Server 2025 win-x64 and Ubuntu 24.04 linux-x64: JIT 76/76,
full-trim 51/51 and AOT 51/51 per host. All sixteen original journeys, twelve NS
groups and thirteen mandatory subcases per cell pass; trim/AOT reflection remains
SDK-default disabled. The 24 deliberate non-product counterexample warning records
are individually classified; no supported product-path warning is hidden.

Native credit covers the bounded public recipe, including typed value/wait parking,
physical cancellation/join, timers/retries, transient-pool grants, root Parallel and
bounded ForEach. Live event delivery/resumption and outgoing publishing are not
exported by the selected closure and receive no native credit. Durable/DAG/providers,
other RIDs/architectures, whole-package badges and performance claims remain outside
scope. The approved boundary amendment is explicit; no issue #3 assertion is waived.

Implementation checkpoint 8566986 / user-verdict child 1ad2a59 preserve the reviewed
327-path target. Native evidence ran validation commit 609f1045 with identical product
and executable harness bytes. Publication removes only the temporary workflow push
trigger; its jobs/harness remain byte-identical. Preview.2 and issue #3's unsupported
assessment stay historical; no new public version is assigned or published by this PR.

Validation retained from the approved implementation: strict OpenSpec 25/25; Release
0 warnings/errors; Infrastructure 302/302; six unit projects 969/969; PostgreSQL 103,
SQL Server 74 and integration 11 passed; fifty consecutive unfiltered dotnet test
stress runs; CI-filtered engine line coverage 79.32% >=75%; ReSharper 0 errors with
retained triage. The publication-preparation lane results accompany the review.
Local provider execution used Windows plus actual Linux Docker databases; PR CI's
Linux provider job and release/registry checks must still run after authorization.

Merge using a merge commit, never squash or rebase. Issue closure and any package/
release publication remain separately authorized actions.

## Local preparation boundary

This file is a draft. Do not open a PR or push until expressly authorized. Task 3.1
is prepared for independent review; 3.2 stays open. The temporary validation branch
is evidence, not publication authority. Future PR publication must use the reviewed
final title/body above, with actual CI results, and attach the PR to this chat.
