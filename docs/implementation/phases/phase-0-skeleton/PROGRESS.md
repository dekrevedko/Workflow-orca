# Phase 0 Progress

Note: this repository currently carries multiple independent implementation workspaces —
`v3-gpt/` (entries below), `v3/`, and `v3-cursor/`. The `v3/` and `v3-cursor/` workspaces
track their own progress in workspace-local notes to avoid conflicting edits to this shared
file; do not reconcile or overwrite across the logs.

## v3-cursor

T0-01 | done | 2026-07-02 | deviations: workspace path is v3-cursor/ (not v3/); global.json pins SDK 10.0.301 because root global.json requests unavailable 10.0.200; xUnit v3 executable test projects use xunit.v3.mtp-v2 + Microsoft.Testing.Platform; ProviderCertification uses xunit.v3.extensibility.core because xunit.v3 requires executable test projects

## v3-gpt

T0-01 | done | 2026-07-01 | deviations: v3-gpt/global.json pins installed SDK 10.0.301 because root global.json requests unavailable 10.0.200; ProviderCertification uses xunit.v3.extensibility.core because xunit.v3 requires executable test projects
T0-02 | done | 2026-07-02 | deviations: workflow commands run from v3-gpt so local global.json controls SDK selection; remote green/red GitHub Actions verification requires a pushed branch
T0-03 | done | 2026-07-02 | deviations: none
T0-04 | done | 2026-07-02 | deviations: none
