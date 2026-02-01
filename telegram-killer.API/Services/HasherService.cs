using telegram_killer.API.Options;
using telegram_killer.API.Services.Interfaces;

namespace telegram_killer.API.Services;

using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

public class HasherService : IHasherService
{
    private readonly byte[] _confirmationKey;

    public HasherService(IOptions<SecuritySettings> securitySettings)
    {
        _confirmationKey = Encoding.UTF8.GetBytes(
            securitySettings.Value.ConfirmationCodeSecret);
    }

    public string HashEmailForLogging(string email)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes)[..16];
    }

    public string HashConfirmationCode(string code)
    {
        var bytes = Encoding.UTF8.GetBytes(code);

        using var hmac = new HMACSHA256(_confirmationKey);
        return Convert.ToHexString(hmac.ComputeHash(bytes));
    }

    public bool VerifyConfirmationCode(string code, string storedHash)
    {
        var computed = HashConfirmationCode(code);

        return CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(computed),
            Convert.FromHexString(storedHash));
    }
}
