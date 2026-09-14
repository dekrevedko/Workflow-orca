# Task 6.5 second post-review hardening

Date: 2026-09-04

## Finding

The independent review approved Task 6.5 post-review hardening and then identified Z-1: the new
ledger completion sentence and the design decision explaining guard-source ownership were not
required by durable executable evidence. Reverting those documentary claims could therefore become
invisible after the active freeze was archived.

## Resolution

`OpenSpecCorpusGuards.Task65_PublicAuthoringCompanionRemainsUnchangedAndLifecycleInternalsStayNonPublic`
now requires the complete Task 6.5 post-review-hardening decision and its new second-review marker.
The same guard reads the harmonization design and requires the exact guard-source ownership decision
that prevents a mutable fixture from authorizing coherent companion drift. Named constants own all
three semantic records; the assertions do not scatter new magic strings.

## Negative controls

Removing the second-review ledger marker makes the focused Task 6.5 guard red. Removing the design's
guard-source ownership paragraph independently makes the same guard red. Both mutations were applied
to the working copy, observed red, and restored byte-exactly.

## Scope

No product source, canonical OpenSpec requirement, public companion, public API baseline, or behavior
test changes. This is durable documentary evidence for the already-approved Task 6.5 decision.