using telegram_killer.API.Models;

namespace telegram_killer.API.DTOs.Response_DTOs;

public class CreateChatResponse
{
    public Guid ChatId { get; set; }
    public ChatType Type { get; set; }
    public string? Name { get; set; }
    public List<ChatParticipantDto> Participants { get; set; }
    public bool IsNew { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}