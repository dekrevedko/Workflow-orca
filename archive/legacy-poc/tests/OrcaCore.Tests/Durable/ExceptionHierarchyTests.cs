using OrcaCore.Runtime.Durable.Persistence;

namespace OrcaCore.Tests.Durable;

public sealed class ExceptionHierarchyTests
{
    [Fact]
    public void Durable_exceptions_derive_from_stable_base_types()
    {
        Assert.IsAssignableFrom<WorkflowEngineException>(new DefinitionAlreadyRegisteredException("def", "v2", "v1", "already registered"));
        Assert.IsAssignableFrom<WorkflowDefinitionException>(new DefinitionVersionMismatchException("def", "v2", "v1", "inst-1"));
        Assert.IsAssignableFrom<WorkflowDefinitionException>(new DurableDefinitionRehydrationException("rehydration failed", new InvalidOperationException("inner")));
        Assert.IsAssignableFrom<WorkflowDefinitionException>(new DurablePayloadSerializationException("serialization failed"));
        Assert.IsAssignableFrom<WorkflowDefinitionException>(new DurablePayloadDeserializationException("deserialization failed"));
        Assert.IsAssignableFrom<WorkflowStoreException>(new ConcurrencyException("concurrency failed"));
        Assert.IsAssignableFrom<WorkflowRoutingException>(new NoActiveWaitException("Evt", "corr-1"));
        Assert.IsAssignableFrom<WorkflowRoutingException>(new AmbiguousCorrelationException("Evt", "corr-1", 2));
    }
}
