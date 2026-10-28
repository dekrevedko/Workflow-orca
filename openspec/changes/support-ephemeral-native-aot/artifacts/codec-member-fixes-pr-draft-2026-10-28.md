# Issue #13 PR draft — publication still gated

Title: Support the bounded ephemeral workflow recipe with trimming and Native AOT
Base: master
Head: codex/issue-13-native-aot-trimming

## Proposed body

The public ephemeral journey previously failed registration after trimming/AOT
removed runtime-discovered metadata. This change adds process-wide generated JSON
metadata and closed typed dispatch while preserving fixed wire bytes/fingerprints,
physical ownership, retention and shutdown. Refs #13.

Applications register the ephemeral role, register one default System.Text.Json
Metadata/Default context, then author definitions and build the provider. The exact
additive public surface is WorkflowJsonMetadata.Register<TContext>, accessibility
of the existing sealed strong-string converter, and the approved interface-metadata
annotation on StepExecutionThrottle.For<TStep>. The .NET 10 synthetic-initializer
omission restriction remains explicit; missing metadata reports SFE-TYPE-003.
No serializer hook, reset, new dependency/friend or package-wide badge is added.

Review corrections preserve write-only admission and validate values actually
serialized from included fields/properties. Unapproved runtime subtypes/collections
now reject in both modes instead of losing subtype data. Valid representations and
getter counts are unchanged; failed starts retain the existing key/instance rules.

[Native run 40762764617](https://github.com/dekrevedko/Workflow-orca/actions/runs/40762764617)
passes Windows Server 2025 win-x64 and Ubuntu 24.04 linux-x64: JIT 82/82, full-trim
55/55 and Native AOT 55/55 per host, 384 total. Every original journey, twelve NS
groups and thirteen mandatory subcases remain. Source, feed and harness identities
agree; trimmed/AOT JSON reflection is disabled and AOT dynamic code is unavailable.
The 24 individually classified warning records are the rejected handwritten-context
counterexample, with no supported product-path diagnostic hidden.

Native credit covers the selected OrcaCore/Core/Ephemeral public recipe: typed
values/wait parking, physical cancellation/join, timers/retries, transient pools,
Parallel and bounded ForEach. Live event delivery/resumption and outgoing publishing
are not exported and receive no native credit. Other engines/RIDs, broad package
badges and performance claims stay outside scope. Issue #3's earlier unsupported
assessment and released preview.2 remain historical; this PR publishes no packages.

Validation includes Release warnings-as-errors, strict OpenSpec, Infrastructure,
969 unit tests, 188 provider/integration tests, 50 consecutive CI-path stress runs,
79.3357% engine line coverage and scoped ReSharper with zero errors. The retained
validation record distinguishes the fresh binding checks from unchanged-source
lanes already run on the exact corrected bytes. PR CI must still pass.

## Local gate

This draft replaces the earlier task-3.1 draft for review. The correction target is
uncommitted, and only the temporary evidence branch was pushed. Checkpoint approval,
issue-branch push/PR, merge, issue closure and release remain separate gates. Merge
with a merge commit, never squash or rebase; attach any authorized created PR to
this chat and use the reviewed final body with its actual CI results.
