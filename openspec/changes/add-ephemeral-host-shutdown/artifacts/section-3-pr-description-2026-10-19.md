# Add cooperative shutdown and drain ownership for ephemeral hosts

Addresses #2.

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

The publication follow-up removes settlement-order and synchronous-cancellation
assumptions in two tests without changing product source or removing any slot,
attempt, timer or wait checks.
The source checkpoint and its independent approval are preserved as separate commits.

Publication validation: OpenSpec strict 23/23, Infrastructure 256/256, and 40
consecutive unfiltered 352-case `dotnet test` runs launched as 20 concurrent pairs.
The PR's CI re-runs the unit, provider/integration, package and coverage gates.

Follow-up: consolidate the ephemeral lane's ownership hooks into Core's work lane
in a later change so future channel fixes do not need to be maintained twice.
