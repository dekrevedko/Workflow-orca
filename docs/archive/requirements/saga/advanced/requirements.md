# Saga Advanced Requirements

This document defines advanced saga capabilities beyond the first supported saga slice.

## Advanced saga capabilities

The advanced saga track should later support:

- durable saga execution as the primary production mode
- message-driven orchestration boundaries
- compensation retry strategies
- operator intervention and manual compensation
- compensation audit trail
- partial compensation policies
- child saga or nested saga composition
- integration consistency patterns such as inbox/outbox

## Requirements

### SG-A-001: Durable coordination boundary

Advanced saga support should define durable coordination boundaries for long-running message-driven execution.

### SG-A-002: Compensation auditability

Every compensation decision and outcome should be inspectable for operators.

### SG-A-003: Manual intervention support

The runtime should allow operator-assisted recovery for compensation failures where policy allows it.

### SG-A-004: Outbox/inbox consistency

Advanced saga support should define how state changes and outbound publications remain consistent.

### SG-A-005: Version-aware saga evolution

Long-running saga instances should be version-aware and protected against incompatible definition changes.
