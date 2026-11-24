# Wait-ownership pilot provenance inventory

This checkpoint adds only the pilot's six development-verification requirements
and one active capability directory. The prior 219 provenance rows and all
canonical content, preambles, historical removals, and successor dispositions
are unchanged. The previous shutdown provenance artifact remains hash-pinned
as a permanent predecessor. No canonical specification is synchronized here.

Provenance eligibility below concerns inventory consistency only. The TLA+
pilot has **failed runtime qualification** (WOP-F001); this record grants no
correctness approval, archival approval, or permission to change the runtime.

- record rows: 225
- record bytes: 59,782
- record SHA-256: `4368f1c82992ed19bcbcf1baae61ad51ca66f764fa4d390cb4de6b501a44cb93`
- synchronized operations: 198
- pending canonical operations: 0
- declared new-capability requirements outside the canonical set: 9
- semantic approval eligible: yes

| State | Count |
|---|---:|
| `Synchronized` | 198 |
| `SupersededByApprovedSuccessor` | 18 |
| `PendingAddition` | 0 |
| `PendingAddedCanonicalConflict` | 0 |
| `PendingModification` | 0 |
| `PendingRemoval` | 0 |
| `NewCapabilityOutsideCanonical` | 9 |

- canonical capability directories: 14
- record bytes: 547
- SHA-256: `7165dac4e1a57022a7890b421f522bf4152f6d5ddc159a41539ce2ef0d18ec9f`
- capability directories: 38
- record bytes: 3,134
- SHA-256: `cdd01b00b72a22734c4ef0d159834437a88526c35fbeb0ec2810ca064264deac`

Independent review and executable guard results are recorded in the pilot
verification report. This inventory change does not rewrite historical approvals.
