namespace telegram_killer.API.Models;

public class User
{
    public Guid Id { get; set; }
    public string Username { get; set; }
    public string Email { get; set; }
    public bool IsEmailConfirmed { get; set; }
    public DateTimeOffset RegisteredAt { get; set; }
    public IList<Chat> Chats { get; set; } = new List<Chat>();
}