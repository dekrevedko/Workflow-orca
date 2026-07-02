using OrcaCore.Abstractions.Errors;

namespace OrcaCore.Abstractions.Primitives;

/// <summary>
/// Represents an expected success or failure at a contract boundary.
/// </summary>
public readonly record struct Result<T>
{
    private readonly T? value;
    private readonly OrcaCoreException? error;
    private readonly bool isSuccess;

    private Result(T value)
    {
        this.value = value;
        error = null;
        isSuccess = true;
    }

    private Result(OrcaCoreException error)
    {
        value = default;
        this.error = error;
        isSuccess = false;
    }

    /// <summary>
    /// Gets whether the result contains a successful value.
    /// </summary>
    public bool IsSuccess => isSuccess;

    /// <summary>
    /// Gets whether the result contains an expected failure.
    /// </summary>
    public bool IsFailure => !isSuccess;

    /// <summary>
    /// Gets the successful value, or throws when this result is a failure.
    /// </summary>
    public T Value => IsSuccess
        ? value!
        : throw new InvalidOperationException("A failed result does not contain a value.");

    /// <summary>
    /// Gets the expected failure error.
    /// </summary>
    public OrcaCoreException Error => IsFailure
        ? error ?? new OrcaCoreException("The default Result<T> value represents failure without a specific error.")
        : throw new InvalidOperationException("A successful result does not contain an error.");

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static Result<T> Success(T value)
    {
        return new Result<T>(value);
    }

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static Result<T> Failure(OrcaCoreException error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new Result<T>(error);
    }

    /// <summary>
    /// Transforms a successful value while preserving failures.
    /// </summary>
    public Result<TResult> Map<TResult>(Func<T, TResult> map)
    {
        ArgumentNullException.ThrowIfNull(map);

        return IsSuccess ? Result<TResult>.Success(map(Value)) : Result<TResult>.Failure(Error);
    }

    /// <summary>
    /// Chains a result-producing operation while short-circuiting failures.
    /// </summary>
    public Result<TResult> Bind<TResult>(Func<T, Result<TResult>> bind)
    {
        ArgumentNullException.ThrowIfNull(bind);

        return IsSuccess ? bind(Value) : Result<TResult>.Failure(Error);
    }

    /// <summary>
    /// Projects exactly one branch based on success or failure.
    /// </summary>
    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<OrcaCoreException, TResult> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return IsSuccess ? onSuccess(Value) : onFailure(Error);
    }
}
