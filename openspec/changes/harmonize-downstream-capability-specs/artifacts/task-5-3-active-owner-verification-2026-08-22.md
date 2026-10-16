# Task 5.3 active-delta ownership verification

Date: 2026-08-22  
Base: `5e8e25b94010965130b3b98ede2392346da2a9cd`

## Scope

Task 5.3 verifies that `reshape-developer-facing-interfaces` remains the only active delta owner for
the repository friend topology and the serialized durable resource-governance aggregate. The check
enumerates every non-archived change and every requirement in its declared capability directories;
it does not infer ownership from proposal prose, canonical synchronization state, or distinct block
content.

## Active corpus

- Active changes: 4
- Active delta requirement headings: 176
- Target owner: `reshape-developer-facing-interfaces`

| Capability | Requirement | Operation | Block bytes | Block SHA-256 | Normative-body SHA-256 |
|---|---|---:|---:|---|---|
| `repository-foundation` | `Dependency direction remains one-way` | `MODIFIED` | 3,524 | `d0d512ea59ea8da595770b5437d7faa7565773c6cd8845850d2dea6010b54b29` | `4d88725607bc7c9d65eaa73c01c99fb6a9e33fac3c487fc89d850372d2b561ad` |
| `durable-runtime` | `Durable resource governance is one serialized provider aggregate` | `ADDED` | 6,134 | `6def32f064858bd75dff10d17e72763b77908294dc2762fabb124ca9509519e5` | `8fcca7ca6f5c35a3e443b978bc63c2c1eb22eda77c277d6648faae7eae554a62` |

The executable gate requires exactly one active owner for each capability/heading pair, requires that
owner to be reshape, and separately compares the exact body bytes after the heading across the full
active corpus. The body comparison prevents a competing change from disguising an identical
requirement under a different heading or capability.

## Mutation evidence

A temporary `Temporary copied topology mutation` requirement was added to
`add-runtime-concurrency-limits/specs/runtime-resource-governance/spec.md` with the friend-topology
body copied byte-for-byte after its renamed heading. The focused Task 5.3 gate failed and named:

`add-runtime-concurrency-limits :: runtime-resource-governance :: Temporary copied topology mutation`

The mutation was removed byte-identically and the focused gate returned green.

## Validation

The final review packet records the refreshed opportunistic provenance pins, focused ownership gate,
Debug and Release warnings-as-errors builds, Infrastructure and intentional expected-red lanes,
OpenSpec strict validation, task accounting, declaration-crosswalk accounting, and
`git diff --check` against the frozen target.

Schema 7 registers this self-inclusive dirty review as `activeFreeze`. That descriptor binds the raw
manifest to the current commit-real target without attempting an impossible hash of its own fixture.
It remains green immediately after an approved checkpoint only when the commit parent and exact path
set match; the following mechanical transition replaces it with committed-blob historical evidence.
