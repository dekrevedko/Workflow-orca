namespace OrcaCore.Abstractions.Primitives;

/// <summary>
/// Represents a value that may be present without treating absence as failure.
/// </summary>
public readonly record struct Option<T>
{
    private readonly T? value;
    private readonly bool hasValue;

    private Option(T value)
    {
        this.value = value;
        hasValue = true;
    }

    /// <summary>
    /// Gets an absent option.
    /// </summary>
    public static Option<T> None => default;

    /// <summary>
    /// Gets whether the option contains a value.
    /// </summary>
    public bool HasValue => hasValue;

    /// <summary>
    /// Gets the present value, or throws when this option is none.
    /// </summary>
    public T Value => HasValue
        ? value!
        : throw new InvalidOperationException("An empty option does not contain a value.");

    /// <summary>
    /// Creates a present option.
    /// </summary>
    public static Option<T> Some(T value)
    {
        return new Option<T>(value);
    }

    /// <summary>
    /// Transforms a present value while preserving absence.
    /// </summary>
    public Option<TResult> Map<TResult>(Func<T, TResult> map)
    {
        ArgumentNullException.ThrowIfNull(map);

        return HasValue ? Option<TResult>.Some(map(Value)) : Option<TResult>.None;
    }

    /// <summary>
    /// Chains an option-producing operation while preserving absence.
    /// </summary>
    public Option<TResult> Bind<TResult>(Func<T, Option<TResult>> bind)
    {
        ArgumentNullException.ThrowIfNull(bind);

        return HasValue ? bind(Value) : Option<TResult>.None;
    }

    /// <summary>
    /// Projects exactly one branch based on presence or absence.
    /// </summary>
    public TResult Match<TResult>(Func<T, TResult> onSome, Func<TResult> onNone)
    {
        ArgumentNullException.ThrowIfNull(onSome);
        ArgumentNullException.ThrowIfNull(onNone);

        return HasValue ? onSome(Value) : onNone();
    }

    /// <summary>
    /// Returns the present value or the supplied fallback when absent.
    /// </summary>
    public T GetValueOrDefault(T fallback)
    {
        return HasValue ? Value : fallback;
    }
}
