using System.Security.Cryptography;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using telegram_killer.API.Data;
using telegram_killer.API.Models;
using telegram_killer.API.Options;
using telegram_killer.API.Services.Interfaces;

namespace telegram_killer.API.Services;

public class EmailSenderService : IEmailSenderService
{
    private readonly ApplicationContext _applicationContext;
    private readonly ILogger<EmailSenderService> _logger;
    private readonly EmailSettings _emailSettings;
    private readonly IHasherService _hasherService;
    
    public EmailSenderService(ApplicationContext applicationContext, ILogger<EmailSenderService> logger,
        IOptions<EmailSettings> emailSettingsOptions, IHasherService hasherService)
    {
        _applicationContext = applicationContext;
        _logger = logger;
        _emailSettings = emailSettingsOptions.Value;
        _hasherService = hasherService;
    }
    
    public async Task SendEmailConfirmationCodeAsync(User user)
    {
        var code = RandomNumberGenerator.GetInt32(1000000, 10000000).ToString();
        
        var message = new MimeMessage();
        
        message.From.Add(new MailboxAddress(
            _emailSettings.FromName,
            _emailSettings.FromEmail));
        
        message.To.Add(MailboxAddress.Parse(user.Email));
        
        message.Body = new BodyBuilder
        {
            HtmlBody = $@"
                <h2>Email Confirmation</h2>
                <p>Your confirmation code is:</p>
                <h3>{code}</h3>
                <p>This code will expire in 10 minutes.</p>
            ",
            TextBody = $"Your confirmation code is: {code}"
        }.ToMessageBody();

        try
        {
            using var client = new SmtpClient();

            await client.ConnectAsync(
                _emailSettings.Host,
                _emailSettings.Port,
                SecureSocketOptions.StartTls);

            await client.AuthenticateAsync(
                _emailSettings.Username,
                _emailSettings.Password);
            
            var confirmationCode = new EmailConfirmationCode
            {
                UserId = user.Id,
                ConfirmationCodeHash = _hasherService.HashConfirmationCode(code),
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10)
            };
            
            _applicationContext.EmailConfirmationCodes.Add(confirmationCode);
            await _applicationContext.SaveChangesAsync();
            
            _logger.LogInformation("Saved email confirmation code for UserId: {UserId}", user.Id);
            
            await client.SendAsync(message);

            await client.DisconnectAsync(true);
            
            _logger.LogInformation("Email confirmation code sent to user with Id {UserId}", user.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email confirmation code to user with Id {UserId}", user.Id);
            throw;
        }
    }
}