using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using telegram_killer.API.Data;
using telegram_killer.API.DTOs.Request_DTOs;
using telegram_killer.API.IntegrationTests.Helpers;
using telegram_killer.API.Models;
using telegram_killer.API.Services.Interfaces;

namespace telegram_killer.API.IntegrationTests.Controllers;

public class ChatControllerTests : IClassFixture<TelegramKillerWebApplicationFactory>, IAsyncLifetime
{
    private readonly TelegramKillerWebApplicationFactory _factory;
    
    public ChatControllerTests(TelegramKillerWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
    }

    [Fact]
    public async Task CreateDirect_NoAccessTokenProvided_Returns401Unauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        var request = new CreateChatRequest();
        
        // Act
        var response = await client.PostAsJsonAsync("api/chat", request);
        
        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateDirect_EmailNotConfirmed_Returns403Forbidden()
    {
        // Arrange
        var client = _factory.CreateClient();

        string emailNotConfirmedAccessToken;
        using (var scope = _factory.CreateScope())
        {
            var tokensProviderService = scope.ServiceProvider.GetRequiredService<ITokensProviderService>();

            var user = new User()
            {
                Id = Guid.NewGuid(),
                Email = "user@example.com",
                Username = "user@example.com",
                IsEmailConfirmed = false,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            emailNotConfirmedAccessToken = tokensProviderService.GenerateAccessToken(user);
        }

        var request = new CreateChatRequest();

        client.DefaultRequestHeaders.Authorization
            = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, emailNotConfirmedAccessToken);
        
        // Act
        var response = await client.PostAsJsonAsync("api/chat", request);
        
        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateDirect_BothUsersIdMatch_Returns400BadRequestWithValidationProblemDetails()
    {
        // Arrange
        var client = _factory.CreateClient();

        string accessToken;
        Guid otherUserId;
        
        using (var scope = _factory.CreateScope())
        {
            var tokensProviderService = scope.ServiceProvider.GetRequiredService<ITokensProviderService>();

            var user = new User()
            {
                Id = Guid.NewGuid(),
                Email = "user@example.com",
                Username = "user@example.com",
                IsEmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            otherUserId = user.Id;
            accessToken = tokensProviderService.GenerateAccessToken(user);
        }

        var request = new CreateChatRequest()
        {
            OtherUserId = otherUserId
        };
        
        client.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, accessToken);
        
        // Act
        var response = await client.PostAsJsonAsync("api/chat", request);
        
        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await TestHelpers.AssertValidationProblemDetails(response, "UserA", "equal");
    }

    [Fact]
    public async Task CreateDirect_OtherUserDoesNotExist_Returns404NotFoundWithProblemDetails()
    {
        // Arrange
        var client = _factory.CreateClient();

        string accessToken;
        var otherNonExistentUserId = Guid.NewGuid();
        
        using (var scope = _factory.CreateScope())
        {
            var tokensProviderService = scope.ServiceProvider.GetRequiredService<ITokensProviderService>();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
            
            var user = new User
            {
                Email = "user@example.com",
                Username = "user@example.com",
                IsEmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow
            };
            
            context.Users.Add(user);
            await context.SaveChangesAsync();
            
            accessToken = tokensProviderService.GenerateAccessToken(user);
        }

        var request = new CreateChatRequest
        {
            OtherUserId = otherNonExistentUserId
        };
        
        client.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, accessToken);
        
        // Act
        var response = await client.PostAsJsonAsync("api/chat", request);
        
        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await TestHelpers.AssertProblemDetails(response, "not found");
    }
    
    public Task DisposeAsync() => Task.CompletedTask;
}