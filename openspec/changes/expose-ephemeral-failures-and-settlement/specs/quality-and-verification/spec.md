
## ADDED Requirements

### Requirement: Ephemeral original failures and settlement are behavior verified
The owning tests SHALL exercise the exact public original-exception identity and
passive settlement contracts with behavior-first controls. They SHALL cover both
retention-off and bounded-count modes, barriers for failure/terminal/physical
settlement separation, single and multiple observers, local cancellation and
Closed/Stopping/Completed/owner-disposal races. No polling or sleep-based timing
assertion SHALL substitute for explicit synchronization. No existing ownership,
retirement, shutdown, retry or branch/item assertions SHALL be weakened.

Tests SHALL cover thrown/returned step errors, non-OrcaCore and OrcaCore codes,
user-thrown invocation wrappers, final retries with equal type/message values,
late timeout exceptions, true aggregates with no live inner, recovered outcomes,
retirement weak-reference release with caller references dropped and durable
unsupported/unchanged behavior. Codec controls SHALL prove exact legacy bytes,
new version-2 bytes and malformed/duplicate/version/type/provenance negatives in
legacy JIT and generated-metadata modes, without serializer hooks or blanket
suppressions. Runtime baselines SHALL be updated with source only.

A fresh public-package consumer SHALL cover the resource-release recipe, resultless
and inherited resultful settlement methods, durable NotSupportedException and
detached type inspection. The unchanged issue #13 journeys and every required
native group/subcase SHALL remain, with added failure/settlement witnesses across
six native Windows Server 2025/Ubuntu 24.04 cells. Required hosted validation pushes
remain separately authorized. A successful native claim SHALL verify actual six
completed jobs, the aggregate, exact source/feed identities and each diagnostic.

#### Scenario: A release consumer owns a file-like resource
- **WHEN** output waiting reports timeout while an explicitly blocked step still uses an application-owned resource
- **THEN** the consumer retains it until settlement success, then releases it exactly once, and cannot treat output completion or an unaccepted start as proof

#### Scenario: Native qualification omits the new path
- **WHEN** a source target omits a failure/type/settlement witness, skips a native job, changes diagnostics without classification or uses a different package feed
- **THEN** qualification fails and neither a semantic approval nor a new native-support claim is inferred
