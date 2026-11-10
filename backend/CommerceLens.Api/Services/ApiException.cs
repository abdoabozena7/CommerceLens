using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CommerceLens.Api.Services;

public class ApiException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken token)
    {
        var status = exception switch
        {
            ApiException known => known.StatusCode,
            BadHttpRequestException invalid => invalid.StatusCode,
            _ => 500
        };
        if (status >= 500) logger.LogError(exception, "Request failed: {Path}", context.Request.Path);
        else logger.LogWarning("Request rejected: {Message}", exception.Message);
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = exception is ApiException or BadHttpRequestException ? exception.Message
                : "The operation failed. See the backend log for details."
        }, token);
        return true;
    }
}
