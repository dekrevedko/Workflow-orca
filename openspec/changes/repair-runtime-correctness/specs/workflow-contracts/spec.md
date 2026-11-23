## ADDED Requirements

### Requirement: Structural fingerprint fields have unambiguous encoding
Compiler format 2 SHALL encode each inspectable structural field independently. A null field SHALL use `null:`. A non-null field SHALL use its invariant-decimal UTF-16 code-unit count followed by `:` and exactly four uppercase hexadecimal digits per code unit. Scalar formatting SHALL be invariant and schema identities SHALL be ordered ordinally; authored node/branch/resource ordering SHALL be preserved. Caller-supplied text SHALL NOT inject structural punctuation, alias null or another value, or lose distinct malformed Unicode code units through UTF-8 replacement fallback. Absent, literal named and opaque-selector outcomes SHALL have distinct tags.

The existing canonical UTF-8/SHA-256 operation SHALL hash the resulting structural representation. Compiler format remains a separate binding value and SHALL NOT be added to fingerprint inputs; definition identity, authored version and other previously excluded binding/acceptance values remain excluded. Opaque code remains opaque and changed behavior still requires an authored version change. This unreleased format 2 completes the greenfield cutover without a legacy alias.

#### Scenario: Names contain fingerprint punctuation or malformed Unicode
- **WHEN** accepted event names, branch ids, pool keys, policy/type/schema/partitioner fields or outcome names differ by punctuation, null presence or UTF-16 code units
- **THEN** the canonical field encoding preserves those differences in both workflow modes instead of creating a structural alias

#### Scenario: The same definition is compiled under different cultures
- **WHEN** hosts compile the same inspectable definition with different current cultures
- **THEN** its canonical representation and fingerprint are equal
