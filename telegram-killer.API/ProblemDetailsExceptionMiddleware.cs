using Microsoft.AspNetCore.Mvc;
using telegram_killer.API.Exceptions;

namespace telegram_killer.API;

public sealed class ProblemDetailsExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IProblemDetailsService _problemDetailsService;
    
    public ProblemDetailsExceptionMiddleware(RequestDelegate next, IProblemDetailsService problemDetailsService)
    {
        _next = next;
        _problemDetailsService = problemDetailsService;
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

    private async ValueTask HandleExceptionAsync(
        HttpContext context,
        Exception exception)
    {
        var problemDetails = new ProblemDetails
        {
            Type = exception is NotFoundException ? "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.5" :
                "https://datatracker.ietf.org/doc/html/rfc9110#section-15.6.1",
            Title = exception is NotFoundException ? exception.Message : "An error occured while processing you request",
            Status = exception is NotFoundException
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status500InternalServerError,
            Instance = $"{context.Request.Method} {context.Request.Path}"
        };

        var problemDetailsContext = new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problemDetails,
            Exception = exception
        };

        context.Response.StatusCode = exception is NotFoundException ? StatusCodes.Status404NotFound : StatusCodes.Status500InternalServerError;
        await _problemDetailsService.WriteAsync(problemDetailsContext);
    }
    
}