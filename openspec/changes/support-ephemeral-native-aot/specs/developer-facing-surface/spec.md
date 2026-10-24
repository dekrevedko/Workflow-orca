## ADDED Requirements

### Requirement: Workflow JSON metadata registration is exact and process scoped
OrcaCore SHALL add only public static class `OrcaCore.WorkflowJsonMetadata` with public static void `Register<TContext>() where TContext : System.Text.Json.Serialization.JsonSerializerContext, new()`. Registration SHALL own one generated metadata context for the process and SHALL expose no reset/unregister, options/resolver/converter parameter, new role option, package or friend. First successful registration SHALL publish atomically; repeating the same CLR context type SHALL be a no-op without reconstruction, including after sealing. A different context type SHALL throw InvalidOperationException without construction or mutation. Construction/validation failure SHALL install nothing and permit retry. Unsupported context policy/provenance, including UseStringEnumConverter=true with otherwise-default runtime options and the handwritten renamed-property context, SHALL raise NotSupportedException identifying the context and rule before publication; an empty converter list or the generic constraints SHALL NOT alone establish conformance. Context construction failures SHALL retain their original cause. The first workflow authoring/type-contract/codec operation SHALL irreversibly seal Generated mode or legacy Reflection mode, including a failing lookup. First registration after a Reflection seal SHALL fail. Configuration/first-use races SHALL linearize, and reentrant configuration SHALL fail explicitly without deadlock. Errors SHALL identify context identities and seal state. Product/public signatures and role fingerprints outside this catalogue SHALL remain unchanged.

#### Scenario: Two service providers use metadata in one process
- **WHEN** startup registers the same context for multiple providers before or after first use
- **THEN** one context remains installed, repeated registration does not invoke application code, providers own no metadata disposal and stopping a host cannot reset configuration

#### Scenario: Configuration races first workflow use
- **WHEN** first registration and first workflow use overlap
- **THEN** the whole context wins before sealing or registration fails after the legacy seal, with no partial context, reflection fallback, deadlock or order-dependent resolver chain

#### Scenario: Configuration fails and is retried
- **WHEN** a context constructor or options validation fails before a successful installation
- **THEN** no registration is visible, the failure preserves its cause and a subsequent valid registration can succeed before first use

### Requirement: Native authoring bootstrap has a static ownership recipe
The supported native recipe SHALL call AddOrcaCoreEphemeralEngine on IServiceCollection, then WorkflowJsonMetadata.Register, then author definitions, build the provider and use existing typed registry/facade APIs. Role registration SHALL synchronously install the Core typed-authoring boundary through a static reference without resolving services, constructing steps, building a provider, loading an assembly dynamically or admitting work. Generated metadata SHALL capture no provider/scope and SHALL introduce no provider-owned disposal. The existing JIT definition-first path and missing-engine diagnostic SHALL remain compatible. A native authoring attempt without the required installation SHALL fail actionably before effects. Metadata SHALL NOT change admission, Closed draining, Stopping/Completed, cancellation, retirement, pins, key expiry or DI dependency ownership.

#### Scenario: Native application configures the engine before building definitions
- **WHEN** it follows the documented role-registration, metadata-registration and authoring sequence
- **THEN** initialization is statically reachable before definition construction and no assembly-load fallback or premature DI resolution occurs

#### Scenario: Metadata is used with a draining or completed host
- **WHEN** existing instances drain while Closed or the host reports Completed
- **THEN** the existing shutdown contract remains authoritative and metadata cannot create new work, touch scoped dependencies or run user workflow code after Completed
