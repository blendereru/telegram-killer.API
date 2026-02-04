using telegram_killer.API.Models;

namespace telegram_killer.API.Services.Interfaces;

public interface ITokensProviderService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
}