using OrcaCore;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Core.Definitions;
using OrcaCore.Internal;

namespace OrcaCore.Core.Authoring;

/// <summary>Builds a resultless ephemeral definition and exposes no further authoring.</summary>
internal sealed class EphemeralWorkflowCompletionBuilder<TInput>
{
    private readonly PublicDefinitionBuildResult frozenBuild;

    internal EphemeralWorkflowCompletionBuilder(PublicDefinitionBuildResult frozenBuild) =>
        this.frozenBuild = frozenBuild;

    public EphemeralWorkflowDefinition<TInput> Build() =>
        CompletionProjection.BuildOrThrow(TryBuild());

    public Validation<EphemeralWorkflowDefinition<TInput>> TryBuild()
    {
        var result = frozenBuild;
        return result.Build is { } build
            ? AuthoringContracts.Valid(
                AuthoringContracts.EphemeralDefinition<TInput>(
                    build.DefinitionId,
                    build.DefinitionVersion,
                    build.Fingerprint,
                    build.RuntimeDefinition,
                    build.RuntimeStateType))
            : AuthoringContracts.Invalid<EphemeralWorkflowDefinition<TInput>>(result.Errors);
    }
}

/// <summary>Builds a resultful ephemeral definition and exposes no further authoring.</summary>
internal sealed class EphemeralWorkflowCompletionBuilder<TInput, TOutput>
{
    private readonly PublicDefinitionBuildResult frozenBuild;

    internal EphemeralWorkflowCompletionBuilder(PublicDefinitionBuildResult frozenBuild) =>
        this.frozenBuild = frozenBuild;

    public EphemeralWorkflowDefinition<TInput, TOutput> Build() =>
        CompletionProjection.BuildOrThrow(TryBuild());

    public Validation<EphemeralWorkflowDefinition<TInput, TOutput>> TryBuild()
    {
        var result = frozenBuild;
        return result.Build is { } build
            ? AuthoringContracts.Valid(
                AuthoringContracts.EphemeralDefinition<TInput, TOutput>(
                    build.DefinitionId,
                    build.DefinitionVersion,
                    build.Fingerprint,
                    build.RuntimeDefinition,
                    build.RuntimeStateType))
            : AuthoringContracts.Invalid<EphemeralWorkflowDefinition<TInput, TOutput>>(result.Errors);
    }
}

/// <summary>Builds a resultless durable definition and exposes no further authoring.</summary>
internal sealed class DurableWorkflowCompletionBuilder<TInput>
{
    private readonly PublicDefinitionBuildResult frozenBuild;

    internal DurableWorkflowCompletionBuilder(PublicDefinitionBuildResult frozenBuild) =>
        this.frozenBuild = frozenBuild;

    public DurableWorkflowDefinition<TInput> Build() =>
        CompletionProjection.BuildOrThrow(TryBuild());

    public Validation<DurableWorkflowDefinition<TInput>> TryBuild()
    {
        var result = frozenBuild;
        return result.Build is { } build
            ? AuthoringContracts.Valid(
                AuthoringContracts.DurableDefinition<TInput>(
                    build.DefinitionId,
                    build.DefinitionVersion,
                    build.Fingerprint,
                    build.RuntimeDefinition,
                    build.RuntimeStateType))
            : AuthoringContracts.Invalid<DurableWorkflowDefinition<TInput>>(result.Errors);
    }
}

/// <summary>Builds a resultful durable definition and exposes no further authoring.</summary>
internal sealed class DurableWorkflowCompletionBuilder<TInput, TOutput>
{
    private readonly PublicDefinitionBuildResult frozenBuild;

    internal DurableWorkflowCompletionBuilder(PublicDefinitionBuildResult frozenBuild) =>
        this.frozenBuild = frozenBuild;

    public DurableWorkflowDefinition<TInput, TOutput> Build() =>
        CompletionProjection.BuildOrThrow(TryBuild());

    public Validation<DurableWorkflowDefinition<TInput, TOutput>> TryBuild()
    {
        var result = frozenBuild;
        return result.Build is { } build
            ? AuthoringContracts.Valid(
                AuthoringContracts.DurableDefinition<TInput, TOutput>(
                    build.DefinitionId,
                    build.DefinitionVersion,
                    build.Fingerprint,
                    build.RuntimeDefinition,
                    build.RuntimeStateType))
            : AuthoringContracts.Invalid<DurableWorkflowDefinition<TInput, TOutput>>(result.Errors);
    }
}

internal static class PublicDefinitionBuilder
{
    internal static PublicDefinitionBuildResult TryBuild<TState>(
        global::OrcaCore.Core.Building.EphemeralWorkflowBuilder<TState> builder) =>
        Convert(builder.TryBuild());

    internal static PublicDefinitionBuildResult TryBuild<TState>(
        global::OrcaCore.Core.Building.DurableWorkflowBuilder<TState> builder) =>
        Convert(builder.TryBuild());

    private static PublicDefinitionBuildResult Convert<TState>(
        global::OrcaCore.Abstractions.Primitives.Validation<WorkflowDefinition<TState>> validation)
    {
        if (!validation.IsValid)
        {
            return new PublicDefinitionBuildResult(null, validation.Errors);
        }

        var definition = validation.Value;
        return new PublicDefinitionBuildResult(
            new PublicDefinitionBuild(
                definition.DefinitionId,
                definition.DefinitionVersion,
                AuthoringContracts.Fingerprint(definition.CompiledPlan.Fingerprint),
                definition,
                typeof(TState)),
            []);
    }
}

internal sealed record PublicDefinitionBuild(
    DefinitionId DefinitionId,
    DefinitionVersion DefinitionVersion,
    DefinitionFingerprint Fingerprint,
    object RuntimeDefinition,
    Type RuntimeStateType);

internal sealed record PublicDefinitionBuildResult(
    PublicDefinitionBuild? Build,
    IReadOnlyList<ValidationError> Errors);

file static class CompletionProjection
{
    internal static T BuildOrThrow<T>(Validation<T> validation)
    {
        if (validation.TryGetValue(out var value))
        {
            return value!;
        }

        throw AuthoringContracts.DefinitionException(validation.Diagnostics);
    }
}
