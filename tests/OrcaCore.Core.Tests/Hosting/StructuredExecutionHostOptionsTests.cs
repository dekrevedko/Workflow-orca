using AwesomeAssertions;
using OrcaCore.Hosting;
using Xunit;

namespace OrcaCore.Core.Tests.Hosting;

public sealed class StructuredExecutionHostOptionsTests
{
    [Fact]
    public void StepThrottle_RequiresNamedStepTypeAndPositiveCapacity()
    {
        var throttle = StepExecutionThrottle.For<NamedStep>(3);

        throttle.StepType.Should().Be(typeof(NamedStep));
        throttle.MaxConcurrency.Should().Be(3);
        Action invalidCapacity = () => StepExecutionThrottle.For<NamedStep>(0);
        invalidCapacity.Should().Throw<ArgumentOutOfRangeException>();
        Action invalidStep = () => StepExecutionThrottle.For<string>(1);
        invalidStep.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ValidateAndCopy_RejectsDuplicateExactStepTypes()
    {
        var options = new StructuredExecutionHostOptions
        {
            MaxConcurrentExecutionPathsPerInstance = 2,
            StepThrottles =
            [
                StepExecutionThrottle.For<NamedStep>(1),
                StepExecutionThrottle.For<NamedStep>(2)
            ]
        };

        Action act = () => options.ValidateAndCopy();

        act.Should().Throw<ArgumentException>()
            .WithMessage("*duplicate*NamedStep*");
    }

    private sealed class NamedStep : IStep<State>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<State> context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class State;
}
