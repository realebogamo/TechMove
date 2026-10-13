namespace GLMS.Shared.Results;

public enum ServiceErrorType
{
    None = 0,
    Validation,
    NotFound,
    Conflict,
    Unavailable
}

/// <summary>
/// Outcome of a business operation. Services return this instead of throwing so the MVC layer can
/// map errors onto ModelState and the API can map them onto HTTP status codes.
/// </summary>
public class ServiceResult
{
    public bool Succeeded => ErrorType == ServiceErrorType.None;
    public ServiceErrorType ErrorType { get; init; }

    /// <summary>Errors keyed by field name; an empty key means a general (non-field) error.</summary>
    public Dictionary<string, string[]> Errors { get; init; } = new();

    public IEnumerable<string> AllMessages => Errors.SelectMany(e => e.Value);

    public static ServiceResult Success() => new();

    public static ServiceResult Fail(ServiceErrorType type, string message, string field = "") =>
        new() { ErrorType = type, Errors = new() { [field] = new[] { message } } };
}

public class ServiceResult<T> : ServiceResult
{
    public T? Value { get; init; }

    public static ServiceResult<T> Success(T value) => new() { Value = value };

    public static new ServiceResult<T> Fail(ServiceErrorType type, string message, string field = "") =>
        new() { ErrorType = type, Errors = new() { [field] = new[] { message } } };

    public static ServiceResult<T> NotFound(string message) => Fail(ServiceErrorType.NotFound, message);

    public static ServiceResult<T> Invalid(string message, string field = "") =>
        Fail(ServiceErrorType.Validation, message, field);
}
