namespace telegram_killer.API.Models;

public class Message
{
    public Guid Id { get; set; }
    public Guid ChatId { get; set; }
    public Chat Chat { get; set; }
    public Guid SenderId { get; set; }
    public User Sender { get; set; }
    public string Content { get; set; }
    public DateTimeOffset SentAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}