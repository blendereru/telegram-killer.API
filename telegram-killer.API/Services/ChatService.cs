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

    public async Task<Chat> CreateDirectChatAsync(Guid requesterId, Guid requestedId)
    {
        if (requesterId == requestedId)
        {
            _logger.LogWarning("Attempted to create a chat with oneself. Requested: {RequestedId}", requestedId);
            throw new ValidationException("Cannot create a chat with yourself");
        }
        
        var targetUser = await _applicationContext.Users
            .Where(u => u.Id == requestedId)
            .Select(u => new { u.Id, u.IsEmailConfirmed })
            .FirstOrDefaultAsync();
        
        if (targetUser == null || !targetUser.IsEmailConfirmed)
        {
            _logger.LogWarning("Chat creation failed: Requested user does not exist or has non-confirmed email. RequesterId: {RequesterId}", requesterId);
            throw new NotFoundException("Requested user is not found");
        }
        
        var existingChat = await _applicationContext.Chats
            .AsNoTracking()
            .Where(c => c.Type == ChatType.Direct)
            .Where(c => c.Participants.Any(p => p.UserId == requesterId))
            .Where(c => c.Participants.Any(p => p.UserId == requestedId))
            .FirstOrDefaultAsync();

        if (existingChat != null)
        {
            _logger.LogInformation("Chat already exists between users. Returning existing chat. ChatId: {ChatId}, RequesterId: {RequesterId}, RequestedId: {RequestedId}", existingChat.Id, requesterId, requestedId);
            return existingChat;
        }
        
        var chat = new Chat
        {
            Type = ChatType.Direct,
            CreatedAt = DateTimeOffset.UtcNow,
            Participants = new List<ChatParticipant>
            {
                new() { UserId = requesterId, JoinedAt = DateTimeOffset.UtcNow },
                new() { UserId = requestedId, JoinedAt = DateTimeOffset.UtcNow }
            }
        };

        _applicationContext.Chats.Add(chat);

        try
        {
            await _applicationContext.SaveChangesAsync();
            _logger.LogInformation("Successfully created a new direct chat. ChatId: {ChatId}, RequesterId: {RequesterId}, RequestedId: {RequestedId}", chat.Id, requesterId, requestedId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while saving new chat to database. RequesterId: {RequesterId}, RequestedId: {RequestedId}", requesterId, requestedId);
            throw;
        }

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