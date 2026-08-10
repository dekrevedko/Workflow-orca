namespace OrcaCore.Abstractions.Primitives;

/// <summary>Accumulates internal compiler validation errors without exporting a second validation contract.</summary>
internal sealed record Validation<T>
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

    public bool IsValid => Errors.Count == 0;

    public T Value => IsValid
        ? value!
        : throw new InvalidOperationException("An invalid validation result does not contain a value.");

    public IReadOnlyList<ValidationError> Errors { get; }

    public static Validation<T> Valid(T value) => new(value);

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

    public Validation<T> Merge(Validation<T> other, Func<T, T, T> merge) =>
        Combine(other, merge);
}
