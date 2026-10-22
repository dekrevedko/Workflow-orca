# Assessment harness controls — 2026-10-21

The Python controls are synthetic record mutations, never native execution credit.
The current nineteen controls cover missing scenarios/hosts, broken JIT, impossible
claims and reflection settings, lost conditions, missing assertions, shortened
measurement denominators, mislabeled memory, unclassified/changed/dropped warnings
and fixture rejection as execution evidence. Targeted diagnostic controls assert their intended failure
reason; general malformed records must raise a validation error. The runner-level disappearance control first failed because the runner
omitted a formerly classified diagnostic if it no longer appeared. The corrected
runner retains that identity with actual count zero, which fails count matching
unless an explicit Corrected/expectedCount=0 decision and its evidence exist.
A companion control accepts that explicit correction and keeps the zero-count row.
All nineteen controls now pass; the initial failure log is retained locally.

The public consumer control executes sixteen assertion journeys against packages,
and the local repeated composition control completed thirty runs. Native receipt
hashes identify the exact consumer/harness used for qualification separately from
these unqualified Windows 11 development controls. No assertion or product behavior
was relaxed to classify an unsupported deployment.

The first real native discovery reproduced two cause-reporting defects on both
hosts: AOT-06 waited for body entry after StartOrGetAsync had faulted, and AOT-13
replaced an unexpected earlier facade error with its JsonException assertion.
The revised drain observes either entry or a completed start, propagating a start
fault immediately; a still-pending correct JIT start retains every drain assertion.
The cycle case rethrows an unexpected exception with its original dispatch stack,
while still requiring the same JsonException and cycle message for success. Explicit
Register/Start phase markers remove stale DefinitionBuild labels. Both fixes pass
the unchanged sixteen-scenario JIT assertions; final native reruns must expose the
underlying product failures, with neither timeout nor ASSERTION accepted as an
unsupported profile. No product source or existing runtime test changed.

Emitter attribution is derived from the actual compiled namespaces: the conditional
ORCACORE_CORE_CODEC_COPY namespace Core.Internal denotes the linked Core assembly,
while the original OrcaCore.Internal copy belongs to OrcaCore. A parser control
checks both forms and the original raw line. The intermediate classified native
run reproduced behavior but had the former copy misattributed; final qualification
is rerun with the corrected parser and source/package/harness identities.
