namespace OrcaCore.Abstractions.Primitives;

public readonly struct Option<T> : IEquatable<Option<T>>
{
    private readonly bool _hasValue;
    private readonly T? _value;

    private Option(bool hasValue, T? value)
    {
        _hasValue = hasValue;
        _value = value;
    }

    public bool HasValue => _hasValue;

    public T Value => _hasValue
        ? _value!
        : throw new InvalidOperationException("Option has no value.");

    public static Option<T> Some(T value) => new(true, value);

    public static Option<T> None => new(false, default);

    public Option<TResult> Map<TResult>(Func<T, TResult> mapper) =>
        _hasValue ? Option<TResult>.Some(mapper(_value!)) : Option<TResult>.None;

    public Option<TResult> Bind<TResult>(Func<T, Option<TResult>> binder) =>
        _hasValue ? binder(_value!) : Option<TResult>.None;

    public TResult Match<TResult>(Func<TResult> onNone, Func<T, TResult> onSome) =>
        _hasValue ? onSome(_value!) : onNone();

    public T GetValueOrDefault(T defaultValue = default!) =>
        _hasValue ? _value! : defaultValue;

    public bool Equals(Option<T> other) =>
        _hasValue == other._hasValue && EqualityComparer<T>.Default.Equals(_value, other._value);

    public override bool Equals(object? obj) => obj is Option<T> other && Equals(other);

    public override int GetHashCode() => _hasValue ? HashCode.Combine(true, _value) : HashCode.Combine(false, 0);

    public static bool operator ==(Option<T> left, Option<T> right) => left.Equals(right);

    public static bool operator !=(Option<T> left, Option<T> right) => !left.Equals(right);
}
