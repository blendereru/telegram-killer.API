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

    public async Task<GetChatMessagesDto> GetMessagesAsync(Guid chatId, Guid userId)
    {
        var chatData = await _applicationContext.ChatParticipants
            .Where(cp => cp.ChatId == chatId && cp.UserId == userId)
            .Select(cp => new
            {
                cp.LastReadMessageId,
                Messages = _applicationContext.Messages
                    .Where(m => m.ChatId == chatId)
                    .OrderBy(m => m.SentAt)
                    .Select(m => new MessageDto
                    {
                        Id = m.Id,
                        SenderId = m.SenderId,
                        Content = m.Content,
                        SentAt = m.SentAt
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (chatData == null)
        {
            _logger.LogWarning(
                "Unauthorized chat access attempt. User={UserId} Chat={ChatId}",
                userId, chatId);

            throw new ForbiddenException("User is not a participant of this chat.");
        }

        return new GetChatMessagesDto
        {
            ChatId = chatId,
            Messages = chatData.Messages,
            LastReadMessageId = chatData.LastReadMessageId
        };
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

    public async Task<bool> UserCanAccessChat(Guid userId, Guid chatId)
    {
        var isParticipant = await _applicationContext.ChatParticipants
            .AnyAsync(cp => cp.ChatId == chatId && cp.UserId == userId);

        return isParticipant;
    }

    public async Task<MarkAsReadResponse> MarkAsRead(Guid chatId, Guid messageId, Guid userId)
    {
        var now = DateTimeOffset.UtcNow;

        var message = await _applicationContext.Messages
            .Where(m => m.Id == messageId && m.ChatId == chatId)
            .Select(m => new
            {
                m.Id,
                m.ChatId,
                m.SenderId
            })
            .FirstOrDefaultAsync();

        if (message == null)
        {
            _logger.LogWarning(
                "Mark message as read failed: message not found or not in chat. ChatId: {ChatId}, MessageId: {MessageId}",
                chatId, messageId);

            throw new NotFoundException("Message not found");
        }
        
        var affectedRows = await _applicationContext.ChatParticipants
            .Where(c =>
                c.ChatId == chatId &&
                c.UserId == userId &&
                (c.LastReadMessageId == null || c.LastReadMessageId != messageId))
            .ExecuteUpdateAsync(setters =>
                setters
                    .SetProperty(c => c.LastReadMessageId, messageId)
                    .SetProperty(c => c.LastReadAt, now));

        if (affectedRows == 0)
        {
            var exists = await _applicationContext.ChatParticipants
                .AnyAsync(c => c.ChatId == chatId && c.UserId == userId);

            if (!exists)
            {
                _logger.LogWarning(
                    "Mark message as read failed: participant doesn't exist. ChatId: {ChatId}, UserId: {UserId}",
                    chatId, userId);

                throw new NotFoundException("User or chat is not found");
            }

            _logger.LogInformation(
                "Mark as read skipped (already up to date). MessageId: {MessageId}, ChatId: {ChatId}, UserId: {UserId}",
                messageId, chatId, userId);
        }
        else
        {
            _logger.LogInformation(
                "Marked message as read. MessageId: {MessageId}, ChatId: {ChatId}, UserId: {UserId}",
                messageId, chatId, userId);
        }
        
        return new MarkAsReadResponse
        {
            ReadAt = now,
            SenderId = message.SenderId
        };
    }
}