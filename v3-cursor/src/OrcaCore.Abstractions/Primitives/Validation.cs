namespace OrcaCore.Abstractions.Primitives;

/// <summary>
/// Accumulated build-time validation outcome.
/// </summary>
public sealed record Validation<T>
{
    private Validation(T? value, IReadOnlyList<ValidationError> errors, bool isValid)
    {
        Value = value;
        Errors = errors;
        IsValid = isValid;
    }

    public T? Value { get; }

    public IReadOnlyList<ValidationError> Errors { get; }

    public bool IsValid { get; }

    public static Validation<T> Valid(T value) => new(value, [], true);

    public static Validation<T> Invalid(IReadOnlyList<ValidationError> errors) =>
        new(default, errors, false);

    public static Validation<T> Invalid(params ValidationError[] errors) =>
        Invalid((IReadOnlyList<ValidationError>)errors);

    public static Validation<T> Combine(Validation<T> first, Validation<T> second)
    {
        if (first.IsValid && second.IsValid)
        {
            return Valid(second.Value!);
        }

        var merged = new List<ValidationError>(first.Errors.Count + second.Errors.Count);
        merged.AddRange(first.Errors);
        merged.AddRange(second.Errors);
        return Invalid(merged);
    }
}
