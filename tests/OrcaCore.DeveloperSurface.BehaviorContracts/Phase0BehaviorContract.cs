using System.Diagnostics;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace OrcaCore.DeveloperSurface.BehaviorContracts;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class Phase0ScenarioAttribute(string id, string taskId) : Attribute
{
    public string Id { get; } = id;
    public string TaskId { get; } = taskId;
}

[Flags]
public enum Phase0DeterministicRequirement
{
    None = 0,
    Time = 1,
    Barrier = 2,
    TimeAndBarrier = Time | Barrier
}

public interface IPhase0DeterministicBarrier
{
    ValueTask ReachAsync(string name, CancellationToken cancellationToken = default);
}

public sealed class Phase0ScenarioServices
{
    internal Phase0ScenarioServices(TimeProvider timeProvider, IPhase0DeterministicBarrier barrier)
    {
        TimeProvider = timeProvider;
        Barrier = barrier;
    }

    public TimeProvider TimeProvider { get; }
    public IPhase0DeterministicBarrier Barrier { get; }
}

public sealed class Phase0Observation<T>
{
    private readonly Phase0ScenarioContext _owner;

    internal Phase0Observation(Phase0ScenarioContext owner, string callId, T value)
    {
        _owner = owner;
        CallId = callId;
        Value = value;
    }

    internal string CallId { get; }
    internal T Value { get; }
    internal void MarkAsserted() => _owner.MarkAsserted(this);
}

public readonly record struct Phase0Completion;

internal sealed record Phase0ExpectedCall(
    string Id,
    string Assembly,
    string Type,
    string Member,
    int GenericArity,
    IReadOnlyList<string> Parameters,
    string ReturnType);

// Every observation API accepts an expression tree, not a Func. The expression body must be one
// direct method/property/constructor call whose complete runtime signature matches a frozen row.
// The context compiles and invokes that exact expression itself, records the execution, and mints
// the only observation that Phase0Assert can consume. Blocks, wrappers, uninvoked lambdas, stored
// results, and unrelated return values therefore never enter the certified execution path.
public sealed class Phase0ScenarioContext
{
    private readonly IReadOnlyList<Phase0ExpectedCall> _expectedCalls;
    private readonly HashSet<string> _productAssemblies;
    private readonly HashSet<string> _harnessBoundaryAssemblies;
    private readonly HashSet<string> _executedCalls = new(StringComparer.Ordinal);
    private readonly AsyncLocal<int> _activeObservationDepth = new();
    private readonly Phase0DeterministicRequirement _requirement;
    private readonly HashSet<object> _observations = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<object> _asserted = new(ReferenceEqualityComparer.Instance);
    private readonly TrackingTimeProvider _timeProvider;
    private readonly TrackingBarrier _barrier;
    private readonly ConcurrentQueue<string> _productConsumptionFrames = new();

    internal Phase0ScenarioContext(
        IReadOnlyList<Phase0ExpectedCall> expectedCalls,
        IEnumerable<string> productAssemblies,
        IEnumerable<string> harnessBoundaryAssemblies,
        Phase0DeterministicRequirement requirement)
    {
        _expectedCalls = expectedCalls;
        _productAssemblies = productAssemblies.ToHashSet(StringComparer.Ordinal);
        _harnessBoundaryAssemblies = harnessBoundaryAssemblies.ToHashSet(StringComparer.Ordinal);
        _requirement = requirement;
        _timeProvider = new TrackingTimeProvider(this);
        _barrier = new TrackingBarrier(this);
        Services = new Phase0ScenarioServices(_timeProvider, _barrier);
    }

    public Phase0ScenarioServices Services { get; }
    internal IReadOnlyCollection<string> ProductConsumptionFrames => _productConsumptionFrames.ToArray();

    public Phase0Observation<T> Observe<T>(Expression<Func<Phase0ScenarioServices, T>> call)
    {
        ArgumentNullException.ThrowIfNull(call);
        var expected = ResolveExpectedCall(call);
        EnterObservedCall();
        try
        {
            var value = call.Compile()(Services);
            return CreateObservation(expected, value);
        }
        finally
        {
            ExitObservedCall();
        }
    }

