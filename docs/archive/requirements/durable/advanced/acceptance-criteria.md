# Durable Advanced Acceptance Criteria

## Acceptance set

### DRA-AT-001: Multi-node execution preserves one logical mutator

Given multiple hosts able to process the same durable instance
When concurrent ownership attempts occur
Then only one committed execution path succeeds at a time

### DRA-AT-002: History pressure is observable

Given a long-running durable workflow
When history or checkpoint growth exceeds expected levels
Then operators can detect that pressure through supported inspection

### DRA-AT-003: Continue-as-new preserves logical identity

Given a long-lived durable workflow
When history control rolls it into a new run or checkpoint lineage
Then logical identity and documented continuity semantics are preserved

### DRA-AT-004: Retention and purge are safe

Given completed durable instances under retention policy
When archive or purge operations run
Then data removal follows documented policy and does not remove active instances
