namespace OrcaCore.Abstractions.Primitives;

/// <summary>
/// Represents build-time validation that may accumulate multiple errors.
/// </summary>
public sealed record Validation<T>
{
    private readonly T? value;

    private Validation(T value)
    {
        this.value = value;
        Errors = [];
    }

    private Validation(IReadOnlyList<ValidationError> errors)
    {
        value = default;
        Errors = errors;
    }

    /// <summary>
    /// Gets whether validation succeeded.
    /// </summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    /// Gets the valid value, or throws when validation failed.
    /// </summary>
    public T Value => IsValid
        ? value!
        : throw new InvalidOperationException("An invalid validation result does not contain a value.");

    /// <summary>
    /// Gets validation errors in deterministic discovery order.
    /// </summary>
    public IReadOnlyList<ValidationError> Errors { get; }

    /// <summary>
    /// Creates a valid validation result.
    /// </summary>
    public static Validation<T> Valid(T value)
    {
        return new Validation<T>(value);
    }

    /// <summary>
    /// Creates an invalid validation result with one or more errors.
    /// </summary>
    public static Validation<T> Invalid(IEnumerable<ValidationError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        var collectedErrors = errors.ToArray();
        if (collectedErrors.Length == 0)
        {
            throw new ArgumentException("Invalid validation requires at least one error.", nameof(errors));
        }

        return new Validation<T>(collectedErrors);
    }

    /// <summary>
    /// Combines two validation results, preserving all errors when either side is invalid.
    /// </summary>
    public Validation<TResult> Combine<TOther, TResult>(
        Validation<TOther> other,
        Func<T, TOther, TResult> combine)
    {
        ArgumentNullException.ThrowIfNull(other);
        ArgumentNullException.ThrowIfNull(combine);

        if (IsValid && other.IsValid)
        {
            return Validation<TResult>.Valid(combine(Value, other.Value));
        }

        var errors = new List<ValidationError>(Errors.Count + other.Errors.Count);
        errors.AddRange(Errors);
        errors.AddRange(other.Errors);

        return Validation<TResult>.Invalid(errors);
    }

    /// <summary>
    /// Merges two validation results of the same value type.
    /// </summary>
    public Validation<T> Merge(Validation<T> other, Func<T, T, T> merge)
    {
        return Combine(other, merge);
    }
}
