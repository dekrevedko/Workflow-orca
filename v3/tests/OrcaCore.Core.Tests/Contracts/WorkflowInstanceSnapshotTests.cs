using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;

namespace OrcaCore.Core.Tests.Contracts;

public class WorkflowInstanceSnapshotTests
{
    [Fact]
    public void Snapshot_IsMetadataOnly()
    {
        var type = typeof(WorkflowInstanceSnapshot);

        type.IsGenericType.Should().BeFalse("a snapshot must never expose TState");

        var allowedPropertyTypes = new HashSet<Type>
        {
            typeof(InstanceId),
            typeof(DefinitionId),
            typeof(DefinitionVersion),
            typeof(WorkflowStatus),
            typeof(DateTimeOffset),
            typeof(string),
        };

        foreach (var property in type.GetProperties())
        {
            var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            allowedPropertyTypes.Should().Contain(
                propertyType,
                $"{property.Name} must be a metadata-only value, not a live/mutable reference");
        }
    }
}
