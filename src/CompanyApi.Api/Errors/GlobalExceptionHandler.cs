using CompanyApi.Api.Contracts;
using Microsoft.AspNetCore.Diagnostics;

namespace CompanyApi.Api.Errors;

/// <summary>Turns unhandled exceptions into a generic 500 ErrorResponse; details are logged, never returned.</summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public const string GenericMessage = "An unexpected error occurred. Please try again later.";

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            // Client went away; nothing useful to send.
            httpContext.Response.StatusCode = 499;
            return true;
        }

        logger.LogError(exception, "Unhandled exception for {Method} {Path} (trace {TraceId})",
            httpContext.Request.Method, httpContext.Request.Path, httpContext.TraceIdentifier);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(ErrorResponse.From(GenericMessage), cancellationToken);
        return true;
    }
}
