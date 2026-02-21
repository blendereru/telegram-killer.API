using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;

namespace telegram_killer.API.IntegrationTests.Helpers;

public class TestHelpers
{
    public static async Task AssertValidationProblemDetails(
        HttpResponseMessage response, 
        string expectedFieldName, 
        string expectedErrorMessage)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json; charset=utf-8", response.Content.Headers.ContentType?.ToString());

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
    
        Assert.NotNull(problem);
        Assert.False(string.IsNullOrWhiteSpace(problem.Instance), "Problem Instance should not be null");
        
        Assert.True(problem.Extensions.ContainsKey("requestId"), "Missing 'requestId' extension");
        Assert.True(problem.Extensions.ContainsKey("traceId"), "Missing 'traceId' extension");
        Assert.True(problem.Extensions.ContainsKey("timestamp"), "Missing 'timestamp' extension");
        
        if (problem.Extensions.TryGetValue("timestamp", out var timestampObj) && timestampObj is string ts)
        {
            Assert.True(DateTimeOffset.TryParse(ts, out _), "Timestamp extension is not a valid date");
        }
        
        Assert.NotNull(problem.Errors);
        Assert.Contains(expectedFieldName, problem.Errors.Keys);
        Assert.Contains(problem.Errors[expectedFieldName], msg => msg.Contains(expectedErrorMessage));
    }
}