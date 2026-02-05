using System;

namespace NetworkConfigApp.Core.Models
{
    /// <summary>
    /// Generic result type for operations that can succeed or fail.
    /// Provides a functional approach to error handling without exceptions.
    ///
    /// Algorithm: Railway-oriented programming pattern for error handling.
    /// Data Structure: Discriminated union-like structure (Success OR Failure).
    /// Security: Error messages are sanitized to avoid leaking sensitive info.
    /// </summary>
    /// <typeparam name="T">Type of the success value.</typeparam>
    public sealed class Result<T>
    {
        /// <summary>True if operation succeeded.</summary>
        public bool IsSuccess { get; }

        /// <summary>True if operation failed.</summary>
        public bool IsFailure => !IsSuccess;

        /// <summary>The success value (only valid if IsSuccess is true).</summary>
        public T Value { get; }

        /// <summary>Error message (only valid if IsFailure is true).</summary>
        public string Error { get; }

        /// <summary>Error code for categorization (only valid if IsFailure is true).</summary>
        public ErrorCode ErrorCode { get; }

        private Result(bool isSuccess, T value, string error, ErrorCode errorCode)
        {
            IsSuccess = isSuccess;
            Value = value;
            Error = error ?? string.Empty;
            ErrorCode = errorCode;
        }

        /// <summary>Creates a successful result.</summary>
        public static Result<T> Success(T value)
        {
            return new Result<T>(true, value, null, ErrorCode.None);
        }

        /// <summary>Creates a failed result.</summary>
        public static Result<T> Failure(string error, ErrorCode code = ErrorCode.Unknown)
        {
            return new Result<T>(false, default, error, code);
        }

        /// <summary>Creates a failed result from an exception.</summary>
        public static Result<T> FromException(Exception ex, string context = "")
        {
            var code = CategorizeException(ex);
            var message = string.IsNullOrEmpty(context)
                ? ex.Message
                : $"{context}: {ex.Message}";

            return new Result<T>(false, default, message, code);
        }

        /// <summary>
        /// Maps the success value to a new type.
        /// </summary>
        public Result<TNew> Map<TNew>(Func<T, TNew> mapper)
        {
            if (IsSuccess)
            {
                try
                {
                    return Result<TNew>.Success(mapper(Value));
                }
                catch (Exception ex)
                {
                    return Result<TNew>.FromException(ex);
                }
            }
            return Result<TNew>.Failure(Error, ErrorCode);
        }

        /// <summary>
        /// Chains another operation if this one succeeded.
        /// </summary>
        public Result<TNew> Bind<TNew>(Func<T, Result<TNew>> binder)
        {
            if (IsSuccess)
            {
                try
                {
                    return binder(Value);
                }
                catch (Exception ex)
                {
                    return Result<TNew>.FromException(ex);
                }
            }
            return Result<TNew>.Failure(Error, ErrorCode);
        }

        /// <summary>
        /// Gets the value or a default if failed.
        /// </summary>
        public T GetValueOrDefault(T defaultValue = default)
        {
            return IsSuccess ? Value : defaultValue;
        }

        /// <summary>
        /// Executes an action if successful.
        /// </summary>
        public Result<T> OnSuccess(Action<T> action)
        {
            if (IsSuccess)
            {
                try
                {
                    action(Value);
                }
                catch
                {
                    // Swallow action exceptions to maintain fluent pattern
                }
            }
            return this;
        }

        /// <summary>
        /// Executes an action if failed.
        /// </summary>
        public Result<T> OnFailure(Action<string, ErrorCode> action)
        {
            if (IsFailure)
            {
                try
                {
                    action(Error, ErrorCode);
                }
                catch
                {
                    // Swallow action exceptions to maintain fluent pattern
                }
            }
            return this;
        }

        private static ErrorCode CategorizeException(Exception ex)
        {
            switch (ex)
            {
                case UnauthorizedAccessException _:
                    return ErrorCode.AccessDenied;
                case System.Net.Sockets.SocketException _:
                    return ErrorCode.NetworkError;
                case TimeoutException _:
                    return ErrorCode.Timeout;
                case ArgumentException _:
                    return ErrorCode.InvalidInput;
                case InvalidOperationException _:
                    return ErrorCode.InvalidOperation;
                case System.IO.IOException _:
                    return ErrorCode.IoError;
                default:
                    return ErrorCode.Unknown;
            }
        }

        public override string ToString()
        {
            return IsSuccess
                ? $"Success: {Value}"
                : $"Failure ({ErrorCode}): {Error}";
        }
    }

    /// <summary>
    /// Non-generic result for operations without return value.
    /// </summary>
    public sealed class Result
    {
        public bool IsSuccess { get; }
        public bool IsFailure => !IsSuccess;
        public string Error { get; }
        public ErrorCode ErrorCode { get; }

        private Result(bool isSuccess, string error, ErrorCode errorCode)
        {
            IsSuccess = isSuccess;
            Error = error ?? string.Empty;
            ErrorCode = errorCode;
        }

        public static Result Success()
        {
            return new Result(true, null, ErrorCode.None);
        }

        public static Result Failure(string error, ErrorCode code = ErrorCode.Unknown)
        {
            return new Result(false, error, code);
        }

        public static Result FromException(Exception ex, string context = "")
        {
            var message = string.IsNullOrEmpty(context)
                ? ex.Message
                : $"{context}: {ex.Message}";

            return new Result(false, message, CategorizeException(ex));
        }

        private static ErrorCode CategorizeException(Exception ex)
        {
            switch (ex)
            {
                case UnauthorizedAccessException _:
                    return ErrorCode.AccessDenied;
                case System.Net.Sockets.SocketException _:
                    return ErrorCode.NetworkError;
                case TimeoutException _:
                    return ErrorCode.Timeout;
                case ArgumentException _:
                    return ErrorCode.InvalidInput;
                case InvalidOperationException _:
                    return ErrorCode.InvalidOperation;
                case System.IO.IOException _:
                    return ErrorCode.IoError;
                default:
                    return ErrorCode.Unknown;
            }
        }

        public override string ToString()
        {
            return IsSuccess ? "Success" : $"Failure ({ErrorCode}): {Error}";
        }
    }

    /// <summary>
    /// Error codes for categorizing failures.
    /// </summary>
    public enum ErrorCode
    {
        None = 0,
        Unknown = 1,
        AccessDenied = 2,
        NetworkError = 3,
        Timeout = 4,
        InvalidInput = 5,
        InvalidOperation = 6,
        IoError = 7,
        AdapterNotFound = 8,
        ConfigurationError = 9,
        RegistryError = 10
    }
}
