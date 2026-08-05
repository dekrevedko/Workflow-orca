# Harmonize downstream capability specs — approved canonical synchronization record

**Date:** 2026-08-01  
**Scope:** post-approval canonical synchronization under tasks `1.5` and `2.1`–`2.3`  
**Product implementation scope:** none

## 1. Approval consumed

Task `1.5` consumed the immutable independent `APPROVE` verdict:

- path:
  `docs/review/harmonize-downstream-capability-specs-reduced-scope-remediation-independent-planning-rereview-verdict-2026-08-01.md`
- SHA-256:
  `97099C0E79D49432C33192B3F1AE0F78DA8F0EAE41E25DF6045705EEDB0010AB`

The approval authorized canonical synchronization of only `event-driven-prototype` and
`state-driven-runtime`. It did not authorize harmonize to synchronize messaging,
persistence/outbox, repository-friend, or durable-governance requirements owned by
`reshape-developer-facing-interfaces`.

## 2. Exact canonical synchronization

| Capability | ADDED | MODIFIED | REMOVED | Purpose revised |
|---|---:|---:|---:|---:|
| `event-driven-prototype` | 2 | 0 | 4 | yes |
| `state-driven-runtime` | 0 | 1 | 0 | yes |
| **Total** | **2** | **1** | **4** | **2** |

The event-driven prototype canonical spec now contains exactly these two requirements:

1. `The event-driven prototype is outside the first release`
2. `Future prototype work requires a reviewed capability amendment`

The four former implemented-prototype requirements were removed. The Purpose now records planning
history outside v1 and the reviewed gate for any future return.

`Ephemeral mode has explicit limitations` now makes ordinary `Wait` residency a runtime/hosting
policy, rejects restart-safe rehydration, and keeps root `ContinueAsNew` and scoped
`AcquireResources` absent from the ephemeral builder. Its Purpose carries the same policy without
introducing a separate authored long-wait node.

Canonical post-sync artifacts:

| Artifact | Lines | SHA-256 |
|---|---:|---|
| `openspec/specs/event-driven-prototype/spec.md` | 24 | `BBDF7DFC01C7781DC3895B11A20B083CAB7B2861BBFB42D2ACDBEE89340D3423` |
| `openspec/specs/state-driven-runtime/spec.md` | 93 | `A94915AED0B1182064C8B2EA9C9185BA3C381E5E223E3AAE7E900D35A0F9C83F` |

## 3. Reshape-owned capability exclusion

The following canonical working-tree blob hashes were frozen immediately before synchronization
and reproduced unchanged afterward:

| Canonical capability | Pre/post blob hash |
|---|---|
| `event-routing-and-waits` | `8d24a9b7e7f1b2585f6a223e7ee597e6f4b6133c` |
| `durable-persistence-and-outbox` | `57b665fc9e16cba484e55ea8849c8c1a87fd2013` |
| `repository-foundation` | `53e962a1a1fad9eb954d5ac123143b73dda1c002` |
| `durable-runtime` | `51a87be6adac0bdf4ab2a09772379d03f7f2de8e` |

The independent cross-change resolver found 175 delta requirement entries and zero duplicate
`(capability, requirement heading)` owners.

## 4. Validation and task state

| Check | Result |
|---|---|
| `openspec.cmd validate --all --strict` | 18 passed / 0 failed |
| Cross-change delta requirement entries | 175 |
| Duplicate capability/heading owners | 0 |
| Capability directories lacking `spec.md` | 0 |
| `git diff --check` | exit 0 |
| Harmonize task accounting | 9 complete / 24 pending / 33 total |

Completed by this approval/synchronization step: `1.5`, `2.1`, `2.2`, and `2.3`.

No product `src/`, behavior tests, samples, packages, provider schemas, or runtime behavior were
edited. Prior requests and verdicts remain immutable. No commit was created. Section 7
checkpointing and task `8.0` remain blocked by their own exit gates.

## 5. Post-sync freeze anchors

HEAD: `ac46d99543daf85c0fa3234272997ba40f47f96b`  
HEAD tree: `28f4033c4773ea7761afa905c9836fd25866f1c0`

- porcelain entries: `426`
- raw ordered SHA-256: `0432097368b4c3c1a1ebe54a5a743d24b1ac806c625edbdc7b1b5ec39f7c8d73`
- NUL-expanded normalized records: `456`
- normalized LF SHA-256: `9765189e0236dc878c28cc930cb82ad1f46b082f0047200d10169c20ced19fe8`
- capability directories: `16`
- capability-directory SHA-256: `5e8a9725979d702ff2f639fef587828958c92533139602ab13f1401d6df7ebd5`
- capability directories lacking `spec.md`: `0`
