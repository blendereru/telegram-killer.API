namespace telegram_killer.API.Extensions;

public static class ProblemDetailsExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseProblemDetailsException(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ProblemDetailsExceptionMiddleware>();
    }
}