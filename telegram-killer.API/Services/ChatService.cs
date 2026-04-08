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

    public async Task<Chat> CreateDirectChat(Guid requesterId, Guid requestedId)
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
            _logger.LogWarning(
                "Chat creation failed: Requested user does not exist or has non-confirmed email. RequesterId: {RequesterId}",
                requesterId);
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
            _logger.LogInformation(
                "Chat already exists between users. Returning existing chat. ChatId: {ChatId}, RequesterId: {RequesterId}, RequestedId: {RequestedId}",
                existingChat.Id, requesterId, requestedId);
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
            _logger.LogInformation(
                "Successfully created a new direct chat. ChatId: {ChatId}, RequesterId: {RequesterId}, RequestedId: {RequestedId}",
                chat.Id, requesterId, requestedId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error occurred while saving new chat to database. RequesterId: {RequesterId}, RequestedId: {RequestedId}",
                requesterId, requestedId);
            throw;
        }

        return chat;
    }

    public async Task<GetChatMessagesResponse> GetMessages(Guid chatId, Guid userId)
    {
        var participants = await _applicationContext.ChatParticipants
            .Where(cp => cp.ChatId == chatId)
            .Select(cp => new
            {
                cp.UserId,
                cp.LastReadMessageId
            })
            .ToListAsync();

        if (participants.Count == 0)
        {
            _logger.LogWarning(
                "Messages retrieval failed: Chat not found or has no participants. Chat={ChatId}",
                chatId);

            throw new NotFoundException($"Chat {chatId} not found.");
        }

        var currentUser = participants.FirstOrDefault(p => p.UserId == userId);

        if (currentUser == null)
        {
            _logger.LogWarning(
                "Message retrieval failed: Unauthorized access attempt. User={UserId} Chat={ChatId}",
                userId, chatId);

            throw new ForbiddenException("User is not a participant of this chat.");
        }

        var messages = await _applicationContext.Messages
            .Where(m => m.ChatId == chatId)
            .OrderBy(m => m.SentAt)
            .Select(m => new MessageDto
            {
                Id = m.Id,
                SenderId = m.SenderId,
                Content = m.Content,
                SentAt = m.SentAt
            })
            .ToListAsync();

        var otherParticipantReadStates = participants
            .Where(p => p.UserId != userId)
            .Select(p => new ParticipantReadDto
            {
                UserId = p.UserId,
                LastReadMessageId = p.LastReadMessageId
            })
            .ToList();

        _logger.LogInformation(
            "Messages retrieved successfully. Chat={ChatId}, User={UserId}, Messages={MessageCount}, Participants={ParticipantCount}",
            chatId,
            userId,
            messages.Count,
            participants.Count);

        return new GetChatMessagesResponse
        {
            ChatId = chatId,
            Messages = messages,
            LastReadMessageId = currentUser.LastReadMessageId,
            OtherParticipantReadStates = otherParticipantReadStates
        };
    }

    public async Task<Message> StoreMessage(Guid chatId, Guid senderId, string content)
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

        var affectedRows = await _applicationContext.ChatParticipants
            .Where(cp => cp.ChatId == chatId && cp.UserId == senderId &&
                         (cp.LastReadMessageId == null || cp.LastReadMessageId < message.Id))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(cp => cp.LastReadMessageId, message.Id)
                .SetProperty(cp => cp.LastReadAt, DateTimeOffset.UtcNow)
            );

        if (affectedRows > 0)
        {
            _logger.LogInformation(
                "LastRead updated after sending message. Chat={ChatId} User={UserId} MessageId={MessageId}",
                chatId, senderId, message.Id);
        }
        else
        {
            _logger.LogDebug(
                "LastRead not updated (already ahead). Chat={ChatId} User={UserId} MessageId={MessageId}",
                chatId, senderId, message.Id);
        }

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

    public async Task<GetChatResponse> GetChat(Guid chatId, Guid userId)
    {
        var chat = await _applicationContext.Chats
            .Where(c => c.Id == chatId &&
                        c.Participants.Any(p => p.UserId == userId))
            .Select(c => new GetChatResponse
            {
                ChatId = c.Id,
                Type = c.Type,
                Name = c.Type == ChatType.Direct
                    ? c.Participants
                        .Where(p => p.UserId != userId)
                        .Select(p => p.User.Username)
                        .FirstOrDefault()!
                    : c.Name!
            })
            .FirstOrDefaultAsync();
        
        if (chat == null)
        {
            _logger.LogWarning(
                "Chat retrieval failed: Chat not found or user is not a participant. ChatId: {ChatId}, UserId: {UserId}",
                chatId, userId);
            
            throw new NotFoundException("Chat not found");
        }

        if (chat.Type != ChatType.Channel)
        {
            chat.Participants = await _applicationContext.ChatParticipants
                .Where(p => p.ChatId == chatId)
                .Select(p => new ChatParticipantDto
                {
                    Id = p.UserId,
                    Username = p.User.Username
                })
                .ToListAsync();
        }
        
        _logger.LogInformation("Chat retrieved successfully. ChatId: {ChatId}, UserId: {UserId}, Participants: {ParticipantCount}",
            chat.ChatId, userId, chat.Participants?.Count);
        
        return chat;
    }
}