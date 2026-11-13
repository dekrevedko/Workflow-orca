# Task 7.5/7.6 review remediation — 2026-09-23

The 2026-09-22 combined Task 7.5 OOO-1 and Task 7.6 target was rejected for PPP-1.
Its immutable request, 13-line raw-order manifest, and 2026-09-23 REJECT verdict remain
registered. The rejected raw anchor is 1,195 bytes /
`ed1c3952812568f34ee46c0599157dc96a5169277c79bb316fd73a46537cd47a`;
its 12-row scoped content anchor is 1,961 bytes /
`f947d3e0f8b71e18bb12e28bba4f12a59b7a8e43655d4e93faa5cf6bf942133e`.
This record does not authorize a checkpoint.

## PPP-1: exact ownership

The 122-entry `retired-public-symbols` inventory belongs to
`reshape-developer-facing-interfaces` Task 7.17, which removed the public legacy
surface. Its JSON owner is restored to `task:7.17`. The deletion-ledger guard now
requires that exact value and checks the completed reshape Task 7.17 removal text,
so a merely existing but unrelated reshape task number cannot satisfy the owner.
Harmonization Task 7.6 audits and pins the qualified identities; it does not take
ownership of the original removal.

## QQQ-1: completeness and wording

- The Markdown companion now reports all six exact accounting counts from the
  machine-readable ledger, lists all four ordered symbol inventories including
  removed hosting/codec types, and uses the correct 134-path recovery/target count.
  The existing companion guard compares each ordered Markdown row and count with
  the JSON fields and rejects an invented count such as 999.
- Historical source is lexically masked before namespace, type, and member matching.
  A member must have declaration syntax in the named balanced type body; enum
  values are handled separately. The in-process regression rejects a parameter
  token and a comment while retaining a real field, method, and enum value.
- The `.Management` namespace is described precisely: it existed in removed
  `v3/` lineages but not the promoted product lineage audited by Task 7.6.
  The two codec interfaces belonged to historical
  `OrcaCore.Abstractions.csproj`; `OrcaCore` is the current successor assembly
  and therefore the correct package-negative coordinate.

The original 122-entry forbidden-symbol catalog, its ordered
`302b74d5ac5d52ddb2586002454f1f6771c37896beb643499b854ae85645c3a4`
digest, the Task 7.5 classifier/probe catalogs, and all product source remain
unchanged. This remediation is presented with a new freeze for independent review.
