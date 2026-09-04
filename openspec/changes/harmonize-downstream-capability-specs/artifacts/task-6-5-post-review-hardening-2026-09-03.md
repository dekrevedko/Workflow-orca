# Task 6.5 post-review hardening

Date: 2026-09-03

## Finding

The Task 6.5 review approved the decision and its executable public-surface evidence, then identified
Y-1: the companion SHA-256 was owned only by mutable `v1-public-contract.json`. A coherent edit to
the companion plus that fixture could therefore preserve the Task 6.5 consistency assertion after
the active freeze became historical evidence.

## Resolution

`OpenSpecCorpusGuards` now owns the reviewed companion SHA-256
`41f6472c2774363d2ab922c608922e787ec241333e1d1c0b76b0c6d529ab8ec3` as a named source constant.
The guard first requires the public-contract fixture to reproduce that constant and then requires the
companion bytes to match it. The fixture remains useful shared metadata but cannot authorize a new
companion baseline by changing itself.

## Negative control

Appending a differently named public declaration to `docs/specs/17-public-authoring-contract.cs`
and coherently updating `v1-public-contract.json` remains red because neither edit can change the
guard-source digest. The existing exhaustive twelve-assembly API baseline continues to protect the
real compiled surface independently.

## Scope

No product source, canonical OpenSpec requirement, public API baseline, package manifest, or behavior
test changes. This is review hardening for the already-approved Task 6.5 decision.