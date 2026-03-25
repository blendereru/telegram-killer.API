using telegram_killer.API.DTOs.Response_DTOs;
using telegram_killer.API.Models;

namespace telegram_killer.API.Services.Interfaces;

public interface IChatService
{
    Task<Chat> CreateDirectChat(Guid requesterId, Guid requestedId);
    
    Task<GetChatMessagesDto> GetMessages(Guid chatId, Guid userId);
    
    Task<Message> StoreMessage(Guid chatId, Guid senderId, string content);
    Task<bool> UserCanAccessChat(Guid userId, Guid chatId);
    Task<MarkAsReadResponse> MarkAsRead(Guid chatId, Guid messageId, Guid userId);
}