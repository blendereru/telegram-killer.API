using telegram_killer.API.DTOs.Response_DTOs;
using telegram_killer.API.Models;

namespace telegram_killer.API.Services.Interfaces;

public interface IChatService
{
    Task<CreateChatResponse> CreateDirectChat(Guid requesterId, Guid requestedId);
    Task<GetChatMessagesResponse> GetMessages(Guid chatId, Guid userId);
    Task<Message> StoreMessage(Guid chatId, Guid senderId, string content);
    Task<bool> UserCanAccessChat(Guid userId, Guid chatId);
    Task<MarkAsReadResponse> MarkAsRead(Guid chatId, Guid messageId, Guid userId);
    Task<GetChatResponse> GetChat(Guid chatId, Guid userId);
}