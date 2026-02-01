namespace telegram_killer.API.Models;

public class EmailConfirmationCode
{
    public Guid Id { get; set; }
    public string ConfirmationCodeHash { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    
    public Guid UserId { get; set; }
    public User User { get; set; }
}