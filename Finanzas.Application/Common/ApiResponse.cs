namespace Finanzas.Application.Common;

public sealed record ApiError(string Field, string Message);

public sealed record ApiResponse<T>(
    bool Success,
    string Message,
    T? Data,
    IReadOnlyCollection<ApiError> Errors)
{
    public static ApiResponse<T> Ok(T data, string message = "Operacion completada.") =>
        new(true, message, data, []);

    public static ApiResponse<T> Fail(string message, params ApiError[] errors) =>
        new(false, message, default, errors);
}

