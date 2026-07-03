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
            IsMetadataOnly(property.PropertyType).Should().BeTrue(
                because: $"property {property.Name} must be metadata-only (value type, string, or an immutable " +
                    "collection/record composed entirely of metadata-only members)");
        }
    }

    /// <summary>
    /// A type is metadata-only when it is a value type, a string, a read-only collection of a
    /// metadata-only element type (e.g. <see cref="ActiveWaitSnapshot"/> lists), or a sealed
    /// record whose own public properties are all metadata-only. This keeps snapshot types
    /// free of live runtime references (CR-021) without forcing every nested field into a
    /// value type.
    /// </summary>
    private static bool IsMetadataOnly(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;

        if (underlying == typeof(string) || underlying.IsValueType)
        {
            return true;
        }

        if (underlying.IsGenericType)
        {
            var elementType = underlying.GetGenericArguments() is [var single] ? single : null;
            var isReadOnlyCollection = underlying.GetInterfaces().Concat([underlying])
                .Any(candidate => candidate.IsGenericType &&
                    candidate.GetGenericTypeDefinition() == typeof(IReadOnlyList<>));

            if (isReadOnlyCollection && elementType is not null)
            {
                return IsMetadataOnly(elementType);
            }
        }

        return underlying.IsSealed &&
            underlying.GetProperties().All(nested => IsMetadataOnly(nested.PropertyType));
    }
}
