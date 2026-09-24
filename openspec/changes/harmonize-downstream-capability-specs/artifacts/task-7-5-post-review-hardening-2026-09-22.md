# Task 7.5 post-review hardening — 2026-09-22

## Scope

This record closes OOO-1 from the approved Task 7.4/7.5 remediation review. It does not reopen the
approved documentation corrections or authorize Task 7.6.

## Natural-language regression coverage

The active-tree classifier now rejects all eight reviewed forms:

1. `Fanout to every instance of a definition is not supported in v1.`
2. `Definition fanout is not supported.`
3. `Section 7B is not yet approved.`
4. `Section 7B has not been approved yet.`
5. `Events that arrive before a wait exists are dropped and must be redelivered.`
6. `The durable engine does not buffer events before a wait exists.`
7. `Publishing events from a workflow is not available in v1.`
8. `The engine supports instance and correlation delivery only.`

Each sentence is exercised directly against the same classifier used for the evolving active-tree
scan. The historical 56-row replay remains exact.

## Classifier ownership

The ordered classifier-name/expression record hashes to
`fa5cb2d5828f453a54311be5c8b77934bc7ca5fbf68c06de6571e334fea55c03`. The digest is a
guard-source constant. Removing a tuple together with its historical rows and the mutable artifact
digest is therefore red until an explicitly reviewed guard-source catalog decision changes it.

The ordered natural-language phrase/expected-classifier record hashes to
`df9180ed2858bbe28e3d3e71326efd7ecfb86fb6b9157558922fe1b0c2218549`. That digest is also a
guard-source constant, so removing or substituting one of the eight executable probes is red.
