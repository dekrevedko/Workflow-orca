# Assess ephemeral package trimming and Native AOT on Windows and Ubuntu

Closes #3.

The same public-package-only ephemeral consumer passes all sixteen scenarios on
native Windows Server 2025 and Ubuntu 24.04 JIT builds. Trimmed and Native AOT
builds remain unsupported with SDK defaults and with JSON reflection enabled:
defaults reject schemas, while extras expose later facade metadata failures.
Product code, the fixed codec, public API and package/dependency baselines are
unchanged. These are conclusions about the exact tested profile, not general
library compatibility promises.

The change adds the consumer and manual native assessment workflow, four canonical
requirements with NF-033 / AC-555–562, per-finding classifications, dependency and
artifact closure, and JIT measurements from three warm-ups plus twenty runs per
host. Unsupported-workload measurements stay unavailable. The temporary-branch
push trigger is removed.

Independent approvals: contract eea51c9 / dbd829e; assessment 7639189 / baae5ad.
[Native run 39721105482](https://github.com/dekrevedko/Workflow-orca/actions/runs/39721105482)
passed both native jobs and the same-source/package record gate. Local assessment
validation: OpenSpec 24/24, Release 0 warnings/errors, Infrastructure 259/259, six
unit projects 969/969, engine coverage 79.10%. Publication reruns strict OpenSpec
and Infrastructure; normal PR CI also gates database/integration and coverage.
