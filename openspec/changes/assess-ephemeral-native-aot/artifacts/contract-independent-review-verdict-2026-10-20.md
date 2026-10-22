# Issue #3 Native AOT assessment contract — independent review verdict

**Date:** 2026-10-20
**Target:** `codex/issue-3-ephemeral-aot`, base `7895262` (GitHub master, PR #8 merged), 19 entries,
tree `16c371baf6c40c779e9b11e0fa83ae4b814c87dc`, porcelain SHA-256 `07edabf5…`. The tree reproduced
through a private index, and nothing drifted during review.

## Verdict: APPROVE, with one binding clarification (F1) folded into the task 2 target

The contract is sound and well scoped:
- one package-only consumer;
- native Windows and Ubuntu cells, each untrimmed JIT, trimmed JIT and Native AOT, with Docker, WSL
  and cross-publishing excluded;
- per-diagnostic classification with no blanket suppression;
- explicit scenario and platform outcome vocabularies, where a green record gate is not a support
  claim;
- the fixed codec preserved, with no serializer hook;
- measurement definitions that do not equate process memory with managed-heap estimates.

It changes no product, API or dependency. Only the planning directory and two mechanical provenance
pins change. It covers each issue acceptance criterion, and keeps dynamic DLL loading and durable
providers out of scope.

**Authorized now:**
1. Stage exactly the 19 manifest paths, excluding this verdict. Confirm tree `16c371ba…` with parent
   `7895262`, then commit.
2. Add a verdict-only child commit; 1.4 may be ticked there.
3. Prepare task 2 as **one** coherent target: canonical, docs, consumer, workflow and evidence,
   including F1. That review checks F1's exact wording.

There is no separate contract round.

## F1 — P2 — The JSON reflection feature switch decides every trimmed/AOT cell; the contract must say how it is treated

**Problem.**
- `FixedWorkflowValueCodec` builds `new JsonSerializerOptions()` with no `TypeInfoResolver`.
- `DefinitionCompilerOptions.cs:58` calls `JsonSerializerOptions.Default.GetTypeInfo(type)` and
  treats `NotSupportedException` as "unsupported declared type".
- With `PublishTrimmed` or `PublishAot`, the SDK defaults `JsonSerializerIsReflectionEnabledByDefault`
  to false. Reflection-based JSON then fails at runtime.

The four trimmed and AOT cells are therefore expected to fail at the first registration or codec use.
That failure may surface as a misleading "unsupported type" definition error rather than a codec
failure. Every later scenario would then be `BlockedUpstream`, with no native measurements. The facade
and runtime-generic findings that the assessment exists to discover would never be reached.

The spec bans "unchecked runtime-feature switches" but does not say whether this switch is allowed,
required or forbidden.

**Required in the task 2 target:**
- **Default cells.** Each trimmed or AOT cell runs first with SDK defaults (switch unset). Record the
  exact exception type and message and the public failure classification, including whether
  registration misreports the cause.
- **Switch-enabled cells.** Either:
  - add explicitly labelled diagnostic cells per OS that set
    `JsonSerializerIsReflectionEnabledByDefault=true`. Classify the switch as a candidate
    `LocalRequirement`, account for its IL2026/IL3050 diagnostics, and allow downstream discovery and
    measurement. These cells can be at most `AssessedConditional`, never `AssessedCompatible`;
  - or state in the spec and protocol that the switch is forbidden and why, and accept that the
    default failure ends those cells.

  Recommendation: the first option.
- **Static inventory.** Add `DefinitionCompilerOptions.cs:58` (`JsonSerializerOptions.Default.GetTypeInfo`)
  to the static risk inventory. It is currently missing.

## Lanes

I did not re-run the lanes. This is a contract-only target with mechanical pins, and the reported
results (OpenSpec 24/24, Release 0/0, Infrastructure 256/256) belong to the exact tree I reproduced.
The task 2 review runs the lanes and executes the consumer.
