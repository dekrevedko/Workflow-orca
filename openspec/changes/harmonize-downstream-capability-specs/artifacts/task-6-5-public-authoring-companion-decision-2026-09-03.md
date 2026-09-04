# Task 6.5 public-authoring companion decision

Date: 2026-09-03

## Decision

`docs/specs/17-public-authoring-contract.cs` remains byte-unchanged. The authoring-session lifecycle
does not add an application-authored type or callable signature: its state, session, lifecycle and
join handles, lexical token, and shared implementation base remain internal to `OrcaCore.Core`.
The companion therefore continues to model only the public application contract.

## Executable evidence

- The companion remains 53,745 bytes with SHA-256
  `41f6472c2774363d2ab922c608922e787ec241333e1d1c0b76b0c6d529ab8ec3`, already owned by
  `v1-public-contract.json` and `CompanionBaseline_HasExactReviewedNamespaceArityAndSignatures`.
- The approved `OrcaCore.Core` API baseline contains only its format and assembly headers because
  the assembly intentionally exports no public declarations.
- `EveryTargetAssembly_MatchesTheApprovedExactPublicApiBaseline` captures all twelve current
  assemblies and rejects any newly public lifecycle type.
- `Task65_PublicAuthoringCompanionRemainsUnchangedAndLifecycleInternalsStayNonPublic` binds the
  unchanged companion, internal declarations, empty Core surface, exhaustive baseline, and this
  completed task decision together.

## Task 6.4 finding X-1

The same slice pins the immutable source appendix at 8,415 bytes and SHA-256
`131d22bea736b6c7c4ac8a310ef1db72c992dcc867776b664c01fe2988d57be6`. A coherent edit to both
the source artifact and its published canonical projection now fails before content comparison, so
the complete appendix remains protected after the prior active freeze becomes historical evidence.

## Scope

No product source, canonical OpenSpec requirement, public-authoring companion, API baseline, package
manifest, or behavior test changes. The implementation is a decision record plus must-green
infrastructure evidence.
