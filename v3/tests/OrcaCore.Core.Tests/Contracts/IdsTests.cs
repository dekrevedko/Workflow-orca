using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Core.Tests.Contracts;

public class IdsTests
{
    [Fact]
    public void Ids_New_AreUniqueAndVersion7Ordered()
    {
        var first = InstanceId.New();
        var second = InstanceId.New();

        first.Should().NotBe(second);
        string.CompareOrdinal(first.ToString(), second.ToString()).Should().BeLessThan(0);
    }

    [Fact]
    public void EventId_New_AreUnique()
    {
        var first = EventId.New();
        var second = EventId.New();

        first.Should().NotBe(second);
    }

    [Fact]
    public void WaitId_New_AreUnique()
    {
        var first = WaitId.New();
        var second = WaitId.New();

        first.Should().NotBe(second);
    }

    [Fact]
    public void DefinitionId_Empty_Throws()
    {
        var act = () => new DefinitionId(string.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void DefinitionVersion_NonPositive_Throws()
    {
        var act = () => new DefinitionVersion(0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CorrelationId_Empty_Throws()
    {
        var act = () => new CorrelationId(string.Empty);

        act.Should().Throw<ArgumentException>();
    }
}
