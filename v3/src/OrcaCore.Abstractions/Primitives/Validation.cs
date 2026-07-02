namespace OrcaCore.Abstractions.Primitives;

/// <summary>
/// Accumulated build/authoring-time errors — never used for runtime operational failures.
/// </summary>
public sealed record Validation<T>
{
    private static readonly IReadOnlyList<ValidationError> NoErrors = [];

    private Validation(T? value, IReadOnlyList<ValidationError> errors)
    {
        Value = value;
        Errors = errors;
    }

    public T? Value { get; }

    public IReadOnlyList<ValidationError> Errors { get; }

    public bool IsValid => Errors.Count == 0;

    public static Validation<T> Valid(T value) => new(value, NoErrors);

    public static Validation<T> Invalid(IReadOnlyList<ValidationError> errors) =>
        new(default, [.. errors]);

    public Validation<T> Combine(Validation<T> other)
    {
        if (IsValid && other.IsValid)
        {
            return this;
        }

        return Invalid([.. Errors, .. other.Errors]);
    }
}
