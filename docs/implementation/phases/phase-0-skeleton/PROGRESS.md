# Phase 0 Progress

Note: the active implementation is now the repository root. This append-only log retains
historical entries from the former parallel workspaces; do not reconcile historical logs or
write active code under `archive/legacy-poc/`.

## current implementation

T0-01 | done | 2026-07-01 | deviations: global.json pins installed SDK 10.0.301 because root global.json requests unavailable 10.0.200; ProviderCertification uses xunit.v3.extensibility.core because xunit.v3 requires executable test projects
T0-02 | done | 2026-07-02 | deviations: workflow commands run from current implementation so local global.json controls SDK selection; remote green/red GitHub Actions verification requires a pushed branch
T0-03 | done | 2026-07-02 | deviations: none
T0-04 | done | 2026-07-02 | deviations: none
