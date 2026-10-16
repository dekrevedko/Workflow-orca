## 1. Workspace Bootstrap

- [ ] 1.1 Keep `openspec/` and `.codex/` local-only unless the team explicitly decides to promote them into tracked repository assets.
- [ ] 1.2 Treat the OpenSpec baseline as a planning layer derived from the current source and docs, not as a replacement for immediate code verification.

## 2. Baseline Maintenance

- [ ] 2.1 Update the affected base spec whenever a future change alters repository boundaries, runtime semantics, or operational guarantees.
- [ ] 2.2 Use one bounded OpenSpec change per feature slice, defect cluster, or architecture increment instead of batching unrelated work.

## 3. Recommended Rebuild Order

- [ ] 3.1 Rebuild `repository-foundation`, `workflow-contracts`, and `workflow-authoring` before deeper runtime work.
- [ ] 3.2 Rebuild `state-driven-runtime` and `event-routing-and-waits` as the first executable orchestration slice.
- [ ] 3.3 Rebuild `durable-runtime`, `durable-persistence-and-outbox`, and `management-and-querying` as separate follow-up slices.
- [ ] 3.4 Advance `event-driven-prototype` and `saga-orchestration` only through explicit, tightly scoped proposals.
