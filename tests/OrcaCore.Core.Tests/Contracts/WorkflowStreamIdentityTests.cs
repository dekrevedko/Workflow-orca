using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using Xunit;

namespace OrcaCore.Core.Tests.Contracts;

public sealed class WorkflowStreamIdentityTests
{
    private static readonly Guid Identity = Guid.Parse("ABCDEF01-2345-6789-ABCD-EF0123456789");

    [Fact]
    public void WorkflowIdentity_PreservesConstructorInstanceAndRendering()
    {
        var instance = InstanceId.Parse(Identity.ToString());
        var stream = new WorkflowStreamId(instance);

        stream.AggregateKind.Should().Be(DurableAggregateKind.Workflow);
        stream.InstanceId.Should().BeSameAs(instance);
        stream.DagRunId.Should().BeNull();
        stream.ToString().Should().Be("workflow-abcdef01-2345-6789-abcd-ef0123456789");
        stream.Should().Be(new WorkflowStreamId(InstanceId.Parse(Identity.ToString())));
    }

    [Fact]
    public void DagIdentity_IsTypedCanonicalAndCannotBeReadAsAWorkflow()
    {
        var stream = WorkflowStreamId.ForDagRun(Identity);

        stream.AggregateKind.Should().Be(DurableAggregateKind.DagRun);
        stream.DagRunId.Should().Be(Identity);
        stream.ToString().Should().Be("dag-run-abcdef01-2345-6789-abcd-ef0123456789");
        Action workflowAccess = () => _ = stream.InstanceId;
        workflowAccess.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void EqualityAndHashing_KeepAggregateFamiliesAndIdentitiesDistinct()
    {
        var workflow = new WorkflowStreamId(InstanceId.Parse(Identity.ToString()));
        var dag = WorkflowStreamId.ForDagRun(Identity);
        var sameDag = WorkflowStreamId.ForDagRun(Identity);
        var otherDag = WorkflowStreamId.ForDagRun(Guid.Parse("abcdef02-2345-6789-abcd-ef0123456789"));

        (dag == sameDag).Should().BeTrue();
        dag.GetHashCode().Should().Be(sameDag.GetHashCode());
        (dag == workflow).Should().BeFalse();
        (dag != otherDag).Should().BeTrue();
        new HashSet<WorkflowStreamId> { workflow, dag, sameDag, otherDag }.Should().HaveCount(3);
        var streams = new Dictionary<WorkflowStreamId, string> { [workflow] = "workflow", [dag] = "dag" };
        streams[workflow].Should().Be("workflow");
        streams[sameDag].Should().Be("dag");
    }

    [Fact]
    public void EmptyAndDefaultIdentities_FailClosed()
    {
        Action emptyDag = () => WorkflowStreamId.ForDagRun(Guid.Empty);
        Action nullWorkflow = () => new WorkflowStreamId(null!);
        Action defaultWorkflow = () => _ = default(WorkflowStreamId).InstanceId;
        Action defaultRendering = () => default(WorkflowStreamId).ToString();

        emptyDag.Should().Throw<ArgumentException>().WithParameterName("runId");
        nullWorkflow.Should().Throw<ArgumentNullException>().WithParameterName("instanceId");
        defaultWorkflow.Should().Throw<InvalidOperationException>();
        defaultRendering.Should().Throw<InvalidOperationException>();
    }
}
