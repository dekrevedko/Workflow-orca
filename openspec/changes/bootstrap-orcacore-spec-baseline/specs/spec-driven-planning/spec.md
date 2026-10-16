## ADDED Requirements

### Requirement: Local OpenSpec workspace remains isolated from git
The project SHALL keep bootstrap OpenSpec artifacts in local-only `openspec/` and `.codex/` workspaces excluded from git by local repository ignore rules unless the team explicitly adopts a tracked workflow later.

#### Scenario: Workspace is initialized locally
- **WHEN** OpenSpec is bootstrapped in the repository for local planning
- **THEN** the generated `openspec/` and `.codex/` folders remain outside normal git status and staging operations

### Requirement: Capability baseline exists before rebuild work starts
The project SHALL maintain a base spec set covering the major repository boundaries and runtime capabilities before using OpenSpec to drive a rebuild from scratch.

#### Scenario: Team prepares a rebuild plan
- **WHEN** a contributor starts planning implementation work from the OpenSpec workspace
- **THEN** they can discover baseline specs for repository foundation, runtime semantics, durability, management, prototype behavior, and saga direction

### Requirement: Future changes are chunked for review
The project SHALL prefer small OpenSpec changes that each cover one bounded capability slice, operational risk, or architecture increment rather than large multi-feature batches.

#### Scenario: Contributor prepares follow-up work
- **WHEN** a new implementation or refactor is proposed after the bootstrap
- **THEN** the proposal, design, and task set are scoped so the resulting review remains bounded and traceable
