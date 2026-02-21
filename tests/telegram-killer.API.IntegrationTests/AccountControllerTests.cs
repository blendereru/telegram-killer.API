using System.Net.Http.Json;
using telegram_killer.API.DTOs.Request_DTOs;
using telegram_killer.API.IntegrationTests.Helpers;

namespace telegram_killer.API.IntegrationTests;

public class AccountControllerTests : IClassFixture<TelegramKillerWebApplicationFactory>, IAsyncLifetime
{
    private readonly TelegramKillerWebApplicationFactory _factory;
    public AccountControllerTests(TelegramKillerWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
    }

    [Fact]
    public async Task Register_NoBodyProvided_Returns400BadRequestWithProblemDetails()
    {
        // Arrange
        var client = _factory.CreateClient();
        
        var request = new GetUserEmailRequest();
        
        // Act
        var response = await client.PostAsJsonAsync("api/account/signup", request);
        
        // Assert
        await TestHelpers.AssertValidationProblemDetails(response, "Email", "required");
    }
    
    public Task DisposeAsync() => Task.CompletedTask;
}