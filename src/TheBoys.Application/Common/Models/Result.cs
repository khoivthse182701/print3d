namespace TheBoys.Application.Common.Models;

public record Error(string Code, string Message, ErrorType Type = ErrorType.Failure, object? Extra = null, int? CustomStatusCode = null)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);
    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);
    public static Error NotFound(string code, string message = "") => new(code, message, ErrorType.NotFound);
    public static Error Conflict(string code, string message, object? extra = null) => new(code, message, ErrorType.Conflict, extra);
    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);
    public static Error PaymentRequired(string code, string message) => new(code, message, ErrorType.PaymentRequired);
    public static Error ServiceUnavailable(string code, string message) => new(code, message, ErrorType.ServiceUnavailable);
    public static Error BadGateway(string code, string message) => new(code, message, ErrorType.BadGateway);
    public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);
    public static Error Custom(int statusCode, string code, string message) => new(code, message, ErrorType.Custom, CustomStatusCode: statusCode);
    public static Error UnprocessableEntity(string code, string message) => new(code, message, ErrorType.UnprocessableEntity, CustomStatusCode: 422);
}

public enum ErrorType
{
    Failure,
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    PaymentRequired,
    ServiceUnavailable,
    BadGateway,
    UnprocessableEntity,
    Custom
}

public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None)
            throw new InvalidOperationException("Success result cannot have an error.");
        if (!isSuccess && error == Error.None)
            throw new InvalidOperationException("Failure result must have an error.");

        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);

    public static implicit operator Result(Error error) => Failure(error);
}

public class Result<T> : Result
{
    private readonly T? _value;

    protected Result(T? value, bool isSuccess, Error error) : base(isSuccess, error)
    {
        _value = value;
    }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access value of a failure result.");

    public static Result<T> Success(T value) => new(value, true, Error.None);
    public static new Result<T> Failure(Error error) => new(default, false, error);

    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator Result<T>(Error error) => Failure(error);
}
