using telegram_killer.API.Models;

namespace telegram_killer.API.Services.Interfaces;

public interface IEmailSenderService
{
    Task SendEmailConfirmationCodeAsync(User user);
}