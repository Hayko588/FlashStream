using FlashStream.Application.Contracts;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace FlashStream.Api.Filters;

public sealed class ProblemDetailsExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ProblemDetailsExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title, errors) = exception switch
        {
            ValidationException validationException =>
                (StatusCodes.Status400BadRequest, "Request validation failed.",
                    validationException.Errors
                        .GroupBy(error => error.PropertyName)
                        .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray())),
            ProductNotFoundException or OrderNotFoundException =>
                (StatusCodes.Status404NotFound, "Resource not found.", null),
            IdempotencyConflictException =>
                (StatusCodes.Status409Conflict, "Idempotency conflict.", null),
            OrderRateLimitExceededException =>
                (StatusCodes.Status429TooManyRequests, "Rate limit exceeded.", null),
            RateLimiterUnavailableException =>
                (StatusCodes.Status503ServiceUnavailable, "Service temporarily unavailable.", null),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.", null)
        };

        if (status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled API exception.");
        }

        if (exception is OrderRateLimitExceededException rateLimitException)
        {
            httpContext.Response.Headers.RetryAfter = rateLimitException.RetryAfterSeconds.ToString();
        }

        httpContext.Response.StatusCode = status;
        var problemDetails = new ProblemDetails
        {
            Status = status,
            Title = title,
            Type = $"https://httpstatuses.com/{status}"
        };
        if (errors is not null)
        {
            problemDetails.Extensions["errors"] = errors;
        }

        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails
        });
        return true;
    }
}
