namespace telegram_killer.API.DTOs.Response_DTOs;

public class RegisterUserResponse
{
    public Guid UserId { get; set; }
    public DateTimeOffset RegisteredAt { get; set; }
    public string Message { get; set; }
}