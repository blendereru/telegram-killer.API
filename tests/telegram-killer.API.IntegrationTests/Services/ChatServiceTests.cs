using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
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

        Guid requestedId;
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

    [Fact]
    public async Task GetMessages_ChatDoesNotExist_ThrowsNotFoundException()
    {
        // Arrange
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        using (var scope = _factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

            var user = new User
            {
                Id = userId,
                Email = "user@example.com",
                Username = "user@example.com",
                IsEmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();
        }

        using var serviceScope = _factory.CreateScope();
        var chatService = serviceScope.ServiceProvider.GetRequiredService<IChatService>();

        // Assert
        await Assert.ThrowsAsync<NotFoundException>(() => chatService.GetMessages(chatId, userId));
    }

    [Fact]
    public async Task GetMessages_UserIsNotMemberOfChat_ThrowsForbiddenException()
    {
        // Arrange
        var chatId = Guid.NewGuid();

        Guid userId;
        Guid anotherUserId;

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

            var anotherUser = new User
            {
                Email = "another@example.com",
                Username = "another@example.com",
                IsEmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            context.Users.AddRange(user, anotherUser);
            await context.SaveChangesAsync();

            userId = user.Id;
            anotherUserId = anotherUser.Id;

            var chat = new Chat
            {
                Id = chatId,
                Type = ChatType.Direct,
                CreatedAt = DateTimeOffset.UtcNow,
                Participants = new List<ChatParticipant>
                {
                    new() { UserId = anotherUserId, JoinedAt = DateTimeOffset.UtcNow }
                }
            };

            context.Chats.Add(chat);
            await context.SaveChangesAsync();
        }

        using var serviceScope = _factory.CreateScope();
        var chatService = serviceScope.ServiceProvider.GetRequiredService<IChatService>();

        // Assert
        await Assert.ThrowsAsync<ForbiddenException>(() => chatService.GetMessages(chatId, userId));
    }

    [Fact]
    public async Task StoreMessage_UserIsNotParticipant_ThrowsUnauthorizedException()
    {
        // Arrange
        Guid chatId;
        Guid senderId;

        using (var scope = _factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

            var sender = new User
            {
                Email = "sender@example.com",
                Username = "sender@example.com",
                IsEmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            var otherParticipant = new User
            {
                Email = "other@example.com",
                Username = "other@example.com",
                IsEmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            context.Users.AddRange(sender, otherParticipant);
            await context.SaveChangesAsync();

            senderId = sender.Id;

            var chat = new Chat
            {
                Type = ChatType.Direct,
                CreatedAt = DateTimeOffset.UtcNow,
                Participants = new List<ChatParticipant>
                {
                    new() { UserId = otherParticipant.Id, JoinedAt = DateTimeOffset.UtcNow }
                }
            };

            context.Chats.Add(chat);
            await context.SaveChangesAsync();

            chatId = chat.Id;
        }

        using var serviceScope = _factory.CreateScope();
        var chatService = serviceScope.ServiceProvider.GetRequiredService<IChatService>();

        // Assert
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            chatService.StoreMessage(chatId, senderId, "Hello!"));
    }

    [Fact]
    public async Task StoreMessage_UserIsParticipant_StoresMessageSuccessfully()
    {
        // Arrange
        Guid chatId;
        Guid senderId;
        const string content = "Hello, world!";

        using (var scope = _factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

            var sender = new User
            {
                Email = "sender@example.com",
                Username = "sender@example.com",
                IsEmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            var otherParticipant = new User
            {
                Email = "other@example.com",
                Username = "other@example.com",
                IsEmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            context.Users.AddRange(sender, otherParticipant);
            await context.SaveChangesAsync();

            senderId = sender.Id;

            var chat = new Chat
            {
                Type = ChatType.Direct,
                CreatedAt = DateTimeOffset.UtcNow,
                Participants = new List<ChatParticipant>
                {
                    new() { UserId = sender.Id, JoinedAt = DateTimeOffset.UtcNow },
                    new() { UserId = otherParticipant.Id, JoinedAt = DateTimeOffset.UtcNow }
                }
            };

            context.Chats.Add(chat);
            await context.SaveChangesAsync();

            chatId = chat.Id;
        }

        using var serviceScope = _factory.CreateScope();
        var chatService = serviceScope.ServiceProvider.GetRequiredService<IChatService>();

        // Act
        var message = await chatService.StoreMessage(chatId, senderId, content);

        // Assert
        Assert.NotNull(message);
        Assert.NotEqual(Guid.Empty, message.Id);
        Assert.Equal(chatId, message.ChatId);
        Assert.Equal(senderId, message.SenderId);
        Assert.Equal(content, message.Content);
        Assert.InRange(message.SentAt, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(1));
    }

    [Fact]
    public async Task StoreMessage_PersistsMessageInDatabase()
    {
        // Arrange
        Guid chatId;
        Guid senderId;
        const string content = "Persist me";

        using (var scope = _factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

            var sender = new User
            {
                Email = "sender@example.com",
                Username = "sender@example.com",
                IsEmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            var otherParticipant = new User
            {
                Email = "other@example.com",
                Username = "other@example.com",
                IsEmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            context.Users.AddRange(sender, otherParticipant);
            await context.SaveChangesAsync();

            senderId = sender.Id;

            var chat = new Chat
            {
                Type = ChatType.Direct,
                CreatedAt = DateTimeOffset.UtcNow,
                Participants = new List<ChatParticipant>
                {
                    new() { UserId = sender.Id, JoinedAt = DateTimeOffset.UtcNow },
                    new() { UserId = otherParticipant.Id, JoinedAt = DateTimeOffset.UtcNow }
                }
            };

            context.Chats.Add(chat);
            await context.SaveChangesAsync();

            chatId = chat.Id;
        }

        using var serviceScope = _factory.CreateScope();
        var chatService = serviceScope.ServiceProvider.GetRequiredService<IChatService>();

        // Act
        var storedMessage = await chatService.StoreMessage(chatId, senderId, content);

        // Assert
        using (var verifyScope = _factory.CreateScope())
        {
            var context = verifyScope.ServiceProvider.GetRequiredService<ApplicationContext>();

            var dbMessage = await context.Messages.SingleOrDefaultAsync(m => m.Id == storedMessage.Id);

            Assert.NotNull(dbMessage);
            Assert.Equal(chatId, dbMessage.ChatId);
            Assert.Equal(senderId, dbMessage.SenderId);
            Assert.Equal(content, dbMessage.Content);
        }
    }

    [Fact]
    public async Task StoreMessage_UpdatesSenderLastReadMessageId()
    {
        // Arrange
        Guid chatId;
        Guid senderId;

        using (var scope = _factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

            var sender = new User
            {
                Email = "sender@example.com",
                Username = "sender@example.com",
                IsEmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            var otherParticipant = new User
            {
                Email = "other@example.com",
                Username = "other@example.com",
                IsEmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            context.Users.AddRange(sender, otherParticipant);
            await context.SaveChangesAsync();

            senderId = sender.Id;

            var chat = new Chat
            {
                Type = ChatType.Direct,
                CreatedAt = DateTimeOffset.UtcNow,
                Participants = new List<ChatParticipant>
                {
                    new()
                    {
                        UserId = sender.Id,
                        JoinedAt = DateTimeOffset.UtcNow
                    },

                    new()
                    {
                        UserId = otherParticipant.Id,
                        JoinedAt = DateTimeOffset.UtcNow
                    }
                }
            };

            context.Chats.Add(chat);
            await context.SaveChangesAsync();

            chatId = chat.Id;
        }

        using var serviceScope = _factory.CreateScope();
        var chatService = serviceScope.ServiceProvider.GetRequiredService<IChatService>();

        // Act
        var message = await chatService.StoreMessage(chatId, senderId, "First message");

        // Assert
        using (var verifyScope = _factory.CreateScope())
        {
            var context = verifyScope.ServiceProvider.GetRequiredService<ApplicationContext>();

            var participant = await context.ChatParticipants
                .SingleAsync(cp => cp.ChatId == chatId && cp.UserId == senderId);

            Assert.Equal(message.Id, participant.LastReadMessageId);
            Assert.NotNull(participant.LastReadAt);
        }
    }

    [Fact]
    public async Task StoreMessage_MessageCountIncreasesByOne()
    {
        // Arrange
        Guid chatId;
        Guid senderId;

        using (var scope = _factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

            var sender = new User
            {
                Email = "sender@example.com",
                Username = "sender@example.com",
                IsEmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            var otherParticipant = new User
            {
                Email = "other@example.com",
                Username = "other@example.com",
                IsEmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            context.Users.AddRange(sender, otherParticipant);
            await context.SaveChangesAsync();

            senderId = sender.Id;

            var chat = new Chat
            {
                Type = ChatType.Direct,
                CreatedAt = DateTimeOffset.UtcNow,
                Participants = new List<ChatParticipant>
                {
                    new() { UserId = sender.Id, JoinedAt = DateTimeOffset.UtcNow },
                    new() { UserId = otherParticipant.Id, JoinedAt = DateTimeOffset.UtcNow }
                }
            };

            context.Chats.Add(chat);
            await context.SaveChangesAsync();

            chatId = chat.Id;
        }

        using (var verifyScope = _factory.CreateScope())
        {
            var context = verifyScope.ServiceProvider.GetRequiredService<ApplicationContext>();
            var beforeCount = await context.Messages.CountAsync(m => m.ChatId == chatId);

            var chatService = verifyScope.ServiceProvider.GetRequiredService<IChatService>();
            await chatService.StoreMessage(chatId, senderId, "Count me in");

            var afterCount = await context.Messages.CountAsync(m => m.ChatId == chatId);

            Assert.Equal(beforeCount + 1, afterCount);
        }
    }

    [Fact]
    public async Task MarkAsRead_MessageDoesNotExistInChat_ThrowsNotFoundException()
    {
        // Arrange
        Guid chatId;
        Guid userId;

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

            var anotherUser = new User
            {
                Email = "another@example.com",
                Username = "another@example.com",
                IsEmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            context.Users.AddRange(user, anotherUser);
            await context.SaveChangesAsync();

            userId = user.Id;

            var chat = new Chat
            {
                Type = ChatType.Direct,
                CreatedAt = DateTimeOffset.UtcNow,
                Participants = new List<ChatParticipant>
                {
                    new() { UserId = user.Id, JoinedAt = DateTimeOffset.UtcNow },
                    new() { UserId = anotherUser.Id, JoinedAt = DateTimeOffset.UtcNow }
                }
            };

            context.Chats.Add(chat);
            await context.SaveChangesAsync();

            chatId = chat.Id;
        }

        using var serviceScope = _factory.CreateScope();
        var chatService = serviceScope.ServiceProvider.GetRequiredService<IChatService>();

        // Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            chatService.MarkAsRead(chatId, Guid.NewGuid(), userId));
    }

    [Fact]
    public async Task MarkAsRead_UserIsNotParticipant_ThrowsNotFoundException()
    {
        // Arrange
        Guid chatId;
        Guid outsiderUserId;
        Guid messageId;

        using (var scope = _factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

            var participant = new User
            {
                Email = "participant@example.com",
                Username = "participant@example.com",
                IsEmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            var outsider = new User
            {
                Email = "outsider@example.com",
                Username = "outsider@example.com",
                IsEmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            context.Users.AddRange(participant, outsider);
            await context.SaveChangesAsync();
            
            outsiderUserId = outsider.Id;

            var chat = new Chat
            {
                Type = ChatType.Direct,
                CreatedAt = DateTimeOffset.UtcNow,
                Participants = new List<ChatParticipant>
                {
                    new() { UserId = participant.Id, JoinedAt = DateTimeOffset.UtcNow }
                }
            };

            context.Chats.Add(chat);
            await context.SaveChangesAsync();

            chatId = chat.Id;

            var message = new Message
            {
                ChatId = chatId,
                SenderId = participant.Id,
                Content = "Hello!",
                SentAt = DateTimeOffset.UtcNow
            };

            context.Messages.Add(message);
            await context.SaveChangesAsync();

            messageId = message.Id;
        }

        using var serviceScope = _factory.CreateScope();
        var chatService = serviceScope.ServiceProvider.GetRequiredService<IChatService>();

        // Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            chatService.MarkAsRead(chatId, messageId, outsiderUserId));
    }

    [Fact]
    public async Task MarkAsRead_ValidMessageAndParticipant_ReturnsResponse()
    {
        // Arrange
        Guid chatId;
        Guid senderId;
        Guid readerId;
        Guid messageId;

        using (var scope = _factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

            var sender = new User
            {
                Email = "sender@example.com",
                Username = "sender@example.com",
                IsEmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            var reader = new User
            {
                Email = "reader@example.com",
                Username = "reader@example.com",
                IsEmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            context.Users.AddRange(sender, reader);
            await context.SaveChangesAsync();

            senderId = sender.Id;
            readerId = reader.Id;

            var chat = new Chat
            {
                Type = ChatType.Direct,
                CreatedAt = DateTimeOffset.UtcNow,
                Participants = new List<ChatParticipant>
                {
                    new() { UserId = sender.Id, JoinedAt = DateTimeOffset.UtcNow },
                    new() { UserId = reader.Id, JoinedAt = DateTimeOffset.UtcNow }
                }
            };

            context.Chats.Add(chat);
            await context.SaveChangesAsync();

            chatId = chat.Id;

            var message = new Message
            {
                ChatId = chatId,
                SenderId = senderId,
                Content = "First message",
                SentAt = DateTimeOffset.UtcNow
            };

            context.Messages.Add(message);
            await context.SaveChangesAsync();

            messageId = message.Id;
        }

        using var serviceScope = _factory.CreateScope();
        var chatService = serviceScope.ServiceProvider.GetRequiredService<IChatService>();

        // Act
        var result = await chatService.MarkAsRead(chatId, messageId, readerId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(senderId, result.SenderId);
        Assert.NotNull(result.ReadAt);
        Assert.InRange(result.ReadAt.Value, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(1));
    }

    [Fact]
    public async Task MarkAsRead_UpdatesParticipantReadState()
    {
        // Arrange
        Guid chatId;
        Guid senderId;
        Guid readerId;
        Guid messageId;

        using (var scope = _factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

            var sender = new User
            {
                Email = "sender@example.com",
                Username = "sender@example.com",
                IsEmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            var reader = new User
            {
                Email = "reader@example.com",
                Username = "reader@example.com",
                IsEmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            context.Users.AddRange(sender, reader);
            await context.SaveChangesAsync();

            senderId = sender.Id;
            readerId = reader.Id;

            var chat = new Chat
            {
                Type = ChatType.Direct,
                CreatedAt = DateTimeOffset.UtcNow,
                Participants = new List<ChatParticipant>
                {
                    new() { UserId = sender.Id, JoinedAt = DateTimeOffset.UtcNow },
                    new() { UserId = reader.Id, JoinedAt = DateTimeOffset.UtcNow }
                }
            };

            context.Chats.Add(chat);
            await context.SaveChangesAsync();

            chatId = chat.Id;

            var message = new Message
            {
                ChatId = chatId,
                SenderId = senderId,
                Content = "Read this",
                SentAt = DateTimeOffset.UtcNow
            };

            context.Messages.Add(message);
            await context.SaveChangesAsync();

            messageId = message.Id;
        }

        using var serviceScope = _factory.CreateScope();
        var chatService = serviceScope.ServiceProvider.GetRequiredService<IChatService>();

        // Act
        await chatService.MarkAsRead(chatId, messageId, readerId);

        // Assert
        using (var verifyScope = _factory.CreateScope())
        {
            var context = verifyScope.ServiceProvider.GetRequiredService<ApplicationContext>();

            var participant = await context.ChatParticipants
                .SingleAsync(cp => cp.ChatId == chatId && cp.UserId == readerId);

            Assert.Equal(messageId, participant.LastReadMessageId);
            Assert.NotNull(participant.LastReadAt);
        }
    }

    [Fact]
    public async Task MarkAsRead_CalledTwiceWithSameMessage_DoesNotThrow()
    {
        // Arrange
        Guid chatId;
        Guid senderId;
        Guid readerId;
        Guid messageId;

        using (var scope = _factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

            var sender = new User
            {
                Email = "sender@example.com",
                Username = "sender@example.com",
                IsEmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            var reader = new User
            {
                Email = "reader@example.com",
                Username = "reader@example.com",
                IsEmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            context.Users.AddRange(sender, reader);
            await context.SaveChangesAsync();

            senderId = sender.Id;
            readerId = reader.Id;

            var chat = new Chat
            {
                Type = ChatType.Direct,
                CreatedAt = DateTimeOffset.UtcNow,
                Participants = new List<ChatParticipant>
                {
                    new() { UserId = sender.Id, JoinedAt = DateTimeOffset.UtcNow },
                    new() { UserId = reader.Id, JoinedAt = DateTimeOffset.UtcNow }
                }
            };

            context.Chats.Add(chat);
            await context.SaveChangesAsync();

            chatId = chat.Id;

            var message = new Message
            {
                ChatId = chatId,
                SenderId = senderId,
                Content = "Read me twice",
                SentAt = DateTimeOffset.UtcNow
            };

            context.Messages.Add(message);
            await context.SaveChangesAsync();

            messageId = message.Id;
        }

        using var serviceScope = _factory.CreateScope();
        var chatService = serviceScope.ServiceProvider.GetRequiredService<IChatService>();

        // Act
        var first = await chatService.MarkAsRead(chatId, messageId, readerId);
        var second = await chatService.MarkAsRead(chatId, messageId, readerId);

        // Assert
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first.SenderId, second.SenderId);
    }

    [Fact]
    public async Task MarkAsRead_SecondLaterMessage_UpdatesReadStateAgain()
    {
        // Arrange
        Guid chatId;
        Guid senderId;
        Guid readerId;
        Guid firstMessageId;
        Guid secondMessageId;

        using (var scope = _factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

            var sender = new User
            {
                Email = "sender@example.com",
                Username = "sender@example.com",
                IsEmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            var reader = new User
            {
                Email = "reader@example.com",
                Username = "reader@example.com",
                IsEmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            context.Users.AddRange(sender, reader);
            await context.SaveChangesAsync();

            senderId = sender.Id;
            readerId = reader.Id;

            var chat = new Chat
            {
                Type = ChatType.Direct,
                CreatedAt = DateTimeOffset.UtcNow,
                Participants = new List<ChatParticipant>
                {
                    new() { UserId = sender.Id, JoinedAt = DateTimeOffset.UtcNow },
                    new() { UserId = reader.Id, JoinedAt = DateTimeOffset.UtcNow }
                }
            };

            context.Chats.Add(chat);
            await context.SaveChangesAsync();

            chatId = chat.Id;

            var firstMessage = new Message
            {
                ChatId = chatId,
                SenderId = senderId,
                Content = "First",
                SentAt = DateTimeOffset.UtcNow
            };

            var secondMessage = new Message
            {
                ChatId = chatId,
                SenderId = senderId,
                Content = "Second",
                SentAt = DateTimeOffset.UtcNow.AddSeconds(1)
            };

            context.Messages.AddRange(firstMessage, secondMessage);
            await context.SaveChangesAsync();

            firstMessageId = firstMessage.Id;
            secondMessageId = secondMessage.Id;
        }

        using var serviceScope = _factory.CreateScope();
        var chatService = serviceScope.ServiceProvider.GetRequiredService<IChatService>();

        // Act
        await chatService.MarkAsRead(chatId, firstMessageId, readerId);
        var result = await chatService.MarkAsRead(chatId, secondMessageId, readerId);

        // Assert
        Assert.Equal(senderId, result.SenderId);

        using (var verifyScope = _factory.CreateScope())
        {
            var context = verifyScope.ServiceProvider.GetRequiredService<ApplicationContext>();

            var participant = await context.ChatParticipants
                .SingleAsync(cp => cp.ChatId == chatId && cp.UserId == readerId);

            Assert.Equal(secondMessageId, participant.LastReadMessageId);
            Assert.NotNull(participant.LastReadAt);
        }
    }

    [Fact]
    public async Task MarkAsRead_MessageExistsInAnotherChat_ThrowsNotFoundException()
    {
        // Arrange
        Guid targetChatId;
        Guid otherChatId;
        Guid readerId;
        Guid messageId;

        using (var scope = _factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

            var sender = new User
            {
                Email = "sender@example.com",
                Username = "sender@example.com",
                IsEmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            var reader = new User
            {
                Email = "reader@example.com",
                Username = "reader@example.com",
                IsEmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            var otherUser = new User
            {
                Email = "other@example.com",
                Username = "other@example.com",
                IsEmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            context.Users.AddRange(sender, reader, otherUser);
            await context.SaveChangesAsync();

            readerId = reader.Id;

            var targetChat = new Chat
            {
                Type = ChatType.Direct,
                CreatedAt = DateTimeOffset.UtcNow,
                Participants = new List<ChatParticipant>
                {
                    new() { UserId = reader.Id, JoinedAt = DateTimeOffset.UtcNow },
                    new() { UserId = sender.Id, JoinedAt = DateTimeOffset.UtcNow }
                }
            };

            var otherChat = new Chat
            {
                Type = ChatType.Direct,
                CreatedAt = DateTimeOffset.UtcNow,
                Participants = new List<ChatParticipant>
                {
                    new() { UserId = sender.Id, JoinedAt = DateTimeOffset.UtcNow },
                    new() { UserId = otherUser.Id, JoinedAt = DateTimeOffset.UtcNow }
                }
            };

            context.Chats.AddRange(targetChat, otherChat);
            await context.SaveChangesAsync();

            targetChatId = targetChat.Id;
            otherChatId = otherChat.Id;

            var message = new Message
            {
                ChatId = otherChatId,
                SenderId = sender.Id,
                Content = "Message from another chat",
                SentAt = DateTimeOffset.UtcNow
            };

            context.Messages.Add(message);
            await context.SaveChangesAsync();

            messageId = message.Id;
        }

        using var serviceScope = _factory.CreateScope();
        var chatService = serviceScope.ServiceProvider.GetRequiredService<IChatService>();

        // Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            chatService.MarkAsRead(targetChatId, messageId, readerId));
    }
    
    public Task DisposeAsync() => Task.CompletedTask;
}