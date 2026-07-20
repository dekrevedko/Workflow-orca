using System.Collections;
using System.Reflection;
using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Core.Building;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Definitions;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
public sealed class CompilerDefenseGuards
{
    [Fact]
    public void ForgedDurableForEach_IsRejectedByCompilerDefenseInDepth()
    {
        var definitionId = DefinitionId.New();
        var builder = Workflow.Ephemeral<State>(definitionId, DefinitionVersion.Initial)
            .Init<string>(_ => new State())
            .ForEach<int, State, int>(
                _ => [1],
                WorkflowPartitioner<int>.Items(),
                _ => new State(),
                branch => branch.Return(_ => 1),
                ForEachJoinPolicy.WhenAll,
                ForEachFailurePolicy.FailFast)
            .End();

        var builderBase = builder.GetType().BaseType!;
        var authoringField = builderBase.GetField("authoring", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var ephemeralAuthoring = authoringField.GetValue(builder)!;
        var authoringType = ephemeralAuthoring.GetType();
        var constructor = authoringType.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
        var durableAuthoring = constructor.Invoke(
            [definitionId, DefinitionVersion.Initial, WorkflowExecutionMode.Durable]);
        var nodesProperty = authoringType.GetProperty("RootNodes", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var sourceNodes = (IEnumerable)nodesProperty.GetValue(ephemeralAuthoring)!;
        var targetNodes = (IList)nodesProperty.GetValue(durableAuthoring)!;
        foreach (var node in sourceNodes)
        {
            targetNodes.Add(node);
        }

        var compiler = typeof(DefinitionCompilerCodes).Assembly.GetType("OrcaCore.Core.Compilation.DefinitionCompiler")!;
        var compile = compiler.GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Single(method => method.Name == "Compile")
            .MakeGenericMethod(typeof(State));
        var validation = compile.Invoke(null, [durableAuthoring])!;
        var errors = (IEnumerable)validation.GetType().GetProperty("Errors")!.GetValue(validation)!;
        var codes = errors.Cast<object>()
            .Select(error => (string)error.GetType().GetProperty("Code")!.GetValue(error)!)
            .ToArray();

        codes.Should().Contain(DefinitionCompilerCodes.UnsupportedInstruction);
    }

    public sealed record State;
}
