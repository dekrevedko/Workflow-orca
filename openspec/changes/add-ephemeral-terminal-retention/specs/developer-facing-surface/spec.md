## ADDED Requirements

### Requirement: Ephemeral terminal retention policy is explicit and host-owned
`OrcaCore.Engine.Ephemeral` SHALL own the immutable `OrcaCore.Hosting.EphemeralTerminalRetentionPolicy` with a private constructor, get-only `int MaxRetainedInstances`, `TimeSpan MaxAge`, and `TimeSpan SweepInterval`, and exactly one public factory `Create(int maxRetainedInstances, TimeSpan maxAge, TimeSpan sweepInterval)`. `EphemeralEngineHostOptions` SHALL expose the nullable init-only member `EphemeralTerminalRetentionPolicy? TerminalRetention`, defaulting to null. With a null/omitted policy, accepted instances and their start bindings SHALL remain retained for the host lifetime and no retention timer SHALL exist; unaccepted aborted provisional units SHALL still follow mandatory settlement cleanup. The factory SHALL reject a count below one, nonpositive maximum age, or nonpositive sweep interval, an interval greater than maximum age, or an interval greater than one day, using `ArgumentOutOfRangeException` for the corresponding parameter in signature validation order. Registration SHALL immediately validate and freeze an owned copy and include the enabled/disabled state and all three values in duplicate-role option agreement; equal values SHALL be idempotent regardless of policy object identity and different values SHALL conflict before service mutation. Every positive interval up to those ceilings, including one tick, SHALL be valid; documentation SHALL identify very small intervals as potentially continuous sweep/CPU overhead and recommend choosing an operationally appropriate interval without silently clamping the value. The policy SHALL create no new registration overload, public lifecycle command, options-binder DTO, serializer hook, package dependency, friend grant, or durable-host option.

#### Scenario: Existing application omits the policy
- **WHEN** an ephemeral host is configured using only its existing structured-execution and transient-pool values
- **THEN** the selected retention behavior and host-lifetime start bindings remain unchanged and no retention sweep is scheduled

#### Scenario: Host chooses bounded retention
- **WHEN** application code sets `TerminalRetention = EphemeralTerminalRetentionPolicy.Create(128, TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(1))`
- **THEN** the host freezes exactly those values and applies one host-wide eligible-terminal retention budget across registered definitions

#### Scenario: Invalid policy is constructed
- **WHEN** a count is zero/negative, maximum age is nonpositive, or the interval is nonpositive, greater than maximum age, or greater than one day
- **THEN** the factory throws `ArgumentOutOfRangeException` naming the first invalid parameter in signature order before registration or service mutation

#### Scenario: Equivalent or conflicting registrations are composed
- **WHEN** the same role is registered again with equal policy values in another object or with any changed policy value/enabled state
- **THEN** equal values preserve existing idempotent registration and changed values reject before adding services or starting work

#### Scenario: Ordinary consumer package closure is inspected
- **WHEN** compile/reflection/package guards inspect the enabled policy
- **THEN** only the exact proposed ephemeral host signatures are added and no public engine, bulk purge/archive/evict method, durable option, or reflection/friend bridge appears
