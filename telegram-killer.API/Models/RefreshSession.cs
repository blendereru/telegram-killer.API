namespace telegram_killer.API.Models;

public class RefreshSession
{
    public Guid Id { get; set; }
    public string RefreshToken { get; set; }
    public string UA { get; set; }
    public string Ip { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; }
}