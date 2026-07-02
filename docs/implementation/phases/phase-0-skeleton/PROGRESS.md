# Phase 0 Progress

Note: this repository currently carries two independent implementation workspaces under
these docs — `v3-gpt/` (see entries below, unlabeled) and `v3/` (entries labeled `[v3]`).
Treat the two logs as separate histories; do not reconcile or overwrite across them.

T0-01 | done | 2026-07-01 | deviations: v3-gpt/global.json pins installed SDK 10.0.301 because root global.json requests unavailable 10.0.200; ProviderCertification uses xunit.v3.extensibility.core because xunit.v3 requires executable test projects
T0-02 | done | 2026-07-02 | deviations: workflow commands run from v3-gpt so local global.json controls SDK selection; remote green/red GitHub Actions verification requires a pushed branch
T0-03 | done | 2026-07-02 | deviations: none
T0-04 | done | 2026-07-02 | deviations: none
T0-01 [v3] | done | 2026-07-02 | deviations: v3/global.json pins installed SDK 10.0.301 (rollForward latestFeature) because root global.json requests unavailable 10.0.200; xUnit v3 test projects scaffolded via the official `xunit.v3.templates` (xunit3, mtp-v2 runner) rather than the stock `xunit` template, which still emits xUnit v2; OrcaCore.ProviderCertification uses `xunit.v3.extensibility.core` (plain classlib, not executable) since `xunit.v3`/`xunit.v3.mtp-v2` require an executable test project
