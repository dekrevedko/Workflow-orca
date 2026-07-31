# OrcaCore semantic appendix

**Status:** proposed non-normative review and verification aid; publication is pending independent approval under task `4.15` and canonical synchronization under task `10.14`.

The accepted normative contract remains the selected-mode matrix and canonical OpenSpec baseline.
This draft summarizes consequences of the active change deltas; it does not create another promise
and SHALL NOT be published under `docs/specs/` before approval and canonical synchronization.

**Implementation status:** these laws describe the proposed normative target, not a blanket claim
that the current product source already conforms. In particular, L4 remains reserved pending task
4.16, L5's contributor boundary remains pending task 5.14, and the no-live-fiber-resource portions
of L8/L9 remain pending task 5.13. Those gaps are recorded as `ExpectedRed` in the active change.

## Citation rule

Every law must cite the normative requirement it abstracts. A statement without such a citation is
either a missing requirement or an invented claim and must not appear here as a law. A law may be
weaker than its cited requirement but must not be stronger.

## Laws

### L1. Commit-order invariance

For a fixed set of logical child outcomes `O`,

```text
canon(permute(O)) = canon(O)
```

and merge is applied to `canon(O)`. This does not quantify over schedules: different schedules may
produce different outcome sets through deadlines, races, or external observations.

Normative sources:
[state-driven-runtime: Interpreter executes control flow deterministically](../specs/state-driven-runtime/spec.md) and
[structured-fiber-execution: Merge is explicit, deterministic, and side-effect free](../specs/structured-fiber-execution/spec.md).

### L2. Committed-state replay

Let `project_state` remove physical invocation and external-system observations. For a recoverable
durable execution:

```text
project_state(execute(w, crash)) = project_state(execute(w))
```

External effects are equivalent only when the application adapter satisfies the documented
create-or-observe contract using `StepOperationId`.

Normative sources:
[durable-runtime: Durable mutation is crash-safe](../../../specs/durable-runtime/spec.md),
[reshape durable-runtime delta: Scope transitions and effects commit atomically](../specs/durable-runtime/spec.md), and
[workflow-contracts: Step operation identity is stable and opaque](../specs/workflow-contracts/spec.md).

### L3. Codec idempotence

For a supported author-normalized value `v`:

```text
encode(decode(encode(v))) = encode(v)
```

This is equality of canonical encoded bytes, not CLR reference identity or arbitrary `Equals`
behavior.

Normative source:
[workflow-contracts: Durable values use one fixed detached codec](../specs/workflow-contracts/spec.md).

### L4. Reserved

The proposed build-agreement law is intentionally not published until implementation task 4.16
lands. The normative lifecycle requirement already governs the target behavior; this appendix does
not claim that the current implementation satisfies it.

### L5. Fingerprint factorization

Let `q` erase opaque delegate/adapter behavior while preserving inspectable authored structure and
codec format. The structural fingerprint factors through that quotient:

```text
fingerprint = hash_structural after q
```

Detection of a designated structural mutation additionally assumes the hash separates that
mutation. Collision resistance is an engineering assumption exercised by certification, not a
mathematical theorem. Compiler format, mode, definition identity/version, and compiler options are
separate bindings rather than fingerprint inputs.

Current implementation note: task 5.14 implemented this boundary on 2026-07-29 and its exact
mutation/non-mutation matrix is green; independent Section 4/5 exit review remains pending.

Normative source:
[workflow-contracts: Executable plan identity is explicit](../specs/workflow-contracts/spec.md).

### L6. Durable lease capacity accounting with resize debt

For lease obligations whose status is `PendingCommit`, `Held`, `ReviewMarked`, `AmbiguousHeld`, or
`Quarantined`:

```text
ReservedUnits = sum(units(obligation))
ResizeDebt = max(0, ReservedUnits - ConfiguredCapacity)
```

A downward resize may therefore make `ReservedUnits > ConfiguredCapacity`. No new grant occurs
while debt is positive or when the complete next request would exceed configured capacity.
Availability recovery still depends on trusted causal stop/fence proof where ambiguity exists.

Normative sources:
[reshape durable-runtime delta: Durable leases are lexical occurrence-owned obligations](../specs/durable-runtime/spec.md) and
[quality-and-verification: Durable lease guards prove protocol behavior](../specs/quality-and-verification/spec.md).

The lease requirement remains in the active change delta until Section 6 performs its canonical
promotion. This citation records the approved normative target; it does not claim current product
conformance or authorize Section 6 implementation.

### L7. Path-token join freedom

No path token is retained by the parent across a root fan-out join edge. Therefore path-token
capacity alone cannot create a parent-held-token deadlock when the ceiling is one. This is not a
claim of item-admission progress or global workflow progress.

Normative sources:
[structured-fiber-execution: Local fibers use bounded execution-path scheduling](../specs/structured-fiber-execution/spec.md) and
[workflow-contracts: Execution-path tokens have one countable model](../specs/workflow-contracts/spec.md).

### L8. Fixed-branch existence

For fixed root `Parallel(B)`, all `B` branch fibers exist when the scope starts. Runnable branches
queue fairly for path tokens by authored ordinal. There is no separate branch or live-fiber
admission resource in v1 workflow semantics.

Normative sources:
[structured-fiber-execution: Structured scopes preserve the parent fiber](../specs/structured-fiber-execution/spec.md) and
[workflow-contracts: Execution-path tokens have one countable model](../specs/workflow-contracts/spec.md).

### L9. ForEach admission is conditional

Let `C_path` be the host path ceiling and `C_node` the node-local value when present (otherwise
`C_path`). The admitted nonterminal item bound is:

```text
A = min(C_path, C_node)
```

Parking releases a path token but retains the admitted-item slot. Every item eventually
terminalizes only if admitted items do not depend on effects produced exclusively by pending items.
That proviso is a property of authored work, not a runtime guarantee.

Normative sources:
[structured-fiber-execution: Bounded root ForEach uses dynamic isolated item fibers](../specs/structured-fiber-execution/spec.md) and
[workflow-contracts: Execution-path tokens have one countable model](../specs/workflow-contracts/spec.md).

## Deliberately excluded claims

- Merge is symmetric.
- Canonical ordering is invariant under every schedule.
- Replay makes external effects exactly once.
- Codec round-trip yields CLR-equal or reference-identical objects.
- `TryBuild` is unreachable after an eager diagnostic. It remains reachable.
- Every eager authoring rejection leaves the graph unchanged. That guarantee is proposed only for
  the five lifecycle rejections owned by task 4.16.
- Structural drift detection is collision-free by theorem.
- Ceiling-one path tokens imply global progress.
- Every `ForEach` item is unconditionally eventually admitted.
- The current implementation has no third concurrency quantity. The normative v1 model excludes
  one, but product source retains `MaxActiveFibers` until task 5.13 lands.
- Fan-out rank one is a computational complexity class. Step bodies remain arbitrary code.
- Scope-tree acyclicity rules out resource wait cycles.
- Fixed `Parallel` requires whole-set reservation.
- Root-only flattening or staging is a general equivalence.
- Compile-time peak computability derives the capability boundary.
