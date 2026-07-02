using OrcaCore.Abstractions.Errors;

namespace OrcaCore.Abstractions.Primitives;

/// <summary>
/// An expected success/failure outcome. <c>default(Result&lt;T&gt;)</c> is a failure — there
/// is no torn "success with null" state.
/// </summary>
public readonly record struct Result<T>
{
    private readonly T? _value;
    private readonly OrcaCoreException? _error;

    private Result(T? value, OrcaCoreException? error, bool isSuccess)
    {
        _value = value;
        _error = error;
        IsSuccess = isSuccess;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Result is a failure; there is no value.");

    public OrcaCoreException Error => IsSuccess
        ? throw new InvalidOperationException("Result is a success; there is no error.")
        : _error ?? new OrcaCoreException("Result is a default failure with no explicit error.");

    public static Result<T> Success(T value) => new(value, null, isSuccess: true);

    public static Result<T> Failure(OrcaCoreException error) =>
        new(default, error ?? throw new ArgumentNullException(nameof(error)), isSuccess: false);

    public Result<TResult> Map<TResult>(Func<T, TResult> map) =>
        IsSuccess ? Result<TResult>.Success(map(Value)) : Result<TResult>.Failure(Error);

    public Result<TResult> Bind<TResult>(Func<T, Result<TResult>> bind) =>
        IsSuccess ? bind(Value) : Result<TResult>.Failure(Error);

    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<OrcaCoreException, TResult> onFailure) =>
        IsSuccess ? onSuccess(Value) : onFailure(Error);
}
