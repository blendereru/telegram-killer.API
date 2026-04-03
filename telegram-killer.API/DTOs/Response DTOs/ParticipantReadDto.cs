namespace telegram_killer.API.DTOs.Response_DTOs;

public class ParticipantReadDto
{
    public Guid UserId { get; set; }
    public Guid? LastReadMessageId { get; set; }
}