using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;

namespace telegram_killer.API.IntegrationTests.Helpers;

public static class TestHelpers
{
    public static async Task AssertValidationProblemDetails(
        HttpResponseMessage response,
        string? expectedFieldName,
        string? expectedErrorMessage)
    {
        Assert.Equal("application/problem+json; charset=utf-8", response.Content.Headers.ContentType?.ToString());

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.NotNull(problem);

        Assert.NotNull(problem.Instance);
        Assert.NotNull(problem.Title);
        Assert.NotNull(problem.Type);
        Assert.NotNull(problem.Status);

        Assert.Equal((int)response.StatusCode, problem.Status);

        Assert.True(problem.Extensions.ContainsKey("requestId"), "Missing 'requestId' extension");
        Assert.True(problem.Extensions.ContainsKey("traceId"), "Missing 'traceId' extension");
        Assert.True(problem.Extensions.ContainsKey("timestamp"), "Missing 'timestamp' extension");

        if (problem.Extensions.TryGetValue("timestamp", out var timestampObj) && timestampObj is string ts)
        {
            Assert.True(DateTimeOffset.TryParse(ts, out _), "Timestamp extension is not a valid date");
        }

        if (expectedFieldName != null && expectedErrorMessage != null)
        {
            Assert.NotNull(problem.Errors);
            Assert.Contains(expectedFieldName, problem.Errors.Keys);
            Assert.Contains(problem.Errors[expectedFieldName], msg => msg.Contains(expectedErrorMessage));
        }
    }

    public static async Task AssertProblemDetails(HttpResponseMessage response, string? title = null)
    {
        Assert.Equal("application/problem+json; charset=utf-8", response.Content.Headers.ContentType?.ToString());

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problem);

        Assert.NotNull(problem.Instance);
        Assert.NotNull(problem.Title);
        Assert.NotNull(problem.Type);
        Assert.NotNull(problem.Status);

        Assert.Equal((int)response.StatusCode, problem.Status);

        Assert.True(problem.Extensions.ContainsKey("requestId"), "Missing 'requestId' extension");
        Assert.True(problem.Extensions.ContainsKey("traceId"), "Missing 'traceId' extension");
        Assert.True(problem.Extensions.ContainsKey("timestamp"), "Missing 'timestamp' extension");

        if (problem.Extensions.TryGetValue("timestamp", out var timestampObj) && timestampObj is string ts)
        {
            Assert.True(DateTimeOffset.TryParse(ts, out _), "Timestamp extension is not a valid date");
        }

        if (title != null)
        {
            Assert.Contains(title, problem.Title, StringComparison.OrdinalIgnoreCase);
        }
    }
}