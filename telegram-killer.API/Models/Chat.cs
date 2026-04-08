namespace telegram_killer.API.Models;

public class Chat
{
    public Guid Id { get; set; }
    public ChatType Type { get; set; }
    public string? Name {get; set;}
    public Guid? LastMessageId { get; set; }
    public Message? LastMessage { get; set; }
    public ICollection<ChatParticipant> Participants { get; set; } = new List<ChatParticipant>();
    public DateTimeOffset CreatedAt { get; set; }
}