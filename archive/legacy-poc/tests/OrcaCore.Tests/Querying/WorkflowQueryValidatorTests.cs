using System.Linq.Expressions;

namespace OrcaCore.Tests;

public class WorkflowQueryValidatorTests
{
    [Fact]
    public void Allows_simple_status_equality()
    {
        Expression<Func<WorkflowInstanceSnapshot, bool>> expression =
            x => x.Status == WorkflowStatus.Waiting;

        WorkflowQueryValidator.Validate(expression);
    }

    [Fact]
    public void Allows_captured_constant()
    {
        var definitionId = "Def1";
        Expression<Func<WorkflowInstanceSnapshot, bool>> expression =
            x => x.DefinitionId == definitionId;

        WorkflowQueryValidator.Validate(expression);
    }

    [Fact]
    public void Allows_composed_boolean_expression()
    {
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(1);
        Expression<Func<WorkflowInstanceSnapshot, bool>> expression =
            x => x.Status == WorkflowStatus.Waiting && x.CreatedAt < cutoff;

        WorkflowQueryValidator.Validate(expression);
    }

    [Fact]
    public void Rejects_method_calls()
    {
        Expression<Func<WorkflowInstanceSnapshot, bool>> expression =
            x => x.DefinitionId.StartsWith("Def");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            WorkflowQueryValidator.Validate(expression));

        Assert.Contains("Unsupported", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rejects_arithmetic_operators()
    {
        Expression<Func<WorkflowInstanceSnapshot, bool>> expression =
            x => x.InstanceId.Length + 1 > 3;

        var ex = Assert.Throws<InvalidOperationException>(() =>
            WorkflowQueryValidator.Validate(expression));

        Assert.Contains("Unsupported", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
