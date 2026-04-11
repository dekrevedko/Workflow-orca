namespace OrcaCore.Abstractions.Primitives;

public readonly record struct Validation<T>(T? Value, IReadOnlyList<ValidationError> Errors)
{
    public bool IsValid => Errors.Count == 0;

    public static Validation<T> Valid(T value) => new(value, []);

    public static Validation<T> Invalid(params ValidationError[] errors) => new(default, errors);

    public static Validation<T> Invalid(IReadOnlyList<ValidationError> errors) => new(default, errors);

    /// <summary>Merges validation outcomes: all errors are concatenated; valid only if both are valid. When both valid, <paramref name="second"/>'s value is kept.</summary>
    public static Validation<T> Merge(Validation<T> first, Validation<T> second)
    {
        if (first.IsValid && second.IsValid)
        {
            if (EqualityComparer<T>.Default.Equals(first.Value, second.Value))
                return Valid(first.Value!);

            return Invalid(new ValidationError(
                "VALIDATION_MERGE_CONFLICT",
                "Cannot merge two valid validation values with different payloads."));
        }

        var list = new List<ValidationError>(first.Errors.Count + second.Errors.Count);
        list.AddRange(first.Errors);
        list.AddRange(second.Errors);
        return Invalid(list);
    }

    public Validation<TResult> Map<TResult>(Func<T, TResult> mapper)
    {
        if (!IsValid)
            return Validation<TResult>.Invalid([.. Errors]);

        return Validation<TResult>.Valid(mapper(Value!));
    }

    public Validation<TResult> Bind<TResult>(Func<T, Validation<TResult>> binder)
    {
        if (!IsValid)
            return Validation<TResult>.Invalid([.. Errors]);

        return binder(Value!);
    }

    public TResult Match<TResult>(Func<IReadOnlyList<ValidationError>, TResult> onInvalid, Func<T, TResult> onValid) =>
        IsValid ? onValid(Value!) : onInvalid(Errors);
}