    public Phase0Observation<Phase0Completion> Observe(
        Expression<Action<Phase0ScenarioServices>> call)
    {
        ArgumentNullException.ThrowIfNull(call);
        var expected = ResolveExpectedCall(call);
        EnterObservedCall();
        try
        {
            call.Compile()(Services);
            return CreateObservation(expected, new Phase0Completion());
        }
        finally
        {
            ExitObservedCall();
        }
    }

    public async ValueTask<Phase0Observation<T>> ObserveAsync<T>(
        Expression<Func<Phase0ScenarioServices, ValueTask<T>>> call)
    {
        ArgumentNullException.ThrowIfNull(call);
        var expected = ResolveExpectedCall(call);
        EnterObservedCall();
        try
        {
            var pending = call.Compile()(Services);
            _executedCalls.Add(expected.Id);
            var value = await pending;
            return CreateObservationAfterExecution(expected, value);
        }
        finally
        {
            ExitObservedCall();
        }
    }

    public async ValueTask<Phase0Observation<Phase0Completion>> ObserveAsync(
        Expression<Func<Phase0ScenarioServices, ValueTask>> call)
    {
        ArgumentNullException.ThrowIfNull(call);
        var expected = ResolveExpectedCall(call);
        EnterObservedCall();
        try
        {
            var pending = call.Compile()(Services);
            _executedCalls.Add(expected.Id);
            await pending;
            return CreateObservationAfterExecution(expected, new Phase0Completion());
        }
        finally
        {
            ExitObservedCall();
        }
    }

    public async ValueTask<Phase0Observation<T>> ObserveTaskAsync<T>(
        Expression<Func<Phase0ScenarioServices, Task<T>>> call)
    {
        ArgumentNullException.ThrowIfNull(call);
        var expected = ResolveExpectedCall(call);
        EnterObservedCall();
        try
        {
            var pending = call.Compile()(Services);
            _executedCalls.Add(expected.Id);
            var value = await pending;
            return CreateObservationAfterExecution(expected, value);
        }
        finally
        {
            ExitObservedCall();
        }
    }

    public async ValueTask<Phase0Observation<Phase0Completion>> ObserveTaskAsync(
        Expression<Func<Phase0ScenarioServices, Task>> call)
    {
        ArgumentNullException.ThrowIfNull(call);
        var expected = ResolveExpectedCall(call);
        EnterObservedCall();
        try
        {
            var pending = call.Compile()(Services);
            _executedCalls.Add(expected.Id);
            await pending;
            return CreateObservationAfterExecution(expected, new Phase0Completion());
        }
        finally
        {
            ExitObservedCall();
        }
    }

    public async ValueTask<Phase0Observation<TException>> ObserveThrowsAsync<TException>(
        Expression<Func<Phase0ScenarioServices, ValueTask>> call)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(call);
        var expected = ResolveExpectedCall(call);
        ValueTask pending;
        EnterObservedCall();
        try
        {
            pending = call.Compile()(Services);
            _executedCalls.Add(expected.Id);
            await pending;
        }
        catch (TException exception) when (_executedCalls.Contains(expected.Id) ||
                                           ExceptionCameFromExpectedProduct(exception, expected))
        {
            _executedCalls.Add(expected.Id);
            return CreateObservationAfterExecution(expected, exception);
        }
        finally
        {
            ExitObservedCall();
        }

