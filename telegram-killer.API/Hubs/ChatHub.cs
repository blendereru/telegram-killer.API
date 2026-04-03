using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using telegram_killer.API.DTOs.Response_DTOs;
using telegram_killer.API.Services.Interfaces;

namespace telegram_killer.API.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly IChatService _chatService;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(IChatService chatService, ILogger<ChatHub> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    public override Task OnConnectedAsync()
    {
        if (Context.UserIdentifier == null)
        {
            _logger.LogWarning("User connected with unassigned UserIdentifier in ChatHub. ConnectionId: {ConnectionId}",
                Context.ConnectionId);

            Context.Abort();

            _logger.LogInformation("Aborted user connection in ChatHub. ConnectionId: {ConnectionId}",
                Context.ConnectionId);
        }

        _logger.LogInformation("User with Id {UserId} connected to the ChatHub. ConnectionId: {ConnectionId}",
            Context.UserIdentifier, Context.ConnectionId);

        return base.OnConnectedAsync();
    }

    public async Task JoinChat(string chatId)
    {
        var chatGuid = Guid.Parse(chatId);
        var userGuid = Guid.Parse(Context.UserIdentifier!);

        if (!await _chatService.UserCanAccessChat(userGuid, chatGuid))
        {
            _logger.LogWarning("Chat join failed: user can't access chat. ChatId: {ChatId}, UserId: {UserId}", chatGuid,
                userGuid);
            throw new HubException("You are not allowed to join this chat.");
        }

        _logger.LogInformation("User with Id {UserId} joined chat {ChatId}", Context.UserIdentifier, chatId);
        await Groups.AddToGroupAsync(Context.ConnectionId, chatId);
    }

    public async Task LeaveChat(string chatId)
    {
        var userId = Context.UserIdentifier!;

        _logger.LogInformation("User with Id {UserId} left chat {ChatId}", userId, chatId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, chatId);
    }

    public async Task SendMessage(string chatId, string content)
    {
        if (!Guid.TryParse(Context.UserIdentifier, out var senderId))
        {
            throw new HubException("Invalid user identifier.");
        }

        if (!Guid.TryParse(chatId, out var chatGuid))
        {
            throw new HubException("Invalid chat id.");
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new HubException("Message cannot be empty.");
        }

        var message = await _chatService.StoreMessage(chatGuid, senderId, content);

        await Clients.Group(chatId).SendAsync("ReceiveMessage", new
        {
            message.Id,
            message.ChatId,
            message.SenderId,
            message.Content,
            message.SentAt
        });

        _logger.LogInformation(
            "Message dispatched: ChatId={ChatId} From={Sender} Length={Length}",
            chatId, senderId, content.Length);
    }

    public async Task MarkAsRead(string chatId, string messageId)
    {
        var userId = Guid.Parse(Context.UserIdentifier!);
        var chatGuid = Guid.Parse(chatId);
        var messageGuid = Guid.Parse(messageId);

        var result = await _chatService.MarkAsRead(chatGuid, messageGuid, userId);

        await Clients.User(result.SenderId.ToString()).SendAsync("MessageRead", new ReadMessageResponse
        {
            ChatId = chatGuid,
            MessageId = messageGuid,
            UserId = userId,
            ReadAt = result.ReadAt ?? DateTimeOffset.UtcNow // refactor later
        });

        _logger.LogInformation("Message was read by user. UserId: {UserId}, ChatId: {ChatId}, MessageId: {MessageId}",
            userId, chatGuid, messageGuid);
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception != null)
        {
            _logger.LogError(exception, "User disconnected from ChatHub due to exception. UserId: {UserId}",
                Context.UserIdentifier);
        }

        _logger.LogInformation("User disconnected from ChatHub. UserId: {UserId}", Context.UserIdentifier);
        return base.OnDisconnectedAsync(exception);
    }
}