namespace telegram_killer.API.DTOs.Response_DTOs;

public class MarkAsReadResponse
{
    public Guid SenderId { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
}