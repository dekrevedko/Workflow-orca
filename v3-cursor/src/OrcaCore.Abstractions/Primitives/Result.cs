using OrcaCore.Abstractions.Errors;

namespace OrcaCore.Abstractions.Primitives;

/// <summary>
/// Expected success/failure outcome for contract-level operations (routing, commits, commands).
/// </summary>
public readonly record struct Result<T>
{
    private readonly bool _isSuccess;
    private readonly T? _value;
    private readonly OrcaCoreException? _error;

    private Result(bool isSuccess, T? value, OrcaCoreException? error)
    {
        _isSuccess = isSuccess;
        _value = value;
        _error = error;
    }

    public bool IsSuccess => _isSuccess;

    public bool IsFailure => !_isSuccess;

    public T Value =>
        _isSuccess
            ? _value!
            : throw new InvalidOperationException("Cannot read Value from a failed Result.");

    public OrcaCoreException Error =>
        _isSuccess
            ? throw new InvalidOperationException("Cannot read Error from a successful Result.")
            : _error ?? new OrcaCoreException("Operation failed.");

    public static Result<T> Success(T value) => new(true, value, null);

    public static Result<T> Failure(OrcaCoreException error) => new(false, default, error);

    public Result<TOut> Map<TOut>(Func<T, TOut> map) =>
        _isSuccess ? Result<TOut>.Success(map(_value!)) : Result<TOut>.Failure(Error);

    public Result<TOut> Bind<TOut>(Func<T, Result<TOut>> bind) =>
        _isSuccess ? bind(_value!) : Result<TOut>.Failure(Error);

    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<OrcaCoreException, TOut> onFailure) =>
        _isSuccess ? onSuccess(_value!) : onFailure(Error);
}
