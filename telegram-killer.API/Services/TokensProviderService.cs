using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using telegram_killer.API.Models;
using telegram_killer.API.Options;
using telegram_killer.API.Services.Interfaces;

namespace telegram_killer.API.Services;

public class TokensProviderService : ITokensProviderService
{
    private readonly JwtConfigurationOptions _jwtConfigurationOptions;
    private readonly ILogger<TokensProviderService> _logger;
    public TokensProviderService(IOptions<JwtConfigurationOptions> options, ILogger<TokensProviderService> logger)
    {
        _jwtConfigurationOptions = options.Value;
        _logger = logger;
    }
    
    public string GenerateAccessToken(User user)
    {
        var credentials = new SigningCredentials(_jwtConfigurationOptions.GetSymmetricSecurityKey(),
            SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>()
        {
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim("email_confirmed", user.IsEmailConfirmed.ToString())
        };
        
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(_jwtConfigurationOptions.Lifetime),
            SigningCredentials = credentials,
            Issuer = _jwtConfigurationOptions.Issuer,
            Audience = _jwtConfigurationOptions.Audience,
        };
        
        var handler = new JsonWebTokenHandler();
        
        var token = handler.CreateToken(tokenDescriptor);

        _logger.LogInformation("Access token generated for User with Id {UserId}", user.Id);
        
        return token;
    }

    public string GenerateRefreshToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    }
}