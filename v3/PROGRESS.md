# v3 Implementation Progress

Tracks task completion for the `v3/` workspace specifically. The shared
`docs/implementation/phases/*/PROGRESS.md` files are also written to by an
independent parallel workspace (`v3-gpt/`) — this file is the authoritative,
conflict-free log for `v3/` and is appended to instead going forward.

## Phase 0 — skeleton

T0-01 | done | 2026-07-02 | deviations: v3/global.json pins installed SDK 10.0.301 (rollForward latestFeature) because root global.json requests unavailable 10.0.200; xUnit v3 test projects scaffolded via the official `xunit.v3.templates` (xunit3, mtp-v2 runner) rather than the stock `xunit` template, which still emits xUnit v2; OrcaCore.ProviderCertification uses `xunit.v3.extensibility.core` (plain classlib, not executable) since `xunit.v3`/`xunit.v3.mtp-v2` require an executable test project
