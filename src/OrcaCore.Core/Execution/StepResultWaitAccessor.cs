using System.Collections.Concurrent;

namespace OrcaCore.Core.Execution;

internal static class StepResultWaitAccessor
{
    private static readonly ConcurrentDictionary<Type, ITypedWaitAccessor> TypedAccessors = [];

    internal static bool TryGetWait(
        global::OrcaCore.StepResult result,
        out global::OrcaCore.WorkflowEventContract eventContract,
        out global::OrcaCore.CorrelationId correlationId)
    {
        if (result is global::OrcaCore.StepResult.WaitForEvent payloadless)
        {
            eventContract = payloadless.EventContract;
            correlationId = payloadless.CorrelationId;
            return true;
        }

        var resultType = result.GetType();
        if (!resultType.IsGenericType ||
            resultType.GetGenericTypeDefinition() != typeof(global::OrcaCore.StepResult.WaitForEvent<>))
        {
            eventContract = null!;
            correlationId = null!;
            return false;
        }

        // The public contract intentionally keeps both wait-result records sealed. Close one
        // compiled adapter per payload type; dispatch performs no property-name lookup or
        // reflective property invocation.
        var payloadType = resultType.GetGenericArguments()[0];
        var accessor = TypedAccessors.GetOrAdd(payloadType, static type =>
            (ITypedWaitAccessor)Activator.CreateInstance(
                typeof(TypedWaitAccessor<>).MakeGenericType(type))!);
        accessor.Read(result, out eventContract, out correlationId);
        return true;
    }

    private interface ITypedWaitAccessor
    {
        void Read(
            global::OrcaCore.StepResult result,
            out global::OrcaCore.WorkflowEventContract eventContract,
            out global::OrcaCore.CorrelationId correlationId);
    }

    private sealed class TypedWaitAccessor<TPayload> : ITypedWaitAccessor
    {
        public void Read(
            global::OrcaCore.StepResult result,
            out global::OrcaCore.WorkflowEventContract eventContract,
            out global::OrcaCore.CorrelationId correlationId)
        {
            var wait = (global::OrcaCore.StepResult.WaitForEvent<TPayload>)result;
            eventContract = wait.EventContract;
            correlationId = wait.CorrelationId;
        }
    }
}
