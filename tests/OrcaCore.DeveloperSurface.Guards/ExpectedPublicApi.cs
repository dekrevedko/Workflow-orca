using System.Reflection;
using AwesomeAssertions;

namespace OrcaCore.DeveloperSurface.Guards;

internal static class ExpectedPublicApi
{
    internal static object RequiredProperty(object target, string propertyName)
    {
        var property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        property.Should().NotBeNull($"{target.GetType().Name}.{propertyName} is part of the approved application contract");
        return property!.GetValue(target)!;
    }

    internal static async Task<T> InvokeGenericAsync<T>(
        object target,
        string methodName,
        CancellationToken cancellationToken)
    {
        var methods = target.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => method.Name == methodName && method.IsGenericMethodDefinition)
            .Where(method => method.GetGenericArguments().Length == 1)
            .Where(method => method.GetParameters().Length == 1 &&
                             method.GetParameters()[0].ParameterType == typeof(CancellationToken))
            .ToArray();
        methods.Should().ContainSingle(
            $"{target.GetType().Name}.{methodName}<T>(CancellationToken) must be the typed application projection");
        var invocation = methods[0].MakeGenericMethod(typeof(T)).Invoke(target, [cancellationToken]);
        return (T)(await AwaitResultAsync(invocation).ConfigureAwait(false))!;
    }

    internal static async Task<object?> InvokeAsync(object target, string methodName, params object?[] arguments)
    {
        var candidates = target.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => method.Name == methodName)
            .Where(method => method.GetParameters().Length == arguments.Length)
            .ToArray();
        candidates.Should().NotBeEmpty(
            $"{target.GetType().Name}.{methodName} with {arguments.Length} arguments is required by the approved facade contract");

        var method = candidates.FirstOrDefault(candidate => ParametersAccept(candidate, arguments)) ?? candidates[0];
        if (method.IsGenericMethodDefinition)
        {
            var payload = arguments.FirstOrDefault(argument => argument is not null &&
                argument.GetType().Assembly.GetName().Name == typeof(ExpectedPublicApi).Assembly.GetName().Name);
            method = method.MakeGenericMethod(payload?.GetType() ?? typeof(object));
        }

        return await AwaitResultAsync(method.Invoke(target, arguments)).ConfigureAwait(false);
    }

    internal static string OutcomeName(object? result)
    {
        result.Should().NotBeNull("application operations return an explicit result");
        foreach (var name in new[] { "Outcome", "Status", "Disposition", "Code" })
        {
            var value = result!.GetType().GetProperty(name)?.GetValue(result);
            if (value is not null)
            {
                return value.ToString()!;
            }
        }

        return result!.ToString()!;
    }

    private static bool ParametersAccept(MethodInfo method, object?[] arguments)
    {
        var parameters = method.GetParameters();
        for (var index = 0; index < parameters.Length; index++)
        {
            if (arguments[index] is null)
            {
                continue;
            }

            var parameterType = parameters[index].ParameterType;
            if (parameterType.IsGenericParameter || parameterType.IsAssignableFrom(arguments[index]!.GetType()))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private static async Task<object?> AwaitResultAsync(object? invocation)
    {
        invocation.Should().NotBeNull("the expected operation returns an awaitable result");
        if (invocation is Task task)
        {
            await task.ConfigureAwait(false);
            return task.GetType().GetProperty("Result")?.GetValue(task);
        }

        var asTask = invocation!.GetType().GetMethod("AsTask", BindingFlags.Public | BindingFlags.Instance);
        asTask.Should().NotBeNull($"{invocation.GetType().Name} must be Task or ValueTask");
        var converted = (Task)asTask!.Invoke(invocation, null)!;
        await converted.ConfigureAwait(false);
        return converted.GetType().GetProperty("Result")?.GetValue(converted);
    }
}
