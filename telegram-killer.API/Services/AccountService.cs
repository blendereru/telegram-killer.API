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
        
        await _emailSenderService.SendEmailConfirmationCodeAsync(newUser);
        return newUser;
    }

    public async Task LoginUserAsync(string email)
    {
        var user = await _applicationContext.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user == null)
        {
            _logger.LogWarning("Login failed: user with the specified email not found");
            throw new NotFoundException("User with the specified email not found");
        }

        _logger.LogInformation("User with Id {UserId} is logging in...", user.Id);
        
        await _emailSenderService.SendEmailConfirmationCodeAsync(user);
        
        _logger.LogInformation("User with Id {UserId} has successfully logged in. Waiting for email confirmation", user.Id);
    }

    public async Task<AuthResult> RefreshTokensAsync(string refreshToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            _logger.LogWarning("HttpContext object not found");
            throw new ApplicationException("Http request context not found");
        }
        
        var refreshSession = await _applicationContext.RefreshSessions
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.RefreshToken == refreshToken);
        
        if (refreshSession == null || refreshSession.ExpiresAt < DateTimeOffset.UtcNow)
        {
            //var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            //var ua = httpContext.Request.Headers.UserAgent.ToString();
            
            _logger.LogWarning("Refreshing tokens failed: refresh token is expired or not found");
            
            throw new UnauthorizedException("Invalid or expired session. Re-authenticate to continue.");
        }
        
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        
        if (refreshSession.Ip != ip)
        {
            _logger.LogWarning(
                "Refresh token IP mismatch. UserId: {UserId}, TokenIp: {TokenIp}, RequestIp: {RequestIp}",
                refreshSession.UserId,
                refreshSession.Ip,
                ip
            );
            throw new UnauthorizedException("Invalid or expired session. Re-authenticate to continue.");
        }
        
        await _applicationContext.RefreshSessions
            .Where(r => r.Id == refreshSession.Id)
            .ExecuteDeleteAsync();
        
        _logger.LogInformation(
            "Refresh session record with Id {RefreshSessionId} deleted successfully. UserId: {UserId}",
            refreshSession.Id, refreshSession.UserId);

        var authResult = new AuthResult
        {
            AccessToken = _tokensProviderService.GenerateAccessToken(refreshSession.User),
            RefreshToken = _tokensProviderService.GenerateRefreshToken()
        };

        await CreateRefreshSessionAsync(refreshSession.UserId, authResult);
        return authResult;
    }

    public async Task<AuthResult> ConfirmEmailAndSignInAsync(string email, string confirmationCode)
    {
        var user = await ConfirmEmailAsync(email, confirmationCode);
        
        var authResult = new AuthResult
        {
            AccessToken = _tokensProviderService.GenerateAccessToken(user),
            RefreshToken = _tokensProviderService.GenerateRefreshToken()
        };

        await CreateRefreshSessionAsync(user.Id, authResult);
        return authResult;
    }
    
    private async Task<User> ConfirmEmailAsync(string email, string confirmationCode)
    {
        var code = await _applicationContext.EmailConfirmationCodes
            .Include(c => c.User)
            .Where(c =>
                c.User.Email == email &&
                c.ExpiresAt > DateTimeOffset.UtcNow)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync();
        
        if (code == null)
        {
            _logger.LogWarning(
                "Email confirmation failed: code not found.");
            throw new NotFoundException("The user email or confirmation code is invalid");
        }
        
        var userId = code.User.Id;

        if (!_hasherService.VerifyConfirmationCode(
                confirmationCode,
                code.ConfirmationCodeHash))
        {
            _logger.LogWarning(
                "Email confirmation failed: invalid code. UserId={UserId}",
                userId);
            throw new NotFoundException("The user email or confirmation code is invalid");
        }
        
        code.User.IsEmailConfirmed = true;
        
        _applicationContext.EmailConfirmationCodes.RemoveRange(
            _applicationContext.EmailConfirmationCodes
                .Where(c => c.UserId == userId));

        await _applicationContext.SaveChangesAsync();

        _logger.LogInformation(
            "Email confirmed successfully. UserId={UserId}",
            userId);
        
        return code.User;
    }

    private async Task CreateRefreshSessionAsync(Guid userId, AuthResult result)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext == null)
        {
            _logger.LogWarning("HttpContext object not found");
            throw new ApplicationException("Http request context not found");
        }
        
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