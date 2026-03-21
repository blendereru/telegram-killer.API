namespace telegram_killer.API.DTOs.Response_DTOs;

public class GetChatMessagesDto
{
    public Guid ChatId { get; set; }
    public List<MessageDto> Messages { get; set; } = new();
    public Guid? LastReadMessageId { get; set; }
}