using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
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

            var user = new User
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

        var request = new CreateChatRequest
        {
            OtherUserId = otherUserId
        };
        
        client.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, accessToken);
        
        // Act
        var response = await client.PostAsJsonAsync("api/chat", request);
        
        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await TestHelpers.AssertValidationProblemDetails(response, null, null);
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

    [Fact]
    public async Task CreateDirect_ChatAlreadyExists_Returns200Ok()
    {
        // Arrange
        var client = _factory.CreateClient();

        string accessToken;
        Guid requestedId;
        Guid requesterId;
        var existentChatId = Guid.NewGuid();
        
        using (var scope = _factory.CreateScope())
        {
            var applicationContext = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

            var requester = new User
            {
                Email = "user1@example.com",
                Username = "user1@example.com",
                IsEmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            var requested = new User
            {
                Email = "user2@example.com",
                Username = "user2@example.com",
                IsEmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow
            };
            
            applicationContext.Users.AddRange(requester, requested);
            await applicationContext.SaveChangesAsync();

            requestedId = requested.Id;
            requesterId = requester.Id;
            
            var existentChat = new Chat
            {
                Id = existentChatId,
                Type = ChatType.Direct,
                CreatedAt = DateTimeOffset.UtcNow,
                Participants = new List<ChatParticipant>
                {
                    new() { UserId = requester.Id, JoinedAt = DateTimeOffset.UtcNow },
                    new() { UserId = requested.Id, JoinedAt = DateTimeOffset.UtcNow }
                }
            };
            
            applicationContext.Chats.Add(existentChat);
            await applicationContext.SaveChangesAsync();
            
            var tokensProviderService = scope.ServiceProvider.GetRequiredService<ITokensProviderService>();
            
            accessToken = tokensProviderService.GenerateAccessToken(requester);
        }

        var request = new CreateChatRequest
        {
            OtherUserId = requestedId
        };
        
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, accessToken);
        
        // Act
        var response = await client.PostAsJsonAsync("api/chat", request);
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using (var scope = _factory.CreateScope())
        {
            var applicationContext = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
            
            var existentChat = await applicationContext.Chats
                .Include(chat => chat.Participants)
                .SingleOrDefaultAsync(c => c.Id == existentChatId);
            
            Assert.NotNull(existentChat);
            Assert.Equal(existentChatId, existentChat.Id);
            
            Assert.Equal(2, existentChat.Participants.Count);

            var participantIds = existentChat.Participants
                .Select(p => p.UserId)
                .ToList();
            
            Assert.Contains(requesterId, participantIds);
            Assert.Contains(requestedId, participantIds);
            
            Assert.Equal(2, participantIds.Distinct().Count());
        }
    }
    public Task DisposeAsync() => Task.CompletedTask;
}