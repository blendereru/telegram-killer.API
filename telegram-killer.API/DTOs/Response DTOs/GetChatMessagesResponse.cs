namespace telegram_killer.API.DTOs.Response_DTOs;

public class GetChatMessagesResponse
{
    public Guid ChatId { get; set; }
    public List<MessageDto> Messages { get; set; } = new();
    public Guid? LastReadMessageId { get; set; }
    public List<ParticipantReadDto> OtherParticipantReadStates { get; set; } = new();
}