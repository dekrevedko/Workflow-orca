## ADDED Requirements

### Requirement: Runtime concurrency and identity invariants have adversarial coverage
Executable tests SHALL force checkpoint/tail interleavings, position-only competing writes, stable unreconciled tails, conflicting quarantine appends and rearm without repair. Real PostgreSQL and retained SQL Server certification SHALL cover position-fact round-trip, checkpoint lineage replacement/null clearing, and two independently constructed stores/runtimes sharing one database/schema. At least both yield and fiber-failure fencing and both checkpoint/tail publication interleavings SHALL be exercised. Same-store unit races SHALL NOT be credited as independent-store or separate-process qualification.

Ephemeral tests SHALL cover result observation before, at and after the absolute deadline; early or delayed timer delivery; winner election before notification publication; wrapper failure before observation; root/branch/item execution; and physical ownership until losing work settles. Identity tests SHALL cover delimiter injection, null/absent/literal outcomes, accepted Unicode code units, culture-independent schema ordering, and equivalent/conflicting normalized host profiles including every behavior-affecting option. Tests SHALL use deterministic coordination or controlled clocks rather than timing sleeps.

Evidence SHALL name the tested commit/tree, compiled test scope, actual outcomes and qualification limits. Position-event counts, serialized bytes and commit boundaries SHALL be measured for a long loop at small/default driver budgets. Fresh-store correctness SHALL NOT imply migration, mixed-version rollout, physical storage scaling or unsupported product qualification.

#### Scenario: A provider passes ordinary append tests
- **WHEN** the additive position fact and coherent-load fix are qualified for a provider
- **THEN** certification also executes the named independent-store fencing, lineage and publication races rather than inferring them from ordinary append success

#### Scenario: A late attempt finishes after its deadline
- **WHEN** a controlled test delivers the result, wrapper failure or timer in either order around the deadline
- **THEN** the elected winner remains stable and late work retains physical ownership until settlement without publishing state or a replacement failure
