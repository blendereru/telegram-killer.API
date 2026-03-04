using Microsoft.AspNetCore.Mvc;
using telegram_killer.API.Exceptions;

namespace telegram_killer.API;

public sealed class ProblemDetailsExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<ProblemDetailsExceptionMiddleware> _logger;
    
    public ProblemDetailsExceptionMiddleware(RequestDelegate next, IProblemDetailsService problemDetailsService,
        ILogger<ProblemDetailsExceptionMiddleware> logger)
    {
        _next = next;
        _problemDetailsService = problemDetailsService;
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
            _logger.LogError(
                exception,
                "Unhandled exception while executing request {Method} {Path}",
                context.Request.Method,
                context.Request.Path);
            await HandleExceptionAsync(context, exception);
        }
    }

    private async ValueTask HandleExceptionAsync(
        HttpContext context,
        Exception exception)
    {
        var problemDetails = new ProblemDetails
        {
            Instance = $"{context.Request.Method} {context.Request.Path}"
        };
        
        DecorateProblemDetails(problemDetails, exception);
        
        var problemDetailsContext = new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problemDetails,
            Exception = exception
        };

        context.Response.StatusCode = problemDetails.Status!.Value;
        await _problemDetailsService.WriteAsync(problemDetailsContext);
    }

    private void DecorateProblemDetails(ProblemDetails problemDetails, Exception exception)
    {
        problemDetails.Title = exception.Message;
        
        if (exception.GetType() == typeof(NotFoundException))
        {
            problemDetails.Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.5";
            problemDetails.Status = StatusCodes.Status404NotFound;
        }
        else if (exception.GetType() == typeof(UnauthorizedException))
        {
            problemDetails.Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.2";
            problemDetails.Status = StatusCodes.Status401Unauthorized;
        }
        else if (exception.GetType() == typeof(AlreadyExistsException))
        {
            problemDetails.Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.10";
            problemDetails.Status = StatusCodes.Status409Conflict;
        }
        else if (exception.GetType() == typeof(ForbiddenException))
        {
            problemDetails.Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.4";
            problemDetails.Status = StatusCodes.Status403Forbidden;
        }
        else
        {
            problemDetails.Title = "An error occured while processing you request";
            problemDetails.Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.6.1";
            problemDetails.Status = StatusCodes.Status500InternalServerError;
        }
    }
}