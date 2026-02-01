namespace telegram_killer.API.Services.Interfaces;

public interface IEmailSenderService
{
    Task SendEmailConfirmationCodeAsync(string email);
}