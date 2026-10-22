## Why

[Issue #3](https://github.com/dekrevedko/Workflow-orca/issues/3) asks whether the
selected public ephemeral journey survives trimming and Native AOT. Current
reflection/generic dispatch and the fixed codec have not been qualified. A
repeatable assessment must distinguish an unsupported result from a support claim.

## What Changes

- Define one assessment protocol for an ordinary package-only public consumer on
  Windows x64 and native Ubuntu 24.04 LTS x64: untrimmed self-contained JIT,
  trimmed self-contained JIT, and Native AOT. Run the published executables.
- Cover typed registration, named DI steps, reference/value state and output,
  cancellation/shutdown ownership, retries, bounded fan-out and codec failures.
- Account for every publish diagnostic and any exact local requirement. Unsupported
  or environment-blocked cells stay explicit; a green assessment is not a green
  compatibility result. Keep `orcacore-json-v1` and its absence of replacement hooks.
- Record package/runtime/native dependency closure and comparable startup, total
  process CPU, process memory and managed-heap observations against equivalent JIT.
- Use a contract checkpoint followed by one coherent canonical/docs/consumer/lane
  and assessment target. Any product remediation or public amendment requires a
  separately reviewed proposal; it is not implicit in this research issue.

## Capabilities

### New Capabilities

None. This creates no new runtime capability or product-support promise.

### Modified Capabilities

- `quality-and-verification`: four ADDED assessment/evidence requirements. Existing
  requirements and application semantics are unchanged. Proposed numbered owners
  are NF-033 and AC-555–562, introduced only in the later approved transition.

## Impact

Base: GitHub master `7895262a21f19741c0b5b7fbad30907bccff2cbb`, including issue #2's
two-parent merge. Branch: `codex/issue-3-ephemeral-aot`.

This contract target owns planning/deltas, static source-risk and document routing
records, and the complete must-be-green provenance prerequisite. Product source,
consumer/test/workflow source, canonical specs, numbered requirements and all
API/compile/package baselines stay unchanged until independent contract approval.
No new public type, member, annotation, DI option, fingerprint, serializer hook,
dependency or friend is proposed. Existing published packages gain no support badge.

Later implementation is limited to an external consumer, qualification automation
and evidence/docs. Durable providers, dynamic DLL/plugin loading, alternate
serializers, broad library compatibility metadata and product fixes are outside
that initial source target. Issue #1 archival and Core lane consolidation remain
separate maintenance work.
