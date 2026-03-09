using telegram_killer.API.Models;

namespace telegram_killer.API.Services.Interfaces;

public interface IChatService
{
    Task<Chat> CreateDirectChatAsync(Guid userA, Guid userB);
    
    Task<Message> StoreMessageAsync(Guid chatId, Guid senderId, string content);
}