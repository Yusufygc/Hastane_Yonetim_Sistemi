namespace Hospital.Application.Common;

public enum ErrorType
{
    Validation,
    Unauthorized,
    Forbidden,
    NotFound,
    Conflict,
    Failure,
}

/// <summary>Beklenen iş hatası. Alan bazlı doğrulama hataları <see cref="Details"/> içinde taşınır.</summary>
public sealed record Error(
    string Code,
    string Message,
    ErrorType Type,
    IReadOnlyDictionary<string, string[]>? Details = null)
{
    public static Error Validation(string code, string message, IReadOnlyDictionary<string, string[]>? details = null) =>
        new(code, message, ErrorType.Validation, details);

    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);

    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);
}

/// <summary>Hata üretebilen bir sonucun başarısız örneğini oluşturur (pipeline behavior'lar için).</summary>
public interface IFailureFactory<TSelf>
    where TSelf : IFailureFactory<TSelf>
{
    static abstract TSelf Failure(Error error);
}

public class Result : IFailureFactory<Result>
{
    protected Result(bool isSuccess, Error? error)
    {
        if (isSuccess == (error is not null))
        {
            throw new ArgumentException("Başarılı sonuç hata taşıyamaz, başarısız sonuç hata taşımalıdır.", nameof(error));
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error? Error { get; }

    public static Result Success() => new(true, null);

    public static Result Failure(Error error) => new(false, error);

    public static Result<T> Success<T>(T value) => new(value);

    public static implicit operator Result(Error error) => Failure(error);
}

public sealed class Result<T> : Result, IFailureFactory<Result<T>>
{
    private readonly T? _value;

    internal Result(T value) : base(true, null) => _value = value;

    private Result(Error error) : base(false, error)
    {
    }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Başarısız sonucun değeri okunamaz.");

    public static new Result<T> Failure(Error error) => new(error);

    public static implicit operator Result<T>(T value) => new(value);

    public static implicit operator Result<T>(Error error) => Failure(error);
}
