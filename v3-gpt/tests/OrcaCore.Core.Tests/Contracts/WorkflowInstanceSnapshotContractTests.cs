using OrcaCore.Abstractions.Instances;
using Xunit;

namespace OrcaCore.Core.Tests.Contracts;

public sealed class WorkflowInstanceSnapshotContractTests
{
    [Fact]
    public void Snapshot_IsMetadataOnly()
    {
        var propertyTypes = typeof(WorkflowInstanceSnapshot)
            .GetProperties()
            .Select(property => property.PropertyType)
            .ToArray();

        Assert.DoesNotContain(propertyTypes, type => type.IsGenericParameter);
        Assert.DoesNotContain(propertyTypes, type => type.Name.Contains("State", StringComparison.Ordinal));
        Assert.DoesNotContain(propertyTypes, type => type == typeof(object));
    }
}
