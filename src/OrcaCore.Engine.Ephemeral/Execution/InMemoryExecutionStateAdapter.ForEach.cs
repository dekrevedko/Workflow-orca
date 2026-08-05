using System.Collections;
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
            throw global::OrcaCore.Engine.Ephemeral.Internal.EphemeralApplicationContractFactory.DefinitionException("Compiled ForEach contract is missing.");
        if (parentState is not TState typedParent)
        {
            throw global::OrcaCore.Engine.Ephemeral.Internal.EphemeralApplicationContractFactory.DefinitionException(
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
            throw global::OrcaCore.Engine.Ephemeral.Internal.EphemeralApplicationContractFactory.DefinitionException(
                "ForEach item selection failed.",
                exception.InnerException);
        }

        items = ForEachSnapshotMaterializer.Materialize(items, forEach.ItemType);
        var partitionMethod = forEach.Partitioner.GetType().GetMethod("Partition") ??
            throw global::OrcaCore.Engine.Ephemeral.Internal.EphemeralApplicationContractFactory.DefinitionException(
                "ForEach partitioner has no Partition method.");
        var partitions = partitionMethod.Invoke(forEach.Partitioner, [items]) as IEnumerable ??
            throw global::OrcaCore.Engine.Ephemeral.Internal.EphemeralApplicationContractFactory.DefinitionException(
                "ForEach partitioner returned no work descriptors.");
        var inputType = typeof(global::OrcaCore.Core.Building.ForEachItemInput<>)
            .MakeGenericType(forEach.ItemType);
        var branch = scopePlan.Branches.Single();
        var descriptors = new List<ForEachItemDescriptor>();
        foreach (var partition in partitions)
        {
            var partitionType = partition!.GetType();
            var index = (int)(partitionType.GetProperty("Index")?.GetValue(partition) ??
                throw global::OrcaCore.Engine.Ephemeral.Internal.EphemeralApplicationContractFactory.DefinitionException(
                    "ForEach partition index is missing."));
            var partitionItems = partitionType.GetProperty("Items")?.GetValue(partition) ??
                throw global::OrcaCore.Engine.Ephemeral.Internal.EphemeralApplicationContractFactory.DefinitionException(
                    "ForEach partition items are missing.");
            var input = Activator.CreateInstance(inputType, index, partitionItems) ??
                throw global::OrcaCore.Engine.Ephemeral.Internal.EphemeralApplicationContractFactory.DefinitionException(
                    $"Could not create ForEach item input for index '{index}'.");
            object? itemState;
            try
            {
                itemState = StructuredInvocationCache.Invoke(forEach.ItemStateProjector, input);
            }
            catch (TargetInvocationException exception) when (exception.InnerException is not null)
            {
                throw global::OrcaCore.Engine.Ephemeral.Internal.EphemeralApplicationContractFactory.DefinitionException(
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
