using System.Reflection;
using OrcaCore.Core.Building;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Execution;

namespace OrcaCore.Engine.Ephemeral.Execution;

internal sealed partial class InMemoryExecutionStateAdapter<TState>
{
    private IReadOnlyList<ForEachItemDescriptor> MaterializeForEachDescriptors(
        CompiledScopePlan scopePlan,
        object parentState)
    {
        var forEach = scopePlan.ForEach ??
            throw global::OrcaCore.Engine.Ephemeral.Internal.EphemeralContractAdapter.DefinitionException("Compiled ForEach contract is missing.");
        if (parentState is not TState typedParent)
        {
            throw global::OrcaCore.Engine.Ephemeral.Internal.EphemeralContractAdapter.DefinitionException(
                $"ForEach parent state must be '{typeof(TState).FullName}'.");
        }

        object? items;
        try
        {
            items = StructuredInvocationCache.Invoke(
                forEach.ItemSelector,
                new ReadOnlyParentSnapshot<TState>(typedParent));
        }
        catch (TargetInvocationException exception)
            when (exception.InnerException is not null and not StructuredExecutionLimitException)
        {
            throw global::OrcaCore.Engine.Ephemeral.Internal.EphemeralContractAdapter.DefinitionException(
                "ForEach item selection failed.",
                exception.InnerException);
        }

        items = ForEachSnapshotMaterializer.Materialize(items, forEach.ItemType);
        var partitions = ClosedForEachBindings.For(forEach.Partitioner).Partition(items);
        var branch = scopePlan.Branches.Single();
        var descriptors = new List<ForEachItemDescriptor>();
        foreach (var partition in partitions)
        {
            var index = partition.Index;
            var input = partition.Input;
            object? itemState;
            try
            {
                itemState = StructuredInvocationCache.Invoke(forEach.ItemStateProjector, input);
            }
            catch (TargetInvocationException exception) when (exception.InnerException is not null)
            {
                throw global::OrcaCore.Engine.Ephemeral.Internal.EphemeralContractAdapter.DefinitionException(
                    $"ForEach item-state projection failed for index '{index}'.",
                    exception.InnerException);
            }

            descriptors.Add(new ForEachItemDescriptor(
                index,
                codec.Serialize(
                    itemState,
                    branch.Input.BranchStateType,
                    branch.Input.BranchStateSchemaIdentity).Payload));
        }

        return descriptors.OrderBy(descriptor => descriptor.Index).ToArray();
    }
}
