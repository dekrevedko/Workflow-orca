using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace OrcaCore.Internal;

internal static class AuthoringKernelProxy
{
    private const string CoreAssemblyName = "OrcaCore.Core";

    internal static object InvokeStatic(
        string typeName,
        string methodName,
        Type[] genericArguments,
        params object?[] arguments)
    {
        var type = LoadCoreAssembly().GetType(typeName, throwOnError: true)!;
        return InvokeCore(type, null, methodName, genericArguments, arguments)!;
    }

    internal static object? Invoke(
        object implementation,
        string methodName,
        Type[] genericArguments,
        params object?[] arguments)
    {
        ArgumentNullException.ThrowIfNull(implementation);
        return InvokeCore(
            implementation.GetType(),
            implementation,
            methodName,
            genericArguments,
            arguments);
    }

    private static object? InvokeCore(
        Type targetType,
        object? target,
        string methodName,
        IReadOnlyList<Type> genericArguments,
        IReadOnlyList<object?> arguments)
    {
        var flags = BindingFlags.Public |
                    (target is null ? BindingFlags.Static : BindingFlags.Instance);
        var matches = new List<(MethodInfo Method, object?[] Arguments)>();

        foreach (var candidate in targetType
                     .GetMethods(flags)
                     .Where(method =>
                         method.Name == methodName &&
                         method.GetGenericArguments().Length == genericArguments.Count &&
                         method.GetParameters().Length == arguments.Count))
        {
            MethodInfo closed;
            try
            {
                closed = candidate.IsGenericMethodDefinition
                    ? candidate.MakeGenericMethod(genericArguments.ToArray())
                    : candidate;
            }
            catch (ArgumentException)
            {
                continue;
            }

            if (TryAdaptArguments(closed.GetParameters(), arguments, out var adapted))
            {
                matches.Add((closed, adapted));
            }
        }

        if (matches.Count != 1)
        {
            throw new MissingMethodException(
                $"The {CoreAssemblyName} authoring kernel exposed {matches.Count} compatible " +
                $"'{targetType.FullName}.{methodName}' overloads; exactly one was required.");
        }

        try
        {
            return matches[0].Method.Invoke(target, matches[0].Arguments);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is { } inner)
        {
            ExceptionDispatchInfo.Capture(inner).Throw();
            throw;
        }
    }

    private static bool TryAdaptArguments(
        IReadOnlyList<ParameterInfo> parameters,
        IReadOnlyList<object?> arguments,
        out object?[] adapted)
    {
        adapted = new object?[arguments.Count];
        for (var index = 0; index < arguments.Count; index++)
        {
            if (!TryAdaptArgument(arguments[index], parameters[index].ParameterType, out adapted[index]))
            {
                adapted = [];
                return false;
            }
        }

        return true;
    }

    private static bool TryAdaptArgument(object? argument, Type targetType, out object? adapted)
    {
        if (argument is null)
        {
            adapted = null;
            return !targetType.IsValueType || Nullable.GetUnderlyingType(targetType) is not null;
        }

        if (targetType.IsInstanceOfType(argument))
        {
            adapted = argument;
            return true;
        }

        if (argument is Delegate callback && typeof(Delegate).IsAssignableFrom(targetType))
        {
            adapted = AdaptDelegate(callback, targetType);
            return adapted is not null;
        }

        adapted = null;
        return false;
    }

    private static Delegate? AdaptDelegate(Delegate source, Type targetType)
    {
        var sourceInvoke = source.GetType().GetMethod("Invoke")!;
        var targetInvoke = targetType.GetMethod("Invoke")!;
        var sourceParameters = sourceInvoke.GetParameters();
        var targetParameters = targetInvoke.GetParameters();
        if (sourceParameters.Length != targetParameters.Length)
        {
            return null;
        }

        var lambdaParameters = targetParameters
            .Select(parameter => Expression.Parameter(parameter.ParameterType, parameter.Name))
            .ToArray();
        var sourceArguments = new Expression[sourceParameters.Length];
        for (var index = 0; index < sourceArguments.Length; index++)
        {
            var sourceType = sourceParameters[index].ParameterType;
            var targetParameter = lambdaParameters[index];
            if (sourceType.IsAssignableFrom(targetParameter.Type))
            {
                sourceArguments[index] = targetParameter;
                continue;
            }

            if (!CanWrap(targetParameter.Type, sourceType))
            {
                return null;
            }

            sourceArguments[index] = Expression.Convert(
                Expression.Call(
                    typeof(AuthoringKernelProxy),
                    nameof(WrapCallbackArgument),
                    Type.EmptyTypes,
                    Expression.Convert(targetParameter, typeof(object)),
                    Expression.Constant(sourceType, typeof(Type))),
                sourceType);
        }

        var invocation = Expression.Invoke(Expression.Constant(source), sourceArguments);
        Expression body;
        if (targetInvoke.ReturnType == typeof(void) && sourceInvoke.ReturnType == typeof(void))
        {
            body = invocation;
        }
        else if (targetInvoke.ReturnType.IsAssignableFrom(sourceInvoke.ReturnType))
        {
            body = invocation;
        }
        else
        {
            return null;
        }

        return Expression.Lambda(targetType, body, lambdaParameters).Compile();
    }

    private static bool CanWrap(Type runtimeType, Type applicationType) =>
        runtimeType.Assembly.GetName().Name == CoreAssemblyName &&
        applicationType.Assembly == typeof(AuthoringKernelProxy).Assembly &&
        string.Equals(runtimeType.Name, applicationType.Name, StringComparison.Ordinal) &&
        applicationType.GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            [typeof(object)],
            modifiers: null) is not null;

    private static object WrapCallbackArgument(object implementation, Type applicationType) =>
        applicationType.GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                [typeof(object)],
                modifiers: null)!
            .Invoke([implementation]);

    private static Assembly LoadCoreAssembly()
    {
        try
        {
            return Assembly.Load(CoreAssemblyName);
        }
        catch (FileNotFoundException exception)
        {
            throw new InvalidOperationException(
                "Workflow authoring requires an explicit OrcaCore engine package. " +
                "Reference OrcaCore.Engine.Ephemeral or OrcaCore.Durable.Hosting before building definitions.",
                exception);
        }
    }
}
