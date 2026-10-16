# Runtime resource governance (concurrency limits)

This note introduces **optional** limits on concurrent workflow advancement, concurrent step execution, and **named shared pools** (for example “at most four concurrent database-backed operations process-wide”). It complements OrcaCore’s existing guarantee of **one logical mutator per workflow instance**.

## Where the authoritative requirements live

| Artifact | Path |
|----------|------|
| Proposal (why / what / impact) | `openspec/changes/add-runtime-concurrency-limits/proposal.md` |
| Design (decisions, risks, composition with locks) | `openspec/changes/add-runtime-concurrency-limits/design.md` |
| Implementation tasks (checklist) | `openspec/changes/add-runtime-concurrency-limits/tasks.md` |
| Baseline capability spec | `openspec/specs/runtime-resource-governance/spec.md` |
| Composition with state-driven runtime | `openspec/specs/state-driven-runtime/spec.md` |
| Composition with durable runtime | `openspec/specs/durable-runtime/spec.md` |
| Optional authoring hints | `openspec/specs/workflow-contracts/spec.md` |

## Summary for reviewers

- **Per-instance serialization** addresses correctness; **resource governance** addresses capacity (cross-instance and shared services).
- **Named pools** let unrelated workflows share one budget (for example `db-updates` with limit 4).
- **Distributed enforcement** (multiple processes or nodes sharing one limit) is explicitly out of scope for the initial specification unless extended later.

After review, implementation should follow `tasks.md` and update tests listed there.
