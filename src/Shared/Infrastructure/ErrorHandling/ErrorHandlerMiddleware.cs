using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Abstractions.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Infrastructure.ErrorHandling;

public class ErrorHandlerMiddleware(RequestDelegate next, ILogger<ErrorHandlerMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (DomainException ex)
        {
            // Logging could be in separate middleware
            logger.LogInformation(ex, "Business invariant violation");

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;

            var response = new
            {
                error = ex.Message,
                traceId = context.TraceIdentifier,
                activityId = Activity.Current?.Id,
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
        catch (Exception ex)
        {
            // Logging could be in separate middleware
            logger.LogError(ex, "Unhandled exception occurred");

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            var response = new
            {
                error = "An error occurred processing your request.",
                traceId = context.TraceIdentifier,
                activityId = Activity.Current?.Id,
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
    }
}
