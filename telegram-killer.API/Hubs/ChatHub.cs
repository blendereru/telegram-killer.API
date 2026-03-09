using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using telegram_killer.API.Data;

namespace telegram_killer.API.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly ApplicationContext _applicationContext;
    private readonly ILogger<ChatHub> _logger;
    public ChatHub(ApplicationContext applicationContext, ILogger<ChatHub> logger)
    {
        _applicationContext = applicationContext;
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

    public async Task SendMessage(string to, string message)
    {
        var from = Context.UserIdentifier;
        
        await Clients.User(to).SendAsync("ReceiveMessage", from, message);
        
        _logger.LogInformation("Message dispatched: From={Sender}, To={Recipient}, Length={Length}", 
            from, to, message.Length);
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception != null)
        {
            _logger.LogError(exception, "User disconnected from ChatHub due to exception. UserId: {UserId}", Context.UserIdentifier);
        }
        
        _logger.LogInformation("User disconnected from ChatHub. UserId: {UserId}", Context.UserIdentifier);
        return base.OnDisconnectedAsync(exception);
    }
}