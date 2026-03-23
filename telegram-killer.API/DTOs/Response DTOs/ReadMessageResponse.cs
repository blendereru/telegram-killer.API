namespace telegram_killer.API.DTOs.Response_DTOs;

public class ReadMessageResponse
{
    public Guid ChatId { get; set; }
    public Guid MessageId { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset ReadAt { get; set; }
}