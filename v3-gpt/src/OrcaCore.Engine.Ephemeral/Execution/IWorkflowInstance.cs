using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;

namespace OrcaCore.Engine.Ephemeral.Execution;

internal interface IWorkflowInstance
{
    InstanceId InstanceId { get; }

    Type StateType { get; }

    object StateObject { get; }

    WorkflowInstanceSnapshot ToSnapshot();
}
