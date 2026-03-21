using telegram_killer.API.DTOs.Response_DTOs;
using telegram_killer.API.Models;

namespace telegram_killer.API.Services.Interfaces;

public interface IChatService
{
    Task<Chat> CreateDirectChatAsync(Guid userA, Guid userB);
    
    Task<GetChatMessagesDto> GetMessagesAsync(Guid chatId, Guid userId);
    
    Task<Message> StoreMessageAsync(Guid chatId, Guid senderId, string content);
    Task<bool> UserCanAccessChat(Guid userId, Guid chatId);
    Task MarkAsRead(Guid chatId, Guid messageId, Guid userId);
}