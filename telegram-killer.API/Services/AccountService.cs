using Microsoft.EntityFrameworkCore;
using telegram_killer.API.Data;
using telegram_killer.API.DTOs.Response_DTOs;
using telegram_killer.API.Exceptions;
using telegram_killer.API.Models;
using telegram_killer.API.Services.Interfaces;

namespace telegram_killer.API.Services;

public class AccountService : IAccountService
{
    private readonly ApplicationContext _applicationContext;
    private readonly ILogger<AccountService> _logger;
    private readonly IEmailSenderService _emailSenderService;
    private readonly IHasherService _hasherService;
    private readonly ITokensProviderService _tokensProviderService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    
    public AccountService(ApplicationContext applicationContext, ILogger<AccountService> logger,
        IEmailSenderService emailSenderService, IHasherService hasherService,
        ITokensProviderService tokensProviderService, IHttpContextAccessor httpContextAccessor)
    {
        _applicationContext = applicationContext;
        _logger = logger;
        _emailSenderService = emailSenderService;
        _hasherService = hasherService;
        _tokensProviderService = tokensProviderService;
        _httpContextAccessor = httpContextAccessor;
    }
    
    public async Task<User> RegisterUserAsync(string email)
    {
        var existingUser = await _applicationContext.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (existingUser != null)
        {
            _logger.LogWarning("User registration failed: user with the same email already exists. UserId: {UserId}", existingUser.Id);
            throw new AlreadyExistsException("User with the same email already exists");
        }
        
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

    public async Task<AuthResult> LoginUserAsync(string email)
    {
        var user = await _applicationContext.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user == null)
        {
            _logger.LogWarning("Login failed: user with the specified email not found");
            throw new NotFoundException("User with the specified email not found");
        }

        var authResult = new AuthResult
        {
            AccessToken = _tokensProviderService.GenerateAccessToken(user),
            RefreshToken = _tokensProviderService.GenerateRefreshToken()
        };

        await CreateRefreshSessionAsync(user.Id, authResult);
        
        return authResult;
    }

    public async Task<AuthResult> ConfirmEmailAndSignInAsync(Guid userId, string confirmationCode)
    {
        await ConfirmEmailAsync(userId, confirmationCode);

        var user = await _applicationContext.Users.FindAsync(userId);

        if (user == null)
        {
            _logger.LogWarning("Signing in user failed: user not found. UserId: {UserId}", userId);
            throw new NotFoundException($"The user with the id {userId} was not found");
        }
        
        var authResult = new AuthResult
        {
            AccessToken = _tokensProviderService.GenerateAccessToken(user),
            RefreshToken = _tokensProviderService.GenerateRefreshToken()
        };

        await CreateRefreshSessionAsync(userId, authResult);
        return authResult;
    }
    
    private async Task ConfirmEmailAsync(Guid userId, string confirmationCode)
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

    private async Task CreateRefreshSessionAsync(Guid userId, AuthResult result)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        
        var refreshSession = new RefreshSession
        {
            RefreshToken = result.RefreshToken,
            UA = httpContext.Request.Headers.UserAgent.ToString(),
            Ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
            UserId = userId,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            CreatedAt = DateTimeOffset.UtcNow
        };
        
        _applicationContext.RefreshSessions.Add(refreshSession);
        await _applicationContext.SaveChangesAsync();
        
        _logger.LogInformation("Refresh session created successfully for user with Id {UserId}", userId);
    }

}