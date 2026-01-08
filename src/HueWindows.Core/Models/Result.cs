namespace HueWindows.Core.Models;

/// <summary>
/// Represents the result of an operation that can succeed or fail.
/// </summary>
/// <typeparam name="T">The type of the value on success.</typeparam>
public readonly struct Result<T>
{
    /// <summary>
    /// Gets whether the operation succeeded.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets whether the operation failed.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Gets the value if the operation succeeded.
    /// </summary>
    public T? Value { get; }

    /// <summary>
    /// Gets the error message if the operation failed.
    /// </summary>
    public string? Error { get; }

    private Result(bool isSuccess, T? value, string? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    /// <summary>
    /// Creates a successful result with the specified value.
    /// </summary>
    public static Result<T> Success(T value) => new(true, value, null);

    /// <summary>
    /// Creates a failed result with the specified error message.
    /// </summary>
    public static Result<T> Failure(string error) => new(false, default, error);

    /// <summary>
    /// Gets the value or a default if the operation failed.
    /// </summary>
    public T? GetValueOrDefault(T? defaultValue = default) => IsSuccess ? Value : defaultValue;

    /// <summary>
    /// Implicit conversion to bool for easy success checking.
    /// </summary>
    public static implicit operator bool(Result<T> result) => result.IsSuccess;
}

/// <summary>
/// Represents the result of an operation without a return value.
/// </summary>
public readonly struct Result
{
    /// <summary>
    /// Gets whether the operation succeeded.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets whether the operation failed.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Gets the error message if the operation failed.
    /// </summary>
    public string? Error { get; }

    private Result(bool isSuccess, string? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static Result Success() => new(true, null);

    /// <summary>
    /// Creates a failed result with the specified error message.
    /// </summary>
    public static Result Failure(string error) => new(false, error);

    /// <summary>
    /// Implicit conversion to bool for easy success checking.
    /// </summary>
    public static implicit operator bool(Result result) => result.IsSuccess;
}
