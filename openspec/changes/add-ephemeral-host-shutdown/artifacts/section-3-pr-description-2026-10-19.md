# Proposed PR: Add cooperative shutdown and drain ownership for ephemeral hosts

Ephemeral hosted stop previously returned without cancelling or joining workflows,
allowing dependency disposal while their physical work was still running. The new
engine-owned lifetime closes admission at ApplicationStopping, permits accepted
work and initialized starts to drain, then cancels and joins remaining work when
engine stop begins. It returns detached Completed/Incomplete observations with
caller-local budgets and preserves dependency ownership on Incomplete.

The change adds exactly the seven reviewed hosting types, reserved singleton
registration, first-dispatch callback ownership, sealed timer/retention producers,
and stable accounting for physical work, lanes, pins and cleanup handoffs. It
includes the canonical approval binding, executable API/compile/package baselines,
drain/migration guides and the ordinary package-only consumer.

Validation and limits are recorded in the Section 3 qualification artifact. That
includes the required CI lanes, engine coverage, unfiltered dotnet-test repetitions
under load, and all three package-consumer ownership recipes. Published older
packages retain their prior behavior. Run/RunAsync still disposes in finally when
host stop throws; applications needing to retain Incomplete work must explicitly
own the host/provider/scopes and external dependencies.

This is review material only. Push and PR publication remain unauthorized pending
the independent Section 3 source/consumer review. Preserve checkpoint/verdict
ancestry with merge commits; no squash.
