using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using telegram_killer.API.Data;
using telegram_killer.API.DTOs.Request_DTOs;
using telegram_killer.API.DTOs.Response_DTOs;
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
    public async Task CreateDirect_OtherUserEmailNotConfirmed_Returns403ForbiddenWithProblemDetails()
    {
        // Arrange
        var client = _factory.CreateClient();

        string accessToken;
        const string otherUserEmail = "nonconfirmed@example.com";
        Guid otherUserId;
        
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

            var otherUser = new User
            {
                Email = otherUserEmail,
                Username = otherUserEmail,
                IsEmailConfirmed = false,
                RegisteredAt = DateTimeOffset.UtcNow
            };
            context.Users.AddRange(user, otherUser);
            await context.SaveChangesAsync();
            
            otherUserId = otherUser.Id;
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

    [Fact]
    public async Task CreateDirect_Returns201Created()
    {
        // Arrange
        var client = _factory.CreateClient();

        string accessToken;
        Guid requestedId;
        Guid requesterId;
        
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
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        Assert.NotNull(response.Headers.Location);

        var chat = await response.Content.ReadFromJsonAsync<CreateChatResponse>();
        
        Assert.NotNull(chat);
        Assert.NotNull(chat.Participants);

        Assert.Equal($"api/chat/{chat.ChatId}", response.Headers.Location.OriginalString);
        
        var participantIds = chat.Participants;
        
        Assert.Equal(2, participantIds.Count);
        Assert.Contains(requestedId, participantIds);
        Assert.Contains(requesterId, participantIds);
        Assert.Equal(2, participantIds.Distinct().Count());
    }

    [Fact]
    public async Task CreateDirect_ReturnValuesMatchWithDbOnes()
    {
        // Arrange
        var client = _factory.CreateClient();

        string accessToken;
        Guid requestedId;
        Guid requesterId;

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
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var responseBody = await response.Content.ReadFromJsonAsync<CreateChatResponse>();

        Assert.NotNull(responseBody);

        using (var scope = _factory.CreateScope())
        {
            var applicationContext = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

            var chats = await applicationContext.Chats
                .Include(c => c.Participants)
                .ToListAsync();

            Assert.Single(chats);

            var dbChat = chats[0];
            
            Assert.Equal(responseBody.ChatId, dbChat.Id);
            
            var dbParticipantIds = dbChat.Participants
                .Select(p => p.UserId)
                .ToList();
            
            Assert.Equal(2, dbParticipantIds.Count);
            Assert.Equal(2, responseBody.Participants.Count);
            
            Assert.Contains(requesterId, dbParticipantIds);
            Assert.Contains(requestedId, dbParticipantIds);

            Assert.Contains(requesterId, responseBody.Participants);
            Assert.Contains(requestedId, responseBody.Participants);
            
            Assert.True(responseBody.Participants.All(id => dbParticipantIds.Contains(id)));
            Assert.True(dbParticipantIds.All(id => responseBody.Participants.Contains(id)));
        }
    }

    [Fact]
    public async Task GetMessages_AccessTokenNotProvided_Returns401Unauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        
        var chatId = Guid.NewGuid();
        
        // Act
        var response = await client.GetAsync($"api/chat/{chatId}/messages");
        
        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMessages_NonExistentChat_Returns404NotFoundWithProblemDetails()
    {
        // Arrange
        var client = _factory.CreateClient();

        string accessToken;
        var nonExistentChatId = Guid.NewGuid();
        
        using (var scope = _factory.CreateScope())
        {
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
            
            var tokensProviderService = scope.ServiceProvider.GetRequiredService<ITokensProviderService>();
            
            accessToken = tokensProviderService.GenerateAccessToken(user);
        }
        
        client.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, accessToken);
        
        // Act
        var response = await client.GetAsync($"api/chat/{nonExistentChatId}/messages");
        
        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        await TestHelpers.AssertProblemDetails(response, "not found");
    }

    [Fact]
    public async Task GetMessages_UserNotMemberOfChat_Returns403ForbiddenWithProblemDetails()
    {
        // Arrange
        var client = _factory.CreateClient();

        string accessToken;
        const string chatParticipant1Email = "participant1@example.com";
        const string chatParticipant2Email = "participant2@example.com";
        Guid chatId;
        
        using (var scope = _factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

            var user = new User
            {
                Email = "user@example.com",
                Username = "user@example.com",
                IsEmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            var anotherParticipant1 = new User
            {
                Email = chatParticipant1Email,
                Username = chatParticipant1Email,
                IsEmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow
            };
            
            var anotherParticipant2 = new User
            {
                Email = chatParticipant2Email,
                Username = chatParticipant2Email,
                IsEmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow
            };
            
            context.Users.AddRange(user, anotherParticipant1, anotherParticipant2);
            await context.SaveChangesAsync();

            var chat = new Chat
            {
                Type = ChatType.Direct,
                CreatedAt = DateTimeOffset.UtcNow,
                Participants = new List<ChatParticipant>
                {
                    new() { UserId = anotherParticipant1.Id, JoinedAt = DateTimeOffset.UtcNow },
                    new() { UserId = anotherParticipant2.Id, JoinedAt = DateTimeOffset.UtcNow }
                }
            };

            context.Chats.Add(chat);
            await context.SaveChangesAsync();

            chatId = chat.Id;
            
            var tokensProviderService = scope.ServiceProvider.GetRequiredService<ITokensProviderService>();
            
            accessToken = tokensProviderService.GenerateAccessToken(user);
        }
        
        client.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, accessToken);
        
        // Act
        var response = await client.GetAsync($"api/chat/{chatId}/messages");
        
        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await TestHelpers.AssertProblemDetails(response);
    }

    [Fact]
    public async Task GetMessages_NoMessagesInChat_Returns200Ok()
    {
        // Arrange
        var client = _factory.CreateClient();

        string accessToken;
        Guid chatId;
        const string chatParticipantEmail = "participant@example.com";
        
        using (var scope = _factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

            var user = new User
            {
                Email = "user@example.com",
                Username = "user@example.com",
                IsEmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            var anotherParticipant = new User
            {
                Email = chatParticipantEmail,
                Username = chatParticipantEmail,
                IsEmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow
            };
            
            context.Users.AddRange(user, anotherParticipant);
            await context.SaveChangesAsync();

            var chat = new Chat
            {
                Type = ChatType.Direct,
                CreatedAt = DateTimeOffset.UtcNow,
                Participants = new List<ChatParticipant>
                {
                    new() { UserId = user.Id, JoinedAt = DateTimeOffset.UtcNow },
                    new() { UserId = anotherParticipant.Id, JoinedAt = DateTimeOffset.UtcNow }
                }
            };

            context.Chats.Add(chat);
            await context.SaveChangesAsync();

            chatId = chat.Id;
            
            var tokensProviderService = scope.ServiceProvider.GetRequiredService<ITokensProviderService>();
            
            accessToken = tokensProviderService.GenerateAccessToken(user);
        }
        
        client.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, accessToken);
        
        // Act
        var response = await client.GetAsync($"api/chat/{chatId}/messages");
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var chatMessages = await response.Content.ReadFromJsonAsync<GetChatMessagesResponse>();

        Assert.NotNull(chatMessages);
        Assert.NotNull(chatMessages.Messages);
        Assert.Null(chatMessages.LastReadMessageId);
        Assert.Empty(chatMessages.Messages);
        Assert.NotNull(chatMessages.OtherParticipantReadStates);
        Assert.True(chatMessages.OtherParticipantReadStates.All(x => x.LastReadMessageId == null));

        using (var scope = _factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

            var chat = await context.Chats.SingleOrDefaultAsync(c => c.Id == chatMessages.ChatId);

            Assert.NotNull(chat);
            Assert.Equal(chat.Id, chatMessages.ChatId);
        }
    }

    [Fact]
    public async Task GetMessages_Returns200Ok()
    {
        // Arrange
        var client = _factory.CreateClient();
        
        string accessToken;
        Guid chatId;
        const string chatParticipantEmail = "participant@example.com";
        User user;
        User anotherParticipant;
        
        using (var scope = _factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

            user = new User
            {
                Email = "user@example.com",
                Username = "user@example.com",
                IsEmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            anotherParticipant = new User
            {
                Email = chatParticipantEmail,
                Username = chatParticipantEmail,
                IsEmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow
            };
            
            context.Users.AddRange(user, anotherParticipant);
            await context.SaveChangesAsync();

            var chat = new Chat
            {
                Type = ChatType.Direct,
                CreatedAt = DateTimeOffset.UtcNow,
                Participants = new List<ChatParticipant>
                {
                    new() { UserId = user.Id, JoinedAt = DateTimeOffset.UtcNow },
                    new() { UserId = anotherParticipant.Id, JoinedAt = DateTimeOffset.UtcNow }
                }
            };

            context.Chats.Add(chat);
            await context.SaveChangesAsync();
            
            var messages = new List<Message>
            {
                new Message
                {
                    ChatId = chat.Id,
                    SenderId = user.Id,
                    Content = "Hello!",
                    SentAt = DateTimeOffset.UtcNow
                },
                new Message
                {
                    ChatId = chat.Id,
                    SenderId = anotherParticipant.Id,
                    Content = "Hi there!",
                    SentAt = DateTimeOffset.UtcNow.AddSeconds(1)
                },
                new Message
                {
                    ChatId = chat.Id,
                    SenderId = user.Id,
                    Content = "How are you?",
                    SentAt = DateTimeOffset.UtcNow.AddSeconds(2)
                }
            };

            context.Messages.AddRange(messages);
            await context.SaveChangesAsync();
            
            chatId = chat.Id;
            
            var tokensProviderService = scope.ServiceProvider.GetRequiredService<ITokensProviderService>();
            
            accessToken = tokensProviderService.GenerateAccessToken(user);
        }
        
        client.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, accessToken);
        
        // Act
        var response = await client.GetAsync($"api/chat/{chatId}/messages");
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var chatMessages = await response.Content.ReadFromJsonAsync<GetChatMessagesResponse>();

        Assert.NotNull(chatMessages);
        Assert.NotNull(chatMessages.Messages);
        Assert.Null(chatMessages.LastReadMessageId);
        
        Assert.Equal(3, chatMessages.Messages.Count);
        
        var ordered = chatMessages.Messages.OrderBy(m => m.SentAt).ToList();
        Assert.True(chatMessages.Messages.SequenceEqual(ordered));
        
        Assert.Equal("Hello!", chatMessages.Messages[0].Content);
        Assert.Equal("Hi there!", chatMessages.Messages[1].Content);
        Assert.Equal("How are you?", chatMessages.Messages[2].Content);
        
        Assert.Equal(user.Id, chatMessages.Messages[0].SenderId);
        Assert.Equal(anotherParticipant.Id, chatMessages.Messages[1].SenderId);
        Assert.Equal(user.Id, chatMessages.Messages[2].SenderId);
    }

    [Fact]
    public async Task GetMessages_ParticipantsReadDifferentMessages_ReturnsValidReadStates()
    {
        // Arrange
        var client = _factory.CreateClient();

        string accessToken;
        Guid chatId;

        User user;
        User anotherParticipant;

        Guid m1Id;
        Guid m2Id;
        Guid m3Id;
        
        using (var scope = _factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
            var chatService = scope.ServiceProvider.GetRequiredService<IChatService>();
            var tokensProviderService = scope.ServiceProvider.GetRequiredService<ITokensProviderService>();

            user = new User
            {
                Email = "user@example.com",
                Username = "user@example.com",
                IsEmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            anotherParticipant = new User
            {
                Email = "participant@example.com",
                Username = "participant@example.com",
                IsEmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            context.Users.AddRange(user, anotherParticipant);
            await context.SaveChangesAsync();

            var chat = new Chat
            {
                Type = ChatType.Direct,
                CreatedAt = DateTimeOffset.UtcNow,
                Participants = new List<ChatParticipant>
                {
                    new() { UserId = user.Id, JoinedAt = DateTimeOffset.UtcNow },
                    new() { UserId = anotherParticipant.Id, JoinedAt = DateTimeOffset.UtcNow }
                }
            };

            context.Chats.Add(chat);
            await context.SaveChangesAsync();

            chatId = chat.Id;
            
            var m1 = await chatService.StoreMessage(chatId, user.Id, "Hello!");
            var m2 = await chatService.StoreMessage(chatId, anotherParticipant.Id, "Hi there!");
            var m3 = await chatService.StoreMessage(chatId, user.Id, "How are you?");

            m1Id = m1.Id;
            m2Id = m2.Id;
            m3Id = m3.Id;
            
            await chatService.MarkAsRead(chatId, m2.Id, user.Id);
            await chatService.MarkAsRead(chatId, m3.Id, anotherParticipant.Id);

            accessToken = tokensProviderService.GenerateAccessToken(user);
        }

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, accessToken);

        // Act
        var response = await client.GetAsync($"api/chat/{chatId}/messages");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<GetChatMessagesResponse>();

        Assert.NotNull(result);
        
        Assert.Equal(3, result.Messages.Count);

        Assert.Collection(result.Messages,
            m => Assert.Equal(m1Id, m.Id),
            m => Assert.Equal(m2Id, m.Id),
            m => Assert.Equal(m3Id, m.Id)
        );
        
        Assert.Equal(m2Id, result.LastReadMessageId);
        
        Assert.Single(result.OtherParticipantReadStates);

        var other = result.OtherParticipantReadStates.First();

        Assert.Equal(anotherParticipant.Id, other.UserId);
        
        Assert.Equal(m3Id, other.LastReadMessageId);
        
        Assert.NotEqual(result.LastReadMessageId, other.LastReadMessageId);
    }
    
    [Fact]
    public async Task GetMessages_ReturnsMessagesMatchingDatabase()
    {
        // Arrange
        var client = _factory.CreateClient();
        
        string accessToken;
        Guid chatId;

        using (var scope = _factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

            var user = new User
            {
                Email = "user@example.com",
                Username = "user@example.com",
                IsEmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            var anotherParticipant = new User
            {
                Email = "participant@example.com",
                Username = "participant@example.com",
                IsEmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow
            };
            
            context.Users.AddRange(user, anotherParticipant);
            await context.SaveChangesAsync();

            var chat = new Chat
            {
                Type = ChatType.Direct,
                CreatedAt = DateTimeOffset.UtcNow,
                Participants = new List<ChatParticipant>
                {
                    new() { UserId = user.Id, JoinedAt = DateTimeOffset.UtcNow },
                    new() { UserId = anotherParticipant.Id, JoinedAt = DateTimeOffset.UtcNow }
                }
            };

            context.Chats.Add(chat);
            await context.SaveChangesAsync();

            var messages = new List<Message>
            {
                new Message
                {
                    ChatId = chat.Id,
                    SenderId = user.Id,
                    Content = "Hello!",
                    SentAt = DateTimeOffset.UtcNow
                },
                new Message
                {
                    ChatId = chat.Id,
                    SenderId = anotherParticipant.Id,
                    Content = "Hi there!",
                    SentAt = DateTimeOffset.UtcNow.AddSeconds(1)
                }
            };

            context.Messages.AddRange(messages);
            await context.SaveChangesAsync();

            chatId = chat.Id;

            var tokensProviderService = scope.ServiceProvider.GetRequiredService<ITokensProviderService>();
            accessToken = tokensProviderService.GenerateAccessToken(user);
        }

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, accessToken);

        // Act
        var response = await client.GetAsync($"api/chat/{chatId}/messages");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var apiResult = await response.Content.ReadFromJsonAsync<GetChatMessagesResponse>();
        Assert.NotNull(apiResult);
        
        using (var scope = _factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

            var dbMessages = await context.Messages
                .Where(m => m.ChatId == chatId)
                .OrderBy(m => m.SentAt)
                .ToListAsync();

            Assert.Equal(dbMessages.Count, apiResult.Messages.Count);
            
            for (int i = 0; i < dbMessages.Count; i++)
            {
                var db = dbMessages[i];
                var api = apiResult.Messages[i];

                Assert.Equal(db.ChatId, apiResult.ChatId);
                Assert.Equal(db.Id, api.Id);
                Assert.Equal(db.SenderId, api.SenderId);
                Assert.Equal(db.Content, api.Content);
                Assert.InRange(api.SentAt, db.SentAt.AddSeconds(-1), db.SentAt.AddSeconds(1));
            }
        }
    }
    
    public Task DisposeAsync() => Task.CompletedTask;
}