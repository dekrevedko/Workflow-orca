using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;

namespace OrcaCore.Core.Tests.Contracts;

public sealed class WorkflowInstanceSnapshotTests
{
    [Fact]
    public void Snapshot_IsMetadataOnly()
    {
        var snapshotType = typeof(WorkflowInstanceSnapshot);

        snapshotType.IsGenericTypeDefinition.Should().BeFalse();
        snapshotType.GetGenericArguments().Should().BeEmpty();

        foreach (var property in snapshotType.GetProperties())
        {
            property.PropertyType.IsGenericTypeDefinition.Should().BeFalse(
                because: $"property {property.Name} must not expose open generic state");

            property.PropertyType.Should().NotBe(typeof(object),
                because: $"property {property.Name} must not expose untyped live references");

            if (property.PropertyType.IsGenericType)
            {
                property.PropertyType.GetGenericTypeDefinition().Should().NotBe(typeof(Nullable<>),
                    because: $"property {property.Name} must not expose nullable reference wrappers over live objects");
            }
        }

        foreach (var property in snapshotType.GetProperties())
        {
            var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

            if (propertyType == typeof(string))
            {
                continue;
            }

            propertyType.IsValueType.Should().BeTrue(
                because: $"property {property.Name} must be metadata-only (value type or string)");
        }
    }
}
