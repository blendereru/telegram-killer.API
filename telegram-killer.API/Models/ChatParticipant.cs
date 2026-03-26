namespace telegram_killer.API.Models;

public class ChatParticipant
{
    public Guid ChatId { get; set; }
    public Chat Chat { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; }
    public DateTimeOffset JoinedAt { get; set; }

    public Guid? LastReadMessageId { get; set; }
    public DateTimeOffset? LastReadAt { get; set; }
}