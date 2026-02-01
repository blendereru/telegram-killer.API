using Microsoft.EntityFrameworkCore;
using telegram_killer.API.Data;
using telegram_killer.API.Models;
using telegram_killer.API.Services.Interfaces;

namespace telegram_killer.API.Services;

public class AccountService : IAccountService
{
    private readonly ApplicationContext _applicationContext;
    private readonly ILogger<AccountService> _logger;
    private readonly IEmailSenderService _emailSenderService;
    private readonly IHasherService _hasherService;
    
    public AccountService(ApplicationContext applicationContext, ILogger<AccountService> logger,
        IEmailSenderService emailSenderService, IHasherService hasherService)
    {
        _applicationContext = applicationContext;
        _logger = logger;
        _emailSenderService = emailSenderService;
        _hasherService = hasherService;
    }
    
    public async Task<User> RegisterUserAsync(string email)
    {
        var newUser = new User
        {
            Email = email,
            Username = email,
            IsEmailConfirmed = false,
            RegisteredAt = DateTimeOffset.UtcNow
        };
        _applicationContext.Users.Add(newUser);
        await _applicationContext.SaveChangesAsync();
        
        _logger.LogInformation("New user registered. UserId: {UserId}", newUser.Id);
        
        await _emailSenderService.SendEmailConfirmationCodeAsync(newUser.Email);
        return newUser;
    }

    public async Task ConfirmEmailAsync(Guid userId, string confirmationCode)
    {
        var code = await _applicationContext.EmailConfirmationCodes
            .Where(c =>
                c.UserId == userId &&
                c.ExpiresAt > DateTimeOffset.UtcNow)
            .OrderByDescending(c => c.CreatedAt)
            .Include(c => c.User)
            .FirstOrDefaultAsync();

        if (code == null)
        {
            _logger.LogWarning(
                "Email confirmation failed: code not found. UserId={UserId}",
                userId);
            return;
        }

        if (code.User.IsEmailConfirmed)
        {
            _logger.LogInformation(
                "Email already confirmed. UserId={UserId}",
                userId);
            return;
        }

        if (!_hasherService.VerifyConfirmationCode(
                confirmationCode,
                code.ConfirmationCodeHash))
        {
            _logger.LogWarning(
                "Email confirmation failed: invalid code. UserId={UserId}",
                userId);
            return;
        }
        
        code.User.IsEmailConfirmed = true;
        
        _applicationContext.EmailConfirmationCodes.RemoveRange(
            _applicationContext.EmailConfirmationCodes
                .Where(c => c.UserId == userId));

        await _applicationContext.SaveChangesAsync();

        _logger.LogInformation(
            "Email confirmed successfully. UserId={UserId}",
            userId);
    }
}