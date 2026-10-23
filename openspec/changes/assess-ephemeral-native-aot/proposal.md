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
  requirements and application semantics are unchanged. Numbered owners NF-033
  and AC-555–562 are introduced in this task 2 transition, pending its review.

## Impact

Base: GitHub master `7895262a21f19741c0b5b7fbad30907bccff2cbb`, including issue #2's
two-parent merge. Branch: `codex/issue-3-ephemeral-aot`.

The approved contract checkpoint owns the planning and provenance prerequisite.
This task 2 target appends its four canonical blocks, adds numbered owners and
implements only the public external consumer, workflow, process guards and evidence.
Product source, dependencies and all API/compile/package baselines remain unchanged.
No new public type, member, annotation, DI option, fingerprint, serializer hook,
dependency or friend is proposed. Existing published packages gain no support badge.

Task 2 implementation is limited to an external consumer, qualification automation
and evidence/docs. Durable providers, dynamic DLL/plugin loading, alternate
serializers, broad library compatibility metadata and product fixes are outside
that initial source target. Issue #1 archival and Core lane consolidation remain
separate maintenance work.

## Task 2 clarification under the contract approval (2026-10-21)

The immutable contract is checkpoint eea51c9 with verdict-only child dbd829e.
F1 preserves all six SDK-default cells and adds four explicitly labelled trimmed/
AOT JSON-reflection-enabled diagnostic cells, at most conditional. Their results
cannot replace a default failure. DefinitionCompilerOptions.cs:58 is added to the
new static inventory; the approved dated inventory remains immutable. No product
source, API, serializer or dependency amendment is implied.