        throw new InvalidOperationException($"Expected {typeof(TException).FullName} was not thrown.");
    }

    public async ValueTask<Phase0Observation<TException>> ObserveThrowsAsync<TException, TResult>(
        Expression<Func<Phase0ScenarioServices, ValueTask<TResult>>> call)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(call);
        var expected = ResolveExpectedCall(call);
        ValueTask<TResult> pending;
        EnterObservedCall();
        try
        {
            pending = call.Compile()(Services);
            _executedCalls.Add(expected.Id);
            _ = await pending;
        }
        catch (TException exception) when (_executedCalls.Contains(expected.Id) ||
                                           ExceptionCameFromExpectedProduct(exception, expected))
        {
            _executedCalls.Add(expected.Id);
            return CreateObservationAfterExecution(expected, exception);
        }
        finally
        {
            ExitObservedCall();
        }

        throw new InvalidOperationException($"Expected {typeof(TException).FullName} was not thrown.");
    }

    public Phase0Observation<TException> ObserveThrows<TException, TResult>(
        Expression<Func<Phase0ScenarioServices, TResult>> call)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(call);
        var expected = ResolveExpectedCall(call);
        EnterObservedCall();
        try
        {
            _ = call.Compile()(Services);
        }
        catch (TException exception) when (ExceptionCameFromExpectedProduct(exception, expected))
        {
            _executedCalls.Add(expected.Id);
            return CreateObservationAfterExecution(expected, exception);
        }
        finally
        {
            ExitObservedCall();
        }

        throw new InvalidOperationException($"Expected {typeof(TException).FullName} was not thrown.");
    }

    public Phase0Observation<TException> ObserveThrows<TException>(
        Expression<Action<Phase0ScenarioServices>> call)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(call);
        var expected = ResolveExpectedCall(call);
        EnterObservedCall();
        try
        {
            call.Compile()(Services);
        }
        catch (TException exception) when (ExceptionCameFromExpectedProduct(exception, expected))
        {
            _executedCalls.Add(expected.Id);
            return CreateObservationAfterExecution(expected, exception);
        }
        finally
        {
            ExitObservedCall();
        }

        throw new InvalidOperationException($"Expected {typeof(TException).FullName} was not thrown.");
    }

    public async ValueTask<Phase0Observation<TException>> ObserveTaskThrowsAsync<TException>(
        Expression<Func<Phase0ScenarioServices, Task>> call)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(call);
        var expected = ResolveExpectedCall(call);
        EnterObservedCall();
        try
        {
            var pending = call.Compile()(Services);
            _executedCalls.Add(expected.Id);
            await pending;
        }
        catch (TException exception) when (_executedCalls.Contains(expected.Id) ||
                                           ExceptionCameFromExpectedProduct(exception, expected))
        {
            _executedCalls.Add(expected.Id);
            return CreateObservationAfterExecution(expected, exception);
        }
        finally
        {
            ExitObservedCall();
        }

        throw new InvalidOperationException($"Expected {typeof(TException).FullName} was not thrown.");
    }

    public async ValueTask<Phase0Observation<TException>> ObserveTaskThrowsAsync<TException, TResult>(
        Expression<Func<Phase0ScenarioServices, Task<TResult>>> call)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(call);
        var expected = ResolveExpectedCall(call);
        EnterObservedCall();
        try
        {
            var pending = call.Compile()(Services);
            _executedCalls.Add(expected.Id);
            _ = await pending;
        }
        catch (TException exception) when (_executedCalls.Contains(expected.Id) ||
                                           ExceptionCameFromExpectedProduct(exception, expected))
        {
            _executedCalls.Add(expected.Id);
            return CreateObservationAfterExecution(expected, exception);
        }
        finally
        {
            ExitObservedCall();
        }

        throw new InvalidOperationException($"Expected {typeof(TException).FullName} was not thrown.");
    }

    public void AdvanceTimeBy(TimeSpan amount) => _timeProvider.Advance(amount);

    public ValueTask WaitUntilBarrierReachedAsync(
        string name,
        CancellationToken cancellationToken = default) =>
        _barrier.WaitUntilReachedAsync(name, cancellationToken);

    public void ReleaseBarrier(string name) => _barrier.Release(name);

    internal IReadOnlyList<string> CertificationErrors()
    {
        var errors = new List<string>();
        foreach (var expected in _expectedCalls.Where(call => !_executedCalls.Contains(call.Id)))
            errors.Add($"required exact call {expected.Id} did not execute through the guard-owned observation context");
        if (_observations.Count == 0)
            errors.Add("scenario did not produce a guard-owned observation");
        if (_observations.Any(observation => !_asserted.Contains(observation)))
            errors.Add("every guard-owned observation must be consumed by Phase0Assert");
        if ((_requirement & Phase0DeterministicRequirement.Time) != 0 && !_timeProvider.ProductConsumed)
            errors.Add("the required product path did not consume the guard-owned deterministic TimeProvider");
        if ((_requirement & Phase0DeterministicRequirement.Barrier) != 0 && !_barrier.ProductConsumed)
            errors.Add("the required product path did not consume the guard-owned deterministic barrier");
        return errors;
    }

    internal void MarkAsserted(object observation)
    {
        if (!_observations.Contains(observation))
            throw new InvalidOperationException("The observation was not minted by this scenario context.");
        _asserted.Add(observation);
    }

    internal static string CanonicalTypeName(Type type)
    {
        if (type.IsByRef) return $"{CanonicalTypeName(type.GetElementType()!)}&";
        if (type.IsArray) return $"{CanonicalTypeName(type.GetElementType()!)}[]";
        if (type.IsGenericParameter)
            return $"{(type.DeclaringMethod is null ? "!" : "!!")}{type.GenericParameterPosition}";
        if (!type.IsGenericType) return type.FullName ?? type.Name;
        var definition = type.GetGenericTypeDefinition();
        return $"{definition.FullName}[{string.Join(",", type.GetGenericArguments().Select(CanonicalTypeName))}]";
    }

    private Phase0ExpectedCall ResolveExpectedCall(LambdaExpression expression)
    {
        var method = DirectCalledMethod(expression.Body) ?? throw new InvalidOperationException(
            "The observed expression body must be one direct method, property, or constructor call; blocks, wrappers, stored results, and unrelated returns are forbidden.");
        method = NormalizeMethod(method);
        var matches = _expectedCalls.Where(expected => SignatureMatches(method, expected)).ToArray();
        return matches.Length switch
        {
            1 => matches[0],
            0 => throw new InvalidOperationException($"Observed call {Display(method)} does not match any required exact signature."),
            _ => throw new InvalidOperationException($"Observed call {Display(method)} ambiguously matches more than one required exact signature.")
        };
    }

    private Phase0Observation<T> CreateObservation<T>(Phase0ExpectedCall expected, T value)
    {
        _executedCalls.Add(expected.Id);
        return CreateObservationAfterExecution(expected, value);
    }

    private Phase0Observation<T> CreateObservationAfterExecution<T>(Phase0ExpectedCall expected, T value)
    {
        var observation = new Phase0Observation<T>(this, expected.Id, value);
        _observations.Add(observation);
        return observation;
    }

    private static MethodBase? DirectCalledMethod(Expression body)
    {
        while (body is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
            body = unary.Operand;
        return body switch
        {
            MethodCallExpression call => call.Method,
            MemberExpression { Member: PropertyInfo property } => property.GetMethod,
            NewExpression created => created.Constructor,
            _ => null
        };
    }

    private static MethodBase NormalizeMethod(MethodBase method)
    {
        if (method is MethodInfo { IsGenericMethod: true } generic)
            method = generic.GetGenericMethodDefinition();
        if (method.DeclaringType?.IsConstructedGenericType == true)
        {
            var definition = method.DeclaringType.GetGenericTypeDefinition();
            method = definition.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                                           BindingFlags.Static)
                .OfType<MethodBase>()
                .Single(candidate => candidate.MetadataToken == method.MetadataToken);
        }
        return method;
    }

    private static bool SignatureMatches(MethodBase method, Phase0ExpectedCall expected)
    {
        var returnType = method is MethodInfo info ? CanonicalTypeName(info.ReturnType) : "System.Void";
        return method.DeclaringType?.Assembly.GetName().Name == expected.Assembly &&
               method.DeclaringType.FullName == expected.Type &&
               method.Name == expected.Member &&
               (method is MethodInfo { IsGenericMethodDefinition: true } generic ? generic.GetGenericArguments().Length : 0) ==
               expected.GenericArity &&
               method.GetParameters().Select(parameter => CanonicalTypeName(parameter.ParameterType))
                   .SequenceEqual(expected.Parameters, StringComparer.Ordinal) &&
               returnType == expected.ReturnType;
    }

    private static string Display(MethodBase method) =>
        $"{method.DeclaringType?.Assembly.GetName().Name}:{method.DeclaringType?.FullName}::{method.Name}`" +
        $"{(method is MethodInfo { IsGenericMethodDefinition: true } generic ? generic.GetGenericArguments().Length : 0)}" +
        $"({string.Join(",", method.GetParameters().Select(parameter => CanonicalTypeName(parameter.ParameterType)))}) -> " +
        $"{(method is MethodInfo info ? CanonicalTypeName(info.ReturnType) : "System.Void")}";

    private static bool ExceptionCameFromExpectedProduct(Exception exception, Phase0ExpectedCall expected)
    {
        var methods = new StackTrace(exception, false).GetFrames()
            .Select(frame => frame.GetMethod())
            .Where(method => method is not null)
            .ToArray();
        return methods.Any(method =>
                method!.DeclaringType?.Assembly.GetName().Name == expected.Assembly &&
                method.DeclaringType.FullName == expected.Type &&
                method.Name == expected.Member);
    }

    private void EnterObservedCall() => _activeObservationDepth.Value++;
    private void ExitObservedCall() => _activeObservationDepth.Value--;

    private bool IsProductFramePresent()
    {
        foreach (var method in new StackTrace().GetFrames().Select(frame => frame.GetMethod()))
        {
            var assembly = method?.DeclaringType?.Assembly.GetName().Name;
            if (assembly is null) continue;
            if (_productAssemblies.Contains(assembly))
            {
                _productConsumptionFrames.Enqueue(
                    $"{assembly}:{method!.DeclaringType!.FullName}::{method.Name}");
                return true;
            }

            if (_harnessBoundaryAssemblies.Contains(assembly)) return false;
        }

        return false;
    }

    private sealed class TrackingTimeProvider(Phase0ScenarioContext owner) : TimeProvider
    {
        private DateTimeOffset _utcNow = new(2040, 1, 1, 0, 0, 0, TimeSpan.Zero);
        private long _timestamp;
        private readonly List<TrackingTimer> _timers = [];
        internal bool ProductConsumed { get; private set; }

        public override DateTimeOffset GetUtcNow()
        {
            if (owner.IsProductFramePresent()) ProductConsumed = true;
            return _utcNow;
        }

        public override long GetTimestamp()
        {
            if (owner.IsProductFramePresent()) ProductConsumed = true;
            return _timestamp;
        }

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            ArgumentNullException.ThrowIfNull(callback);
            var timer = new TrackingTimer(this, callback, state);
            lock (_timers)
            {
                _timers.Add(timer);
            }

            timer.Change(dueTime, period);
            return timer;
        }

        internal void Advance(TimeSpan amount)
        {
            if (amount < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(amount));
            _utcNow += amount;
            _timestamp += amount.Ticks;

            TrackingTimer[] due;
            lock (_timers)
            {
                due = _timers.Where(timer => timer.TakeIfDue(_utcNow)).ToArray();
            }

            foreach (var timer in due)
            {
                timer.Fire();
            }
        }

        private void Remove(TrackingTimer timer)
        {
            lock (_timers)
            {
                _timers.Remove(timer);
            }
        }

        private sealed class TrackingTimer(
            TrackingTimeProvider owner,
            TimerCallback callback,
            object? state) : ITimer
        {
            private readonly object _gate = new();
            private DateTimeOffset? _dueAt;
            private TimeSpan _period = Timeout.InfiniteTimeSpan;
            private bool _disposed;

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                lock (_gate)
                {
                    if (_disposed)
                    {
                        return false;
                    }

                    _dueAt = dueTime == Timeout.InfiniteTimeSpan
                        ? null
                        : owner._utcNow.Add(dueTime);
                    _period = period;
                    return true;
                }
            }

            internal bool TakeIfDue(DateTimeOffset now)
            {
                lock (_gate)
                {
                    if (_disposed || _dueAt is not { } dueAt || dueAt > now)
                    {
                        return false;
                    }

                    _dueAt = _period > TimeSpan.Zero && _period != Timeout.InfiniteTimeSpan
                        ? dueAt.Add(_period)
                        : null;
                    return true;
                }
            }

            internal void Fire() => callback(state);

            public void Dispose()
            {
                lock (_gate)
                {
                    if (_disposed)
                    {
                        return;
                    }

                    _disposed = true;
                    _dueAt = null;
                }

                owner.Remove(this);
            }

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }

    private sealed class TrackingBarrier(Phase0ScenarioContext owner) : IPhase0DeterministicBarrier
    {
        private readonly Dictionary<string, BarrierState> _states = new(StringComparer.Ordinal);
        internal bool ProductConsumed { get; private set; }

        public ValueTask ReachAsync(string name, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            BarrierState state;
            lock (_states)
            {
                if (!_states.TryGetValue(name, out state!)) _states.Add(name, state = new BarrierState());
                state.Reached.TrySetResult();
            }
            if (owner.IsProductFramePresent()) ProductConsumed = true;
            return new ValueTask(state.Released.Task.WaitAsync(cancellationToken));
        }

        internal ValueTask WaitUntilReachedAsync(string name, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            BarrierState state;
            lock (_states)
            {
                if (!_states.TryGetValue(name, out state!)) _states.Add(name, state = new BarrierState());
            }
            return new ValueTask(state.Reached.Task.WaitAsync(cancellationToken));
        }

        internal void Release(string name)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            BarrierState state;
            lock (_states)
            {
                if (!_states.TryGetValue(name, out state!)) _states.Add(name, state = new BarrierState());
            }
            state.Released.TrySetResult();
        }

        private sealed class BarrierState
        {
            internal TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            internal TaskCompletionSource Released { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }
}

