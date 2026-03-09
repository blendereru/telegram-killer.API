namespace telegram_killer.API.DTOs.Response_DTOs;

public class CreateChatResponse
{
    public Guid ChatId { get; set; }
    public List<Guid> Participants { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}