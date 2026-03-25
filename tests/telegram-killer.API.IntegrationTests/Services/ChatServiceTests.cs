using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.DependencyInjection;
using telegram_killer.API.Data;
using telegram_killer.API.Exceptions;
using telegram_killer.API.Models;
using telegram_killer.API.Services.Interfaces;

namespace telegram_killer.API.IntegrationTests.Services;

public class ChatServiceTests : IClassFixture<TelegramKillerWebApplicationFactory>, IAsyncLifetime
{
    private readonly TelegramKillerWebApplicationFactory _factory;
    public ChatServiceTests(TelegramKillerWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
    }

    [Fact]
    public async Task CreateDirectChat_SameIds_ThrowsValidationException()
    {
        // Arrange
        var requesterId = Guid.NewGuid();

        using var scope = _factory.CreateScope();
        var chatService = scope.ServiceProvider.GetRequiredService<IChatService>();

        // Assert
        await Assert.ThrowsAsync<ValidationException>(() => chatService.CreateDirectChat(requesterId, requesterId));
    }

    [Fact]
    public async Task CreateDirectChat_RequestedUserDoNotExist_ThrowsNotFoundException()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var requestedId = Guid.NewGuid();
        
        using var scope = _factory.CreateScope();
        var chatService = scope.ServiceProvider.GetRequiredService<IChatService>();
        
        // Assert
        await Assert.ThrowsAsync<NotFoundException>(() => chatService.CreateDirectChat(requesterId, requestedId));
    }

    [Fact]
    public async Task CreateDirectChat_RequestedUserEmailNotConfirmed_ThrowsNotFoundException()
    {
        // Arrange
        var requesterId = Guid.NewGuid();

        Guid  requestedId;
        using (var scope = _factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

            var requestedUser = new User
            {
                Email = "user@example.com",
                Username = "user@example.com",
                IsEmailConfirmed = false,
                RegisteredAt = DateTimeOffset.UtcNow
            };
            
            context.Users.Add(requestedUser);
            await context.SaveChangesAsync();
            
            requestedId = requestedUser.Id;
        }
        
        // Assert
        using (var scope = _factory.CreateScope())
        {
            var chatService = scope.ServiceProvider.GetRequiredService<IChatService>();

            await Assert.ThrowsAsync<NotFoundException>(() => chatService.CreateDirectChat(requesterId, requestedId));
        }
    }
    
    public Task DisposeAsync() => Task.CompletedTask;
}