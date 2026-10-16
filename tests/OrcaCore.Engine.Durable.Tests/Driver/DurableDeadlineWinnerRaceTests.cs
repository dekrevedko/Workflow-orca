using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using AwesomeAssertions;
using OrcaCore.Engine.Durable.Driver;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Driver;

public sealed class DurableDeadlineWinnerRaceTests
{
    private static readonly OpCode[] OneByteOpCodes = BuildOpCodeTable(twoByte: false);
    private static readonly OpCode[] TwoByteOpCodes = BuildOpCodeTable(twoByte: true);

    [Fact]
    public async Task SelectedTimeout_RemainsWinnerWhenPostSelectionWorkCompletesExecution()
    {
        var execution = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var timeout = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var selection = DurablePolicyWinnerSelector.TimeoutWonAsync(
            execution.Task,
            timeout.Task,
            () =>
            {
                execution.TrySetResult();
                return ValueTask.CompletedTask;
            });

        timeout.TrySetResult();
        var timeoutWon = await selection;

        execution.Task.IsCompleted.Should().BeTrue();
        timeoutWon.Should().BeTrue();
    }

    [Fact]
    public void TimeoutWinnerSelector_SelectsOnceAndDoesNotReobserveTaskCompletion()
    {
        // The external race has no deterministic seam between WhenAny resuming and a second
        // task-state read, so pin the arbitration structure in the compiled state machine.
        var selector = typeof(DurablePolicyWinnerSelector).GetMethod(
            nameof(DurablePolicyWinnerSelector.TimeoutWonAsync),
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var stateMachine = selector.GetCustomAttribute<AsyncStateMachineAttribute>()!;
        var moveNext = stateMachine.StateMachineType.GetMethod(
            "MoveNext",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var calls = ReadMethodOperands(moveNext).ToArray();

        calls.Count(IsWhenAny).Should().Be(
            1,
            "deadline arbitration must select one winner exactly once");
        calls.Where(IsTaskCompletionObservation).Should().BeEmpty(
            "the selected winner must not be overturned by re-observing either task");
    }

    private static bool IsWhenAny(MethodBase method)
        => method.DeclaringType == typeof(Task)
            && method.Name == nameof(Task.WhenAny);

    private static bool IsTaskCompletionObservation(MethodBase method)
        => method.DeclaringType == typeof(Task)
            && method.Name is "get_IsCompleted"
                or "get_IsCompletedSuccessfully"
                or "get_IsCanceled"
                or "get_IsFaulted"
                or "get_Status";

    private static IEnumerable<MethodBase> ReadMethodOperands(MethodInfo method)
    {
        var il = method.GetMethodBody()!.GetILAsByteArray()!;
        var declaringTypeArguments = method.DeclaringType?.GetGenericArguments();
        var methodArguments = method.GetGenericArguments();

        for (var offset = 0; offset < il.Length;)
        {
            var opCode = ReadOpCode(il, ref offset);
            if (opCode.OperandType == OperandType.InlineMethod)
            {
                var token = BitConverter.ToInt32(il, offset);
                yield return method.Module.ResolveMethod(
                    token,
                    declaringTypeArguments,
                    methodArguments)!;
            }

            offset += GetOperandSize(opCode.OperandType, il, offset);
        }
    }

    private static OpCode ReadOpCode(byte[] il, ref int offset)
    {
        var first = il[offset++];
        return first == 0xfe
            ? TwoByteOpCodes[il[offset++]]
            : OneByteOpCodes[first];
    }

    private static int GetOperandSize(OperandType operandType, byte[] il, int offset)
        => operandType switch
        {
            OperandType.InlineNone => 0,
            OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
            OperandType.InlineVar => 2,
            OperandType.InlineBrTarget
                or OperandType.InlineField
                or OperandType.InlineI
                or OperandType.InlineMethod
                or OperandType.InlineSig
                or OperandType.InlineString
                or OperandType.InlineTok
                or OperandType.InlineType
                or OperandType.ShortInlineR => 4,
            OperandType.InlineI8 or OperandType.InlineR => 8,
            OperandType.InlineSwitch => 4 + (BitConverter.ToInt32(il, offset) * 4),
            _ => throw new InvalidOperationException($"Unsupported IL operand type '{operandType}'.")
        };

    private static OpCode[] BuildOpCodeTable(bool twoByte)
    {
        var table = new OpCode[256];
        foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var opCode = (OpCode)field.GetValue(null)!;
            var value = unchecked((ushort)opCode.Value);
            if ((value > byte.MaxValue) == twoByte)
            {
                table[value & byte.MaxValue] = opCode;
            }
        }

        return table;
    }
}
