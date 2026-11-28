# SQL Server support contract provenance

The ten proposed replacements await independent approval and synchronization. The canonical specifications still contain their predecessors.

- record rows: 253
- record bytes: 67,833
- record SHA-256: `197c9ea04cd338f64fc46a3be31a4112a6f72eb9a76c5de838b2d8884200decd`
- synchronized operations: 206
- pending canonical operations: 10
- declared new-capability requirements outside the canonical set: 3
- semantic approval eligible: no

| State | Count |
| --- | ---: |
| `Synchronized` | 206 |
| `SupersededByApprovedSuccessor` | 34 |
| `SupersededByOwnerAuthorizedCandidate` | 0 |
| `PendingAddition` | 0 |
| `PendingAddedCanonicalConflict` | 0 |
| `PendingModification` | 10 |
| `PendingRemoval` | 0 |
| `NewCapabilityOutsideCanonical` | 3 |

- canonical capability directories: 15
- record bytes: 597
- SHA-256: `bfbd9338f06536e6dfb44b07087bea92e44fbfe0aab5e046d189837fcc50f070`
- capability directories: 60
- record bytes: 4,807
- SHA-256: `94ac5b9d1c64cf73a5c97f1fd05f9938cebcb03e7fb1028225fb50bffdcfaaba`

| Capability | Open operations | Owner |
| --- | ---: | --- |
| `developer-facing-surface` | 2 | scope-sqlserver-provider-support task 1.2 |
| `durable-persistence-and-outbox` | 3 | scope-sqlserver-provider-support task 1.2 |
| `quality-and-verification` | 3 | scope-sqlserver-provider-support task 1.2 |
| `repository-foundation` | 2 | scope-sqlserver-provider-support task 1.2 |
