using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Moq;
using telegram_killer.API.Models;
using telegram_killer.API.Options;
using telegram_killer.API.Services;

namespace telegram_killer.API.UnitTests;

public class TokensProviderServiceTests
{
    private readonly TokensProviderService _service;
    private readonly JwtConfigurationOptions _options;
    private readonly Mock<ILogger<TokensProviderService>> _loggerMock;

    public TokensProviderServiceTests()
    {
        _options = new JwtConfigurationOptions
        {
            Issuer = "test-issuer",
            Audience = "test-audience",
            Lifetime = 60,
            Key = "mysupersecret_secretsecretsecretkey!123"
        };

        var options = Microsoft.Extensions.Options.Options.Create(_options);
        _loggerMock = new Mock<ILogger<TokensProviderService>>();

        _service = new TokensProviderService(options, _loggerMock.Object);
    }

    [Fact]
    public void GenerateAccessToken_NullUser_ThrowsArgumentNullException()
    {
        // Assert
        Assert.Throws<ArgumentNullException>(() => _service.GenerateAccessToken(null!));
    }
    
    [Fact]
    public void GenerateAccessToken_ShouldSetEmailConfirmedFalse()
    {
        // Arrange
        
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@test.com",
            IsEmailConfirmed = false
        };

        // Act
        var token = _service.GenerateAccessToken(user);

        // Assert
        var handler = new JsonWebTokenHandler();
        var jwt = handler.ReadJsonWebToken(token);

        var claim = jwt.Claims.First(c => c.Type == "email_confirmed");

        Assert.Equal("False", claim.Value);
    }
    
    [Fact]
    public void GenerateAccessToken_ShouldUseHmacSha256()
    {
        // Arrange
        var user = CreateUser();

        // Act
        var token = _service.GenerateAccessToken(user);

        // Assert
        var handler = new JsonWebTokenHandler();
        var jwt = handler.ReadJsonWebToken(token);

        Assert.Equal(SecurityAlgorithms.HmacSha256, jwt.GetHeaderValue<string>("alg"));
    }
    
    [Fact]
    public void GenerateAccessToken_ShouldSetSubjectToUserId()
    {
        // Arrange
        var user = CreateUser();

        // Act
        var token = _service.GenerateAccessToken(user);

        // Assert
        var handler = new JsonWebTokenHandler();
        var jwt = handler.ReadJsonWebToken(token);

        var subject = jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub);

        Assert.Equal(user.Id.ToString(), subject.Value);
    }
    
    [Fact]
    public void GenerateAccessToken_ShouldGenerateDifferentTokens()
    {
        // Arrange
        var user1 = new User
        {
            Id = Guid.NewGuid(),
            Email = "user1@test.com",
            IsEmailConfirmed = true
        };

        var user2 = new User
        {
            Id = Guid.NewGuid(),
            Email = "user1@test.com",
            IsEmailConfirmed = true
        };

        // Act
        var token1 = _service.GenerateAccessToken(user1);
        var token2 = _service.GenerateAccessToken(user2);

        // Assert
        Assert.NotEqual(token1, token2);
    }
    
    [Fact]
    public void GenerateAccessToken_ShouldThrow_WhenSecretIsInvalid()
    {
        // Arrange
        var options = Microsoft.Extensions.Options.Options.Create(new JwtConfigurationOptions
        {
            Issuer = "issuer",
            Audience = "audience",
            Lifetime = 60,
            Key = ""
        });
        
        var service = new TokensProviderService(options, _loggerMock.Object);

        var user = CreateUser();

        // Assert
        Assert.ThrowsAny<Exception>(() => service.GenerateAccessToken(user));
    }
    
    [Fact]
    public void GenerateAccessToken_ShouldReturnToken()
    {
        // Arrange
        var user = CreateUser();

        // Act
        var token = _service.GenerateAccessToken(user);

        // Assert
        Assert.NotNull(token);
        Assert.NotEmpty(token);
    }
    
    [Fact]
    public void GenerateAccessToken_ShouldContainCorrectClaims()
    {
        // Arrange
        var user = CreateUser();

        // Act
        var token = _service.GenerateAccessToken(user);

        // Assert
        var handler = new JsonWebTokenHandler();
        var jwt = handler.ReadJsonWebToken(token);

        Assert.Contains(jwt.Claims, c =>
            c.Type == JwtRegisteredClaimNames.Email &&
            c.Value == user.Email);

        Assert.Contains(jwt.Claims, c =>
            c.Type == JwtRegisteredClaimNames.Sub &&
            c.Value == user.Id.ToString());

        Assert.Contains(jwt.Claims, c =>
            c.Type == "email_confirmed" &&
            c.Value == user.IsEmailConfirmed.ToString());
    }
    
    [Fact]
    public void GenerateAccessToken_ShouldContainIssuerAndAudience()
    {
        // Arrange
        var user = CreateUser();

        // Act
        var token = _service.GenerateAccessToken(user);

        // Assert
        var handler = new JsonWebTokenHandler();
        var jwt = handler.ReadJsonWebToken(token);

        Assert.Equal(_options.Issuer, jwt.Issuer);
        Assert.Equal(_options.Audience, jwt.Audiences.First());
    }
    
    [Fact]
    public void GenerateAccessToken_ShouldSetExpiration()
    {
        // Arrange
        var user = CreateUser();

        var before = DateTime.UtcNow;

        // Act
        var token = _service.GenerateAccessToken(user);

        // Assert
        var handler = new JsonWebTokenHandler();
        var jwt = handler.ReadJsonWebToken(token);

        var expectedExpiration = before.AddMinutes(_options.Lifetime);

        Assert.True(jwt.ValidTo <= expectedExpiration.AddSeconds(5));
    }
    
    [Fact]
    public void GenerateAccessToken_ShouldLogInformation()
    {
        // Arrange
        var user = CreateUser();

        // Act
        _service.GenerateAccessToken(user);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(user.Id.ToString())),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()!),
            Times.Once);
    }
    
    [Fact]
    public void GenerateAccessToken_ShouldReturnValidJwt()
    {
        // Arrange
        var user = CreateUser();

        // Act
        var token = _service.GenerateAccessToken(user);

        // Assert
        var handler = new JsonWebTokenHandler();

        Assert.True(handler.CanReadToken(token));
    }
    
    private User CreateUser()
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            IsEmailConfirmed = true
        };
    }
}