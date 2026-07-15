## MODIFIED Requirements

### Requirement: Optional resource governance composes with instance serialization
When host-level concurrency, per-step execution throttles, or named cross-instance transient pools are enabled (see `runtime-resource-governance`), those limits SHALL govern host-local cross-instance operational capacity and shared external resources without replacing per-instance serialized mutation or claiming durable-lease semantics. A local fiber that cannot acquire required transient capacity SHALL become blocked through a mode-appropriate owned obligation and SHALL release the instance turn so another runnable sibling may advance.

#### Scenario: Pool limit and instance lock coexist
- **WHEN** two instances both hold slots from the same named pool
- **THEN** each instance still advances under serialized-per-instance semantics so state transitions for one instance do not interleave with another mutator for that same instance

#### Scenario: Local sibling can advance while resource is saturated
- **WHEN** one local fiber is blocked on a named pool and another fiber in the same instance is runnable
- **THEN** the blocked fiber does not retain the instance turn and the scheduler may advance the runnable sibling
