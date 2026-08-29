using System.Text.Json;
using AuthApi.DTOs;

namespace AuthApi.Middleware;

/// <summary>
/// Catches any unhandled exception from anywhere in the pipeline, logs it,
/// and returns a plain 500 in our standard envelope.
///
/// The real exception message is written to the log only - it is never sent
/// to the browser, because it can contain internal details.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            // Let the request carry on to the controller.
            await _next(context);
        }
        catch (Exception exception)
        {
            // Something unexpected blew up: log the full details for us...
            _logger.LogError(exception, "Unhandled exception while processing {Method} {Path}.",
                context.Request.Method, context.Request.Path);

            // ...and send a safe, generic message to the caller.
            await WriteErrorResponseAsync(context);
        }
    }

    private static async Task WriteErrorResponseAsync(HttpContext context)
    {
        context.Response.Clear();
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";

        ApiResponse errorResponse = ApiResponse.Fail("Something went wrong. Please try again later.");

        // camelCase so the JSON matches what JavaScript expects (result.success).
        JsonSerializerOptions jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse, jsonOptions));
    }
}
