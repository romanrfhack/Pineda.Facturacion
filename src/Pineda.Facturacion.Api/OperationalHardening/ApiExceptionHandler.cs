using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Configurations;

namespace Pineda.Facturacion.Api.OperationalHardening;

internal sealed class ApiExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<ApiExceptionHandler> _logger;

    public ApiExceptionHandler(
        IProblemDetailsService problemDetailsService,
        ILogger<ApiExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        var (statusCode, title, detail) = Classify(exception);

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception for {Method} {Path}.", httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            _logger.LogWarning(exception, "Request failed with controlled exception for {Method} {Path}.", httpContext.Request.Method, httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = statusCode;

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Type = "about:blank",
            Instance = httpContext.Request.Path
        };

        await _problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
            Exception = exception
        });

        return true;
    }

    private static (int StatusCode, string Title, string Detail) Classify(Exception exception)
    {
        if (exception is BadHttpRequestException badHttpRequestException)
        {
            return (StatusCodes.Status400BadRequest, "Invalid request", badHttpRequestException.Message);
        }

        if (exception is ArgumentException argumentException)
        {
            return (StatusCodes.Status400BadRequest, "Invalid request", argumentException.Message);
        }

        if (IsIssuerProfileActiveConflict(exception))
        {
            return (
                StatusCodes.Status409Conflict,
                "Conflict",
                "Another active issuer profile already exists. Deactivate the current active issuer before activating a different one.");
        }

        return (
            StatusCodes.Status500InternalServerError,
            "Unexpected error",
            "An unexpected error occurred while processing the request.");
    }

    private static bool IsIssuerProfileActiveConflict(Exception exception)
    {
        if (exception is not DbUpdateException)
        {
            return false;
        }

        Exception? current = exception;
        while (current is not null)
        {
            if (!string.IsNullOrWhiteSpace(current.Message)
                && current.Message.Contains(IssuerProfileConfiguration.ActiveSingletonIndexName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            current = current.InnerException;
        }

        return false;
    }
}
