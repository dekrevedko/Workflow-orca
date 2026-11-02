## MODIFIED Requirements

### Requirement: Join policies define one scope outcome
`WhenAll` SHALL wait for every branch or item to become terminal, SHALL merge once only when all succeed, and otherwise SHALL fail after all finish without automatically cancelling siblings. One failure SHALL be preserved directly; multiple failures SHALL become `SFE-JOIN-FAILED` with causes ordered by authored branch or item index. `WhenAllOutcomes` SHALL wait for every child, merge the complete ordered success/failure outcomes once, and complete the scope successfully. An ancestor cancellation, termination, or workflow deadline SHALL suppress either merge instead of creating a merge-visible cancellation outcome.

An ephemeral business failure's original exception SHALL be associated with its
actual failure/attempt identity before terminal publication, not guessed later
from code, message, path or occurrence values that can repeat. Retry selection,
late timeout completions and terminal-winner election SHALL not replace an already
committed failure or attach an unrelated exception. A single propagated step
failure SHALL preserve that association. A true aggregate SHALL expose only
ordered detached causes and no invented live AggregateException. WhenAllOutcomes
SHALL continue to supply detached data, and a successful recovery SHALL not retain
obsolete original exception references as a terminal failure. Ephemeral fiber records MAY carry detached type text but SHALL never carry live
Exception objects. Durable engine-produced fiber, checkpoint, outbox, projection
and provider failure records SHALL NOT capture ExceptionType; their persisted
failure bytes SHALL remain byte-identical to base 0a8e375. This does not prohibit
supported typed application payloads from using the shared failure-value codec;
it does not add live exceptions or type capture to durable failure production.
Existing sibling completion, join ordering and no-automatic-sibling-cancellation
semantics SHALL remain unchanged.

#### Scenario: A WhenAll branch fails
- **WHEN** any `WhenAll` branch commits failure before the scope joins
- **THEN** remaining branch work is allowed to reach terminal, merge does not execute, and the scope fails with the ordered failure result

#### Scenario: A WhenAllOutcomes branch fails
- **WHEN** fixed branches reach a mixture of success and failure in any completion order
- **THEN** the merge executes once with authored-order success/failure outcomes and the parent resumes from its replacement state

#### Scenario: Ancestor terminal transition wins
- **WHEN** workflow cancellation, termination, or workflow deadline commits while the scope is active
- **THEN** child work is signalled or fenced, merge is suppressed, and no branch or item cancellation outcome is fabricated

#### Scenario: Identical failures occur at a repeated authored site
- **WHEN** distinct attempts or loop occurrences produce exceptions with equal type/message/path values
- **THEN** the terminal winner's actual object is selected by failure identity and no previous object is substituted

#### Scenario: A failed branch is recovered as data
- **WHEN** WhenAllOutcomes consumes a failed branch/item and the workflow later completes successfully
- **THEN** no output-unavailable wrapper is manufactured and obsolete live exception associations are released without changing detached cause values
