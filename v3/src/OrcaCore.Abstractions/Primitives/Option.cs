namespace OrcaCore.Abstractions.Primitives;

/// <summary>
/// Absence-without-failure. <c>default(Option&lt;T&gt;)</c> behaves as <see cref="None"/>.
/// </summary>
public readonly record struct Option<T>
{
    private readonly T? _value;

    private Option(T? value, bool hasValue)
    {
        _value = value;
        HasValue = hasValue;
    }

    public bool HasValue { get; }

    public T Value => HasValue
        ? _value!
        : throw new InvalidOperationException("Option has no value.");

    public static Option<T> Some(T value) => new(value, hasValue: true);

    public static Option<T> None => new(default, hasValue: false);

    public Option<TResult> Map<TResult>(Func<T, TResult> map) =>
        HasValue ? Option<TResult>.Some(map(Value)) : Option<TResult>.None;

    public Option<TResult> Bind<TResult>(Func<T, Option<TResult>> bind) =>
        HasValue ? bind(Value) : Option<TResult>.None;

    public TResult Match<TResult>(Func<T, TResult> onSome, Func<TResult> onNone) =>
        HasValue ? onSome(Value) : onNone();

    public T GetValueOrDefault(T fallback) => HasValue ? Value : fallback;
}
