## ADDED Requirements

### Requirement: Coherent execution loads bind versioned checkpoint positions
Every committed change of the durable execution envelope SHALL advance the workflow stream's optimistic concurrency version. Yield and fiber-failure transitions SHALL include a `WorkflowExecutionPositionChangedEvent` in the same atomic batch as their checkpoint, including when no other workflow fact is emitted. The driver SHALL use the same captured checkpoint for its envelope and aggregate reconstruction and SHALL read its ordered complete tail from that checkpoint version. It SHALL NOT update only the expected stream version while retaining an unreconciled execution position.

The driver SHALL classify the entire tail before resuming. Facts that change position without sufficient reconstruction information SHALL cause a fresh checkpoint load; terminal, cancellation, park and retry state SHALL govern whether execution proceeds. Changing contention SHALL have bounded retries and preserve retryable continuation ownership. Repeated identical checkpoint bytes/version and ordered immutable tail identities at the same stream version SHALL quarantine unreconciled work with an offending-event diagnostic. A conflicted quarantine append SHALL reload rather than report a successful park. Quarantine SHALL NOT rewrite the checkpoint. No supported checkpoint repair exists; explicit rearm of unchanged bad state SHALL quarantine it again. This SHALL be diagnosed as an engine or provider integrity defect.

#### Scenario: Two hosts retain the same position version
- **WHEN** one host commits a position-only yield or fiber failure and another host attempts to commit using the old version
- **THEN** the first commit advances the version and the second is rejected without overwriting the winning checkpoint

#### Scenario: Another host advances between reads
- **WHEN** a competing host changes position between the checkpoint and tail reads, or commits after the tail read
- **THEN** the driver reloads the complete position or loses expected-version append without publishing the old logical effect again

#### Scenario: An unreconciled position remains unchanged
- **WHEN** consecutive reloads return the same unreconciled checkpoint and tail
- **THEN** the driver quarantines with the offending event type, preserves the checkpoint and bad tail, and rearming the unchanged instance causes quarantine again

#### Scenario: A concurrent cancellation wins the quarantine race
- **WHEN** cancellation commits before the expected-version quarantine append
- **THEN** the stale park is rejected and the driver reloads and honors cancellation before attempting to execute the unreconciled position
