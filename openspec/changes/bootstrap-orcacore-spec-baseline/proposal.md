## Why

OrcaCore already has substantial code and research, but it does not yet have an OpenSpec baseline that can drive future work in small reviewable increments. A local-only bootstrap is needed so the project can be rebuilt and evolved from explicit specifications instead of relying on chat history or scattered docs.

## What Changes

- Initialize a local-only OpenSpec workspace for Codex in this repository.
- Capture a baseline spec set for the major OrcaCore capabilities and repository boundaries.
- Define a chunk-first planning model so future work is proposed, designed, and implemented in small bounded changes.

## Capabilities

### New Capabilities
- `spec-driven-planning`: Establish local OpenSpec workspace rules, capability baselines, and chunk-first change planning for OrcaCore.

### Modified Capabilities

## Impact

- Local-only `openspec/` workspace and `.codex/` helper assets.
- New baseline specifications for repository structure, workflow semantics, durability, routing, management, prototype runtime, and saga direction.
- No tracked source-code or public API changes in the repository itself.
