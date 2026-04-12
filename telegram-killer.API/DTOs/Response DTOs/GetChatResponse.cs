using telegram_killer.API.Models;

namespace telegram_killer.API.DTOs.Response_DTOs;

public class GetChatResponse
{
    public Guid ChatId { get; set; }
    public string Name { get; set; }
    public ChatType Type { get; set; }
    public List<ChatParticipantDto>? Participants { get; set; }
}