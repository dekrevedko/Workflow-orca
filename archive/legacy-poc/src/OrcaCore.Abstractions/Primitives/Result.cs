namespace OrcaCore.Abstractions.Primitives;

public readonly struct Result<T> : IEquatable<Result<T>>
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public T? Value { get; }
    public Exception? Error { get; }

    private Result(bool isSuccess, T? value, Exception? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public static Result<T> Success(T value) => new(true, value, null);

    public static Result<T> Failure(Exception error) => new(false, default, error);

    public Result<TResult> Map<TResult>(Func<T, TResult> mapper) =>
        IsSuccess ? Result<TResult>.Success(mapper(Value!)) : Result<TResult>.Failure(Error!);

    public Result<TResult> Bind<TResult>(Func<T, Result<TResult>> binder) =>
        IsSuccess ? binder(Value!) : Result<TResult>.Failure(Error!);

    public TResult Match<TResult>(Func<Exception, TResult> onFailure, Func<T, TResult> onSuccess) =>
        IsSuccess ? onSuccess(Value!) : onFailure(Error!);

    public bool Equals(Result<T> other) =>
        IsSuccess == other.IsSuccess &&
        EqualityComparer<T>.Default.Equals(Value, other.Value) &&
        Equals(Error, other.Error);

    public override bool Equals(object? obj) => obj is Result<T> other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(IsSuccess, Value, Error);

    public static bool operator ==(Result<T> left, Result<T> right) => left.Equals(right);

    public static bool operator !=(Result<T> left, Result<T> right) => !left.Equals(right);
}
