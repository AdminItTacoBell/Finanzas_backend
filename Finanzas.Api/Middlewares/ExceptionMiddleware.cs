using System.Net;
using Finanzas.Application.Common;

namespace Finanzas.Api.Middlewares;

public sealed class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
        }
        catch (ExternalServiceUnavailableException exception)
        {
            _logger.LogWarning(exception, "Dependencia externa no disponible. TraceId {TraceId}", context.TraceIdentifier);
            await WriteAsync(
                context,
                HttpStatusCode.ServiceUnavailable,
                ApiResponse<object>.Fail(
                    exception.Message,
                    new ApiError("external-service", "La operacion puede reintentarse mas tarde.")));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error no controlado. TraceId {TraceId}", context.TraceIdentifier);
            await WriteAsync(
                context,
                HttpStatusCode.InternalServerError,
                ApiResponse<object>.Fail(
                    "No fue posible completar la operacion.",
                    new ApiError("server", $"Referencia: {context.TraceIdentifier}")));
        }
    }

    private static async Task WriteAsync(HttpContext context, HttpStatusCode status, object response)
    {
        context.Response.StatusCode = (int)status;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(response);
    }
}

