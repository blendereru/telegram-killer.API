using telegram_killer.API.Models;
using telegram_killer.API.Services.Interfaces;

namespace telegram_killer.API.IntegrationTests.Fakes;

public class FakeEmailSenderService : IEmailSenderService
{
    public Task SendEmailConfirmationCodeAsync(User user)
    {
        return Task.CompletedTask;
    }
}