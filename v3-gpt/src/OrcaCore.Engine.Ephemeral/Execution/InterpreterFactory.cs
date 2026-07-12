using OrcaCore.Engine.Ephemeral.Governance;
using OrcaCore.Engine.Ephemeral.Timers;

namespace OrcaCore.Engine.Ephemeral.Execution;

internal sealed class InterpreterFactory(
    TimeProvider timeProvider,
    EphemeralTimerService timerService,
    ResourceGovernanceCoordinator governance,
    YieldContinuationScheduler yieldContinuationScheduler,
    EphemeralWorkflowEngineOptions options)
{
    internal Interpreter<TState> Create<TState>()
    {
        var failureHandler = new WorkflowFailureHandler<TState>(timeProvider);
        var conditionEvaluator = new ConditionEvaluator<TState>(failureHandler);
        var suspensionScheduler = new SuspensionScheduler<TState>(timeProvider, timerService);
        return new Interpreter<TState>(
            timeProvider,
            new StepExecutor<TState>(timeProvider, governance, options.StuckStepThreshold),
            failureHandler,
            conditionEvaluator,
            suspensionScheduler,
            new WaitExecutor<TState>(suspensionScheduler, failureHandler),
            new WhileNodeRunner<TState>(conditionEvaluator),
            new ParallelNodeRunner<TState>(),
            new WhenFirstNodeRunner<TState>(timeProvider),
            new ForEachNodeRunner<TState>(timeProvider),
            yieldContinuationScheduler,
            options);
    }
}
