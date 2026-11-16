namespace CompanyApi.Application.Common;

public enum ResultStatus
{
    Ok,
    Invalid,
    NotFound,
}

/// <summary>
/// Outcome of an application operation. Expected failures (validation, not found) are values, not exceptions.
/// </summary>
public sealed record Result<T>
{
    private Result(ResultStatus status, T? value, string? message, IReadOnlyList<string> errors)
    {
        Status = status;
        Value = value;
        Message = message;
        Errors = errors;
    }

    public ResultStatus Status { get; }
    public T? Value { get; }
    public string? Message { get; }
    public IReadOnlyList<string> Errors { get; }
    public bool IsSuccess => Status == ResultStatus.Ok;

    public static Result<T> Ok(T value) => new(ResultStatus.Ok, value, null, []);

    public static Result<T> Invalid(string message, IReadOnlyList<string>? errors = null) =>
        new(ResultStatus.Invalid, default, message, errors ?? [message]);

    public static Result<T> NotFound(string message) => new(ResultStatus.NotFound, default, message, [message]);
}
