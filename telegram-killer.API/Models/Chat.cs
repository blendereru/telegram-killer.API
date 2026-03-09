namespace telegram_killer.API.Models;

public class Chat
{
    public Guid Id { get; set; }
    public Guid ParticipantAId { get; set; }
    public User ParticipantA { get; set; }
    public Guid ParticipantBId { get; set; }
    public User ParticipantB { get; set; }
    public Guid LastMessageId { get; set; }
    public Message LastMessage { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}