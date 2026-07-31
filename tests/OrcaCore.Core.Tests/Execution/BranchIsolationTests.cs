using System.Text.Json;
using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Building;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Execution;
using Xunit;

namespace OrcaCore.Core.Tests.Execution;

public sealed class BranchIsolationTests
{
    [Fact]
    public void MaterializedBranchInput_CannotMutateParentOrSiblingAliases()
    {
        var parent = new ParentState(["original"]);
        var plan = global::OrcaCore.Workflow.Ephemeral<ParentState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => parent)
            .Parallel<string>(
                branches => branches
                    .Branch<BranchState>("first", snapshot => new BranchState(snapshot.Value.Items), branch =>
                        branch.Return(_ => "first"))
                    .Branch<BranchState>("second", snapshot => new BranchState(snapshot.Value.Items), branch =>
                        branch.Return(_ => "second")),
                (snapshot, _) => snapshot.Value)
            .End()
            .Build()
            .CompiledPlan;
        var scope = plan.Scopes.Should().ContainSingle().Which;
        var codec = new JsonStructuredValueCodec();

        var firstPayload = BranchInputMaterializer.Materialize(scope.Branches[0].Input, parent, codec);
        var secondPayload = BranchInputMaterializer.Materialize(scope.Branches[1].Input, parent, codec);
        var first = codec.Deserialize(firstPayload) as BranchState ??
            throw new InvalidOperationException("First branch state did not deserialize.");
        var second = codec.Deserialize(secondPayload) as BranchState ??
            throw new InvalidOperationException("Second branch state did not deserialize.");
        first.Items.Add("first-only");

        parent.Items.Should().Equal("original");
        second.Items.Should().Equal("original");
    }

    private sealed class JsonStructuredValueCodec : IStructuredValueCodec
    {
        public StructuredSerializedValue Serialize(object? value, Type declaredType, string schemaIdentity)
        {
            return new StructuredSerializedValue(
                declaredType,
                schemaIdentity,
                JsonSerializer.SerializeToUtf8Bytes(value, declaredType));
        }

        public object? Deserialize(StructuredSerializedValue value)
        {
            return JsonSerializer.Deserialize(value.Payload, value.DeclaredType);
        }
    }

    private sealed record ParentState(List<string> Items);

    private sealed record BranchState(List<string> Items);
}
