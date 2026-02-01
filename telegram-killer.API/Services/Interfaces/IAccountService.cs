using telegram_killer.API.Models;

namespace telegram_killer.API.Services.Interfaces;

public interface IAccountService
{
    Task<User> RegisterUserAsync(string email);
    Task ConfirmEmailAsync(Guid userId, string confirmationCode);
}