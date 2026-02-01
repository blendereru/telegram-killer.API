namespace telegram_killer.API.Services.Interfaces;

public interface IHasherService
{
    string HashEmailForLogging(string email);
    string HashConfirmationCode(string code);
    bool VerifyConfirmationCode(string code, string storedHash);
}