public static class Phase0Assert
{
    public static void True(Phase0Observation<bool> observation, string message)
    {
        ArgumentNullException.ThrowIfNull(observation);
        observation.MarkAsserted();
        if (!observation.Value) throw new InvalidOperationException(message);
    }

    public static void Equal<T>(T expected, Phase0Observation<T> observation, string message)
    {
        ArgumentNullException.ThrowIfNull(observation);
        observation.MarkAsserted();
        if (!EqualityComparer<T>.Default.Equals(expected, observation.Value))
            throw new InvalidOperationException($"{message} Expected: {expected}; actual: {observation.Value}.");
    }

    public static void Satisfies<T>(Phase0Observation<T> observation, Func<T, bool> predicate, string message)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(predicate);
        observation.MarkAsserted();
        if (!predicate(observation.Value)) throw new InvalidOperationException(message);
    }

    public static void Completed(Phase0Observation<Phase0Completion> observation, string message)
    {
        ArgumentNullException.ThrowIfNull(observation);
        _ = message;
        observation.MarkAsserted();
    }
}

internal static class Phase0MutationProbe
{
    internal static string RequiredProductOperation() => "observed";
    internal static string TrivialProductOperation() => "unrelated";
    internal static DateTimeOffset IgnoreInjectedClock(TimeProvider _) => TimeProvider.System.GetUtcNow();
    internal static ValueTask IgnoreInjectedBarrierAsync(IPhase0DeterministicBarrier _) => ValueTask.CompletedTask;

    internal static async ValueTask<string> ConsumeDeterminismAsync(Phase0ScenarioServices services)
    {
        _ = services.TimeProvider.GetUtcNow();
        await services.Barrier.ReachAsync("mutation");
        return RequiredProductOperation();
    }

    internal static ValueTask ThrowAsync(Phase0ScenarioServices _) =>
        ValueTask.FromException(new InvalidOperationException("mutation probe"));

    internal static string ThrowSyncValue() => throw new InvalidOperationException("sync value probe");
    internal static void ThrowSyncVoid() => throw new InvalidOperationException("sync void probe");
    internal static ValueTask<string> ThrowValueTaskOfTAsync() =>
        ValueTask.FromException<string>(new InvalidOperationException("valuetask value probe"));
    internal static Task ThrowTaskAsync() => Task.FromException(new InvalidOperationException("task probe"));
    internal static Task<string> ThrowTaskOfTAsync() =>
        Task.FromException<string>(new InvalidOperationException("task value probe"));
}
