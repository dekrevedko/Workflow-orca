namespace OrcaCore.Abstractions.Primitives;

/// <summary>
/// Present/absent value without representing failure.
/// </summary>
public readonly record struct Option<T>
{
    private readonly bool _hasValue;
    private readonly T? _value;

    private Option(bool hasValue, T? value)
    {
        _hasValue = hasValue;
        _value = value;
    }

    public bool HasValue => _hasValue;

    public T Value =>
        _hasValue
            ? _value!
            : throw new InvalidOperationException("Cannot read Value from None.");

    public static Option<T> Some(T value) => new(true, value);

    public static Option<T> None() => default;

    public Option<TOut> Map<TOut>(Func<T, TOut> map) =>
        _hasValue ? Option<TOut>.Some(map(_value!)) : Option<TOut>.None();

    public Option<TOut> Bind<TOut>(Func<T, Option<TOut>> bind) =>
        _hasValue ? bind(_value!) : Option<TOut>.None();

    public TOut Match<TOut>(Func<T, TOut> onSome, Func<TOut> onNone) =>
        _hasValue ? onSome(_value!) : onNone();

    public T GetValueOrDefault(T fallback) => _hasValue ? _value! : fallback;
}
