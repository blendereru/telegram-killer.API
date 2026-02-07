using telegram_killer.API.DTOs.Response_DTOs;
using telegram_killer.API.Models;

namespace telegram_killer.API.Services.Interfaces;

public interface IAccountService
{
    Task<User> RegisterUserAsync(string email);
    Task LoginUserAsync(string email);
    Task<AuthResult> ConfirmEmailAndSignInAsync(Guid userId, string confirmationCode);
    Task<AuthResult> RefreshTokensAsync(string refreshToken);
}