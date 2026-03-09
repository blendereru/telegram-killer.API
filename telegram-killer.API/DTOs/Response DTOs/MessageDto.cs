namespace telegram_killer.API.DTOs.Response_DTOs;

public class MessageDto
{
    public Guid Id { get; set; }

    public Guid ChatId { get; set; }

    public Guid SenderId { get; set; }

    public string Content { get; set; }

    public DateTimeOffset SentAt { get; set; }
}