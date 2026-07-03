using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Building;

namespace OrcaCore.Acceptance.Tests;

public class BuilderAcceptanceTests
{
    private sealed class OrderState
    {
        public int Total { get; set; }
    }

    [Trait("AC", "AC-008")]
    [Fact]
    public void Build_AccumulatesAllValidationErrors()
    {
        var builder = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        builder.If(condition: null!, then => then.End());
        builder.Parallel();
        builder.While(condition: null!, body => body.End());

        var result = builder.BuildValidated(
            new DefinitionId(Guid.Parse("aaaaaaaa-aaaa-7aaa-8aaa-aaaaaaaaaaaa")),
            new DefinitionVersion(1));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCountGreaterThanOrEqualTo(3);
        result.Errors.Select(e => e.Code).Should().Contain(
        [
            WorkflowBuilderValidationCodes.NullCondition,
            WorkflowBuilderValidationCodes.EmptyParallel,
        ]);
    }
}
