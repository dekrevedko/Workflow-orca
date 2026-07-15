using System.Text.Json;
using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Building;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Execution;
using Xunit;

namespace OrcaCore.Core.Tests.Execution;

public sealed class ScopeMergeAdapterTests
{
    [Fact]
    public void WhenAllMerge_UsesAuthoredResultOrder_AndReturnsSerializedReplacementState()
    {
        var branchReturnInvocations = 0;
        var plan = Workflow.Ephemeral<ParentState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(_ => new ParentState([0]))
            .Parallel<int>(
                branches => branches
                    .Branch<BranchState>("first", _ => new BranchState(1), branch => branch.Return(state =>
                    {
                        branchReturnInvocations++;
                        return state.Value.Value;
                    }))
                    .Branch<BranchState>("second", _ => new BranchState(2), branch => branch.Return(state =>
                    {
                        branchReturnInvocations++;
                        return state.Value.Value;
                    })),
                (parent, results) => new ParentState(
                    [.. parent.Value.Values, .. results.Select(result => result.Value)]))
            .End()
            .Build()
            .CompiledPlan;
        var scope = plan.Scopes.Should().ContainSingle().Which;
        var codec = new TrackingJsonCodec();
        var first = scope.Branches[0];
        var second = scope.Branches[1];
        var unordered = new[]
        {
            new MaterializedBranchResult(
                second.Id,
                codec.Serialize(2, typeof(int), second.Result.ResultSchemaIdentity)),
            new MaterializedBranchResult(
                first.Id,
                codec.Serialize(1, typeof(int), first.Result.ResultSchemaIdentity))
        };
        var parent = new ParentState([0]);

        var replacementPayload = ScopeMergeAdapter.Execute(scope, parent, unordered, codec);
        var replacement = codec.Deserialize(replacementPayload) as ParentState ??
            throw new InvalidOperationException("Replacement state did not deserialize.");

        replacement.Values.Should().Equal(0, 1, 2);
        parent.Values.Should().Equal(0);
        replacementPayload.DeclaredType.Should().Be(typeof(ParentState));
        replacementPayload.SchemaIdentity.Should().Be(scope.Merge.ParentStateSchemaIdentity);
        branchReturnInvocations.Should().Be(0, "merge consumes committed results and never reruns branches");
    }

    [Fact]
    public void MergeException_LeavesOriginalParentUnchanged_AndReturnsStableDiagnostic()
    {
        var plan = Workflow.Ephemeral<ParentState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(_ => new ParentState([0]))
            .Parallel<int>(
                branches => branches
                    .Branch<BranchState>("first", _ => new BranchState(1), branch =>
                        branch.Return(state => state.Value.Value))
                    .Branch<BranchState>("second", _ => new BranchState(2), branch =>
                        branch.Return(state => state.Value.Value)),
                ThrowingMerge)
            .End()
            .Build()
            .CompiledPlan;
        var scope = plan.Scopes.Should().ContainSingle().Which;
        var codec = new TrackingJsonCodec();
        var parent = new ParentState([0]);
        var results = scope.Branches.Select(branch => new MaterializedBranchResult(
            branch.Id,
            codec.Serialize(branch.Ordinal + 1, typeof(int), branch.Result.ResultSchemaIdentity))).ToArray();

        var merge = () => ScopeMergeAdapter.Execute(scope, parent, results, codec);

        merge.Should().Throw<StructuredMergeException>()
            .Which.Code.Should().Be("SFE-MERGE-001");
        parent.Values.Should().Equal(0);
    }

    [Fact]
    public void WhenFirstMerge_ReceivesOnlyTheCommittedWinner()
    {
        var plan = Workflow.Ephemeral<ParentState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(_ => new ParentState([0]))
            .WhenFirst<int>(
                branches => branches
                    .Branch<BranchState>("first", _ => new BranchState(1), branch =>
                        branch.Return(state => state.Value.Value))
                    .Branch<BranchState>("second", _ => new BranchState(2), branch =>
                        branch.Return(state => state.Value.Value)),
                (parent, winner) => new ParentState([.. parent.Value.Values, winner.Value]))
            .End()
            .Build()
            .CompiledPlan;
        var scope = plan.Scopes.Should().ContainSingle().Which;
        var winner = scope.Branches[1];
        var codec = new TrackingJsonCodec();
        var committedWinner = new MaterializedBranchResult(
            winner.Id,
            codec.Serialize(2, typeof(int), winner.Result.ResultSchemaIdentity));

        var payload = ScopeMergeAdapter.Execute(
            scope,
            new ParentState([0]),
            [committedWinner],
            codec);
        var replacement = codec.Deserialize(payload) as ParentState ??
            throw new InvalidOperationException("Winner merge state did not deserialize.");

        replacement.Values.Should().Equal(0, 2);
    }

    private static ParentState ThrowingMerge(
        ReadOnlyParentSnapshot<ParentState> parent,
        IReadOnlyList<BranchResult<int>> results)
    {
        parent.Value.Values.Add(99);
        throw new InvalidOperationException("merge failed");
    }

    private sealed class TrackingJsonCodec : IStructuredValueCodec
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

    private sealed record ParentState(List<int> Values);

    private sealed record BranchState(int Value);
}
