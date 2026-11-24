## ADDED Requirements

### Requirement: Capacity increases grant the eligible partition FIFO prefix

A management resize that increases current capacity SHALL apply the capacity and reserve tickets for every eligible request in the grantable FIFO prefix in the same governance commit. The first live request whose complete requirements cannot fit SHALL block younger requests, including requests for a different pool. A promoted scoped owner SHALL still satisfy its workflow checkpoint admission fence at commit. On conflict the operation SHALL reread current state and recompute its grants. Replaying a committed operation SHALL create no additional tickets or generations.

#### Scenario: Capacity increases while all pools are idle

- **GIVEN** A waits for two units of capacity-one pool X and B subsequently waits for one unit of idle pool Y in the same partition
- **WHEN** management raises X to two
- **THEN** both receive their tickets in the resize commit and no holder release is required

#### Scenario: Owner closes or a release wins during resize

- **WHEN** a resize races owner closure or a competing holder release
- **THEN** committed grants respect the winning governance version and scope checkpoints, preserve live FIFO order, and allocate each ticket exactly once
