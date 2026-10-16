using System.Text.Json;
using OrcaCore.Abstractions.Contracts;
using OrcaCore.Abstractions.Models;
using OrcaCore.EventDrivenPrototype.Definitions;
using OrcaCore.EventDrivenPrototype.Persistence;

namespace OrcaCore.EventDrivenPrototype.Engine;

internal sealed class RegisteredPrototypeDefinition
{
    private readonly Func<object?, EventEnvelope?, CancellationToken, Task<PrototypeExecutionResult>> _runToSuspension;

    public RegisteredPrototypeDefinition(string definitionId, string definitionVersion, Type stateType, Func<object?, EventEnvelope?, CancellationToken, Task<PrototypeExecutionResult>> runToSuspension)
    {
        DefinitionId = definitionId;
        DefinitionVersion = definitionVersion;
        StateType = stateType;
        _runToSuspension = runToSuspension;
    }

    public string DefinitionId { get; }
    public string DefinitionVersion { get; }
    public Type StateType { get; }

    public Task<PrototypeExecutionResult> RunToSuspensionAsync(object? aggregateState, EventEnvelope? resumedEvent, CancellationToken cancellationToken)
        => _runToSuspension(aggregateState, resumedEvent, cancellationToken);

    public static RegisteredPrototypeDefinition Create<TState>(EventDrivenWorkflowDefinition<TState> definition)
    {
        return new RegisteredPrototypeDefinition(
            definition.DefinitionId,
            definition.DefinitionVersion,
            typeof(TState),
            async (aggregateState, resumedEvent, cancellationToken) =>
            {
                TState state;
                var nextStepIndex = 0;

                if (aggregateState is PrototypeCheckpointState checkpointState)
                {
                    state = PrototypeCloner.Clone((TState)checkpointState.BusinessState);
                    nextStepIndex = checkpointState.NextStepIndex;
                }
                else
                {
                    state = PrototypeCloner.Clone((TState?)aggregateState ?? Activator.CreateInstance<TState>());
                }

                while (nextStepIndex < definition.Steps.Count)
                {
                    var step = definition.Steps[nextStepIndex];
                    var context = new StepContext<TState>(
                        state,
                        "",
                        definition.DefinitionId,
                        step.StepId,
                        cancellationToken,
                        resumedEvent);

                    var result = await step.ExecuteAsync(context);

                    switch (result)
                    {
                        case StepResult.Completed:
                            nextStepIndex++;
                            resumedEvent = null;
                            continue;

                        case StepResult.Yield:
                            return PrototypeExecutionResult.CreateYielded(
                                state!,
                                nextStepIndex + 1);

                        case StepResult.Failed failed:
                            return PrototypeExecutionResult.CreateFailed(
                                state!,
                                nextStepIndex,
                                failed.Error);

                        case StepResult.WaitForEvent wait:
                            return PrototypeExecutionResult.CreateWaiting(
                                state!,
                                nextStepIndex + 1,
                                wait.EventName,
                                wait.CorrelationId);

                        default:
                            throw new InvalidOperationException($"Unsupported step result type '{result.GetType().Name}'.");
                    }
                }

                return PrototypeExecutionResult.CreateCompleted(state!, nextStepIndex);
            });
    }
}
