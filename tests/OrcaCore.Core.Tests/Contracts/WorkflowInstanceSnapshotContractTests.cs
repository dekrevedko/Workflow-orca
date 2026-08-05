using AwesomeAssertions;
using OrcaCore;
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

        propertyTypes.Should().NotContain(type => type.IsGenericParameter);
        propertyTypes.Should().NotContain(type => type.Name.Contains("State", StringComparison.Ordinal));
        propertyTypes.Should().NotContain(type => type == typeof(object));
    }
}
