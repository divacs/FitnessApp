using FitnessApp.Application.Common.Exceptions;
using FitnessApp.Application.Common.Responses;
using System.Net;
using AppValidationException = FitnessApp.Application.Common.Exceptions.ValidationException;

namespace FitnessApp.API.Middleware;

public class GlobalExceptionMiddleware
{
    private const string ServerErrorMessage = "Došlo je do greške na serveru.";

    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger)
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
        catch (Exception exception)
        {
            await HandleExceptionAsync(context, exception);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, message, errors) = exception switch
        {
            NotFoundException notFoundException => (HttpStatusCode.NotFound, notFoundException.Message, notFoundException.Errors),
            ForbiddenException forbiddenException => (HttpStatusCode.Forbidden, forbiddenException.Message, forbiddenException.Errors),
            BadRequestException badRequestException => (HttpStatusCode.BadRequest, badRequestException.Message, badRequestException.Errors),
            ConflictException conflictException => (HttpStatusCode.Conflict, conflictException.Message, conflictException.Errors),
            AppValidationException validationException => (HttpStatusCode.BadRequest, validationException.Message, validationException.Errors),
            UnauthorizedAccessException unauthorizedAccessException => (HttpStatusCode.Unauthorized, unauthorizedAccessException.Message, Array.Empty<string>()),
            _ => (HttpStatusCode.InternalServerError, ServerErrorMessage, Array.Empty<string>())
        };

        if (statusCode == HttpStatusCode.InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception occurred.");
        }

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        var response = new ErrorResponse
        {
            Message = message,
            Errors = errors
        };

        await context.Response.WriteAsJsonAsync(response);
    }
}
