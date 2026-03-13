using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using telegram_killer.API.Data;
using telegram_killer.API.DTOs.Response_DTOs;
using telegram_killer.API.Exceptions;
using telegram_killer.API.Models;
using telegram_killer.API.Services.Interfaces;

namespace telegram_killer.API.Services;

public class ChatService : IChatService
{
    private readonly ApplicationContext _applicationContext;
    private readonly ILogger<ChatService> _logger;
    
    public ChatService(ApplicationContext applicationContext, ILogger<ChatService> logger)
    {
        _applicationContext = applicationContext;
        _logger = logger;
    }

    public async Task<Chat> CreateDirectChatAsync(Guid userA, Guid userB)
    {
        if (userA == userB)
        {
            _logger.LogWarning("Initiated chat creation with yourself. UserId: {UserId}", userA);
            var exception = new ValidationException("Cannot create a chat with yourself");
            exception.Data["UserA"] = "UserA id can't be equal to UserB";
            throw exception;
        }
        
        var existingChat = await _applicationContext.Chats
            .AsNoTracking()
            .Where(c => c.Type == ChatType.Direct)
            .Where(c => c.Participants.Any(p => p.UserId == userA))
            .Where(c => c.Participants.Any(p => p.UserId == userB))
            .Include(c => c.Participants)
            .FirstOrDefaultAsync();

        if (existingChat != null)
        {
            _logger.LogInformation("Chat creation discarded: Chat with the user already exists. ChatId: {ChatId}", existingChat.Id);
            return existingChat;
        }

        var chat = new Chat
        {
            Id = Guid.NewGuid(),
            Type = ChatType.Direct,
            CreatedAt = DateTimeOffset.UtcNow
        };
        
        _applicationContext.Chats.Add(chat);

        _applicationContext.ChatParticipants.AddRange(
            new ChatParticipant
            {
                ChatId = chat.Id,
                UserId = userA,
                JoinedAt = DateTimeOffset.UtcNow
            },
            new ChatParticipant
            {
                ChatId = chat.Id,
                UserId = userB,
                JoinedAt = DateTimeOffset.UtcNow
            }
        );

        await _applicationContext.SaveChangesAsync();

        _logger.LogInformation("Chat successfully created: ChatId: {ChatId}", chat.Id);
        
        return chat;
    }

    public async Task<List<MessageDto>> GetMessagesAsync(Guid chatId, Guid userId)
    {
        var isParticipant = await _applicationContext.ChatParticipants
            .AsNoTracking()
            .AnyAsync(cp => cp.ChatId == chatId && cp.UserId == userId);

        if (!isParticipant)
        {
            _logger.LogWarning(
                "Unauthorized chat access attempt. User={UserId} Chat={ChatId}",
                userId, chatId);

            throw new ForbiddenException("User is not a participant of this chat.");
        }

        var messages = await _applicationContext.Messages
            .AsNoTracking()
            .Where(m => m.ChatId == chatId)
            .OrderBy(m => m.SentAt)
            .Select(m => new MessageDto
            {
                Id = m.Id,
                ChatId = m.ChatId,
                SenderId = m.SenderId,
                Content = m.Content,
                SentAt = m.SentAt
            })
            .ToListAsync();

        return messages;
    }

    public async Task<Message> StoreMessageAsync(Guid chatId, Guid senderId, string content)
    {
        var isParticipant = await _applicationContext.ChatParticipants
            .AnyAsync(cp => cp.ChatId == chatId && cp.UserId == senderId);

        if (!isParticipant)
        {
            _logger.LogWarning(
                "Unauthorized message attempt. Sender={SenderId} Chat={ChatId}",
                senderId, chatId);

            throw new UnauthorizedException("User is not a participant of this chat.");
        }

        var message = new Message
        {
            ChatId = chatId,
            SenderId = senderId,
            Content = content,
            SentAt = DateTimeOffset.UtcNow
        };

        _applicationContext.Messages.Add(message);
        await _applicationContext.SaveChangesAsync();

        _logger.LogInformation(
            "Message stored successfully. Chat={ChatId} Sender={SenderId} MessageId={MessageId}",
            chatId, senderId, message.Id);

        return message;
    }
}