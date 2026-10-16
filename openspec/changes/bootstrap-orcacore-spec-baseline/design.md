## Context

The repository already separates shared abstractions, a state-driven runtime, a durable runtime path, and a separate event-driven prototype. It also contains detailed requirements, architecture notes, research, and acceptance-test planning. What is missing is a single OpenSpec workspace that turns that existing knowledge into capability-oriented specs and keeps future work constrained to small, reviewable deltas.

The bootstrap must stay local-only because the user explicitly asked for an OpenSpec folder that is not included in git. The repository is also in a dirty state, so the bootstrap must avoid disturbing tracked files and must not rely on rewriting current documentation.

## Goals / Non-Goals

**Goals:**
- Create a working OpenSpec workspace for Codex in the current repository.
- Keep the workspace excluded from git via local exclude rules.
- Capture a project-wide baseline spec corpus that mirrors the current codebase and documented intent.
- Define a repeatable workflow for future changes that favors small, isolated review chunks.

**Non-Goals:**
- Re-implement OrcaCore from the generated specs in this change.
- Replace or rewrite the existing `docs/` tree.
- Force a single architectural conclusion about convergence between the state-driven runtime and the event-driven prototype.

## Decisions

- Use the current OpenSpec CLI layout for Codex so the workspace matches upstream tooling rather than hand-rolled folders.
  Alternative considered: create an ad hoc `specs/` tree without running OpenSpec. Rejected because it would not give the project a real OpenSpec workspace or Codex integration assets.
- Keep `openspec/` and `.codex/` local-only through `.git/info/exclude` instead of modifying tracked `.gitignore`.
  Alternative considered: add tracked ignore rules. Rejected because the user asked for a separate folder that is not included in git, and local excludes achieve that without affecting collaborators.
- Write capability-oriented baseline specs directly under `openspec/specs/`.
  Alternative considered: keep everything only inside a bootstrap change and archive later. Rejected because the user wanted immediate project-wide specifications available as the new planning baseline.
- Organize the baseline around repository boundaries and runtime semantics rather than around individual source files.
  Alternative considered: file-by-file specs. Rejected because it would be noisy, brittle, and poor for small reviewable future changes.

## Risks / Trade-offs

- [Risk] The baseline can drift from the source if future work changes code without updating OpenSpec artifacts. -> Mitigation: keep a dedicated planning capability and require new changes to reference and update the affected spec.
- [Risk] Some roadmap areas such as saga support are specified but not yet implemented. -> Mitigation: label those specs as target behavior and keep scenarios aligned with the documented maturity level.
- [Risk] The local-only workspace will not automatically help other contributors. -> Mitigation: the structure remains compatible with later promotion into tracked files if the team decides to adopt OpenSpec repository-wide.

## Migration Plan

1. Initialize the OpenSpec workspace and exclude it from git locally.
2. Capture the current repository baseline in `openspec/specs/`.
3. Use the bootstrap change as the onboarding reference for future small-scope proposals.
4. For the first rebuild or refactor effort, create a new change per bounded capability slice rather than extending this bootstrap change.

## Open Questions

- Whether the local-only workspace should later become a tracked repository convention.
- Whether the long-term product direction should converge on the state-driven runtime, the event-driven runtime, or a hybrid contract surface.
- Which capability should be the first implementation slice if the project is actively rebuilt from scratch.
