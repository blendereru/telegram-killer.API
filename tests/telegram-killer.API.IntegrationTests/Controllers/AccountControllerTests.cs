using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using telegram_killer.API.Data;
using telegram_killer.API.DTOs.Request_DTOs;
using telegram_killer.API.DTOs.Response_DTOs;
using telegram_killer.API.IntegrationTests.Helpers;
using telegram_killer.API.Models;
using telegram_killer.API.Services.Interfaces;

namespace telegram_killer.API.IntegrationTests.Controllers;

public class AccountControllerTests : IClassFixture<TelegramKillerWebApplicationFactory>, IAsyncLifetime
{
    private readonly TelegramKillerWebApplicationFactory _factory;

    public AccountControllerTests(TelegramKillerWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
    }

    [Fact]
    public async Task Register_NoBodyProvided_Returns400BadRequestWithValidationProblemDetails()
    {
        // Arrange
        var client = _factory.CreateClient();

        var request = new GetUserEmailRequest();

        // Act
        var response = await client.PostAsJsonAsync("api/account/signup", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await TestHelpers.AssertValidationProblemDetails(response, "Email", "required");
    }

    [Fact]
    public async Task Register_AlreadyExistingUserWithoutEmailConfirmed_Returns200OkWithMessage()
    {
        // Arrange
        var client = _factory.CreateClient();

        const string existingEmail = "example@example.com";
        using var scope = _factory.CreateScope();
        var applicationContext = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

        applicationContext.Users.Add(new User
        {
            Email = existingEmail,
            IsEmailConfirmed = false,
            Username = existingEmail,
            RegisteredAt = DateTimeOffset.UtcNow
        });
        await applicationContext.SaveChangesAsync();

        var request = new GetUserEmailRequest
        {
            Email = existingEmail
        };


        // Act
        var response = await client.PostAsJsonAsync("api/account/signup", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var registerUserResponse = await response.Content.ReadFromJsonAsync<RegisterUserResponse>();

        Assert.NotNull(registerUserResponse);

        Assert.Contains("registered", registerUserResponse.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_AlreadyExistingUserWithEmailConfirmed_Returns409ConflictWithProblemDetails()
    {
        // Arrange
        var client = _factory.CreateClient();

        const string existingEmail = "example@example.com";
        using var scope = _factory.CreateScope();
        var applicationContext = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

        applicationContext.Users.Add(new User
        {
            Email = existingEmail,
            IsEmailConfirmed = true,
            Username = existingEmail,
            RegisteredAt = DateTimeOffset.UtcNow
        });
        await applicationContext.SaveChangesAsync();

        var request = new GetUserEmailRequest
        {
            Email = existingEmail
        };

        // Act
        var response = await client.PostAsJsonAsync("api/account/signup", request);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await TestHelpers.AssertProblemDetails(response, "already exists");
    }

    [Fact]
    public async Task Register_Returns200OkWithMessageAndRegistrationDate()
    {
        // Arrange
        var client = _factory.CreateClient();

        const string validEmailExample = "email@example.com";

        var request = new GetUserEmailRequest { Email = validEmailExample };

        var before = DateTimeOffset.UtcNow;

        // Act
        var response = await client.PostAsJsonAsync("api/account/signup", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var registerUserResponse = await response.Content.ReadFromJsonAsync<RegisterUserResponse>();

        Assert.NotNull(registerUserResponse);

        var registrationDate = registerUserResponse.RegisteredAt;
        var after = DateTimeOffset.UtcNow;

        Assert.InRange(registrationDate, before, after);

        Assert.Contains("registered", registerUserResponse.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_UserStoredInDb()
    {
        // Arrange
        var client = _factory.CreateClient();

        const string validEmailExample = "email@example.com";

        var request = new GetUserEmailRequest { Email = validEmailExample };

        // Act
        var response = await client.PostAsJsonAsync("api/account/signup", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.CreateScope();
        var applicationContext = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

        var user = await applicationContext.Users.FirstOrDefaultAsync(u => u.Email == validEmailExample);
        Assert.NotNull(user);
    }

    [Fact]
    public async Task Login_NoBodyProvided_Returns400BadRequestWithValidationProblemDetails()
    {
        // Arrange
        var client = _factory.CreateClient();

        var request = new GetUserEmailRequest();

        // Act
        var response = await client.PostAsJsonAsync("api/account/signin", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await TestHelpers.AssertValidationProblemDetails(response, "Email", "required");
    }

    [Fact]
    public async Task Login_UserNotFound_Returns404NotFoundWithProblemDetails()
    {
        // Arrange
        var client = _factory.CreateClient();

        const string notFoundEmail = "example@example.com";

        var request = new GetUserEmailRequest()
        {
            Email = notFoundEmail
        };

        // Act
        var response = await client.PostAsJsonAsync("api/account/signin", request);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await TestHelpers.AssertProblemDetails(response, "not found");
    }

    [Fact]
    public async Task ConfirmEmailAndSignIn_NoBodyProvided_Returns400BadRequestWithValidationProblemDetails()
    {
        // Arrange
        var client = _factory.CreateClient();

        var request = new ConfirmEmailRequest();

        // Act
        var response = await client.PostAsJsonAsync("api/account/email/confirm", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.NotNull(problem);
        Assert.Contains("Email", problem.Errors.Keys);
        Assert.Contains("ConfirmationCode", problem.Errors.Keys);
    }

    [Fact]
    public async Task
        ConfirmEmailAndSignIn_ConfirmationCodeNotProvided_Returns400BadRequestWithValidationProblemDetails()
    {
        // Arrange
        var client = _factory.CreateClient();

        const string exampleEmail = "example@example.com";

        var request = new ConfirmEmailRequest
        {
            Email = exampleEmail
        };

        // Act
        var response = await client.PostAsJsonAsync("api/account/email/confirm", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await TestHelpers.AssertValidationProblemDetails(response, "ConfirmationCode", "required");
    }


    [Fact]
    public async Task ConfirmEmailAndSignIn_EmailNotProvided_Returns400BadRequestWithValidationProblemDetails()
    {
        // Arrange
        var client = _factory.CreateClient();

        const string emptyCode = "0000000";

        var request = new ConfirmEmailRequest
        {
            ConfirmationCode = emptyCode
        };

        // Act
        var response = await client.PostAsJsonAsync("api/account/email/confirm", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await TestHelpers.AssertValidationProblemDetails(response, "Email", "required");
    }

    [Fact]
    public async Task ConfirmEmailAndSignIn_EmailAndCodeForNonExistingUser_Returns404NotFoundWithProblemDetails()
    {
        // Arrange
        var client = _factory.CreateClient();

        const string nonExistingCode = "0000000";

        const string nonExistingEmail = "example@example.com";

        var request = new ConfirmEmailRequest
        {
            Email = nonExistingEmail,
            ConfirmationCode = nonExistingCode
        };

        // Act
        var response = await client.PostAsJsonAsync("api/account/email/confirm", request);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await TestHelpers.AssertProblemDetails(response, "invalid");
    }

    [Fact]
    public async Task ConfirmEmailAndSignIn_NonExistingConfirmationCodeForEmail_Returns404NotFoundWithProblemDetails()
    {
        // Arrange
        var client = _factory.CreateClient();

        const string nonExistingCode = "0000000";

        const string exampleEmail = "example@example.com";

        var request = new ConfirmEmailRequest
        {
            Email = exampleEmail,
            ConfirmationCode = nonExistingCode
        };

        using var scope = _factory.CreateScope();
        var applicationContext = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

        applicationContext.Users.Add(new User
        {
            Email = exampleEmail,
            Username = exampleEmail,
            RegisteredAt = DateTimeOffset.UtcNow
        });
        await applicationContext.SaveChangesAsync();

        // Act
        var response = await client.PostAsJsonAsync("api/account/email/confirm", request);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await TestHelpers.AssertProblemDetails(response, "invalid");
    }

    [Fact]
    public async Task ConfirmEmailAndSignIn_NotMatchingConfirmationCode_Returns404NotFoundWithProblemDetails()
    {
        // Arrange
        var client = _factory.CreateClient();

        const string originalCode = "123456";
        const string userEmail = "user@example.com";
        const string notMatchingCode = "000000";
        const int expirationTimeInMinutes = 10;

        using var scope = _factory.CreateScope();
        var applicationContext = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var hasherService = _factory.Services.GetRequiredService<IHasherService>();

        var user = new User
        {
            Email = userEmail,
            Username = userEmail,
            RegisteredAt = DateTimeOffset.UtcNow
        };

        applicationContext.Users.Add(user);
        await applicationContext.SaveChangesAsync();

        applicationContext.EmailConfirmationCodes.Add(new EmailConfirmationCode
        {
            UserId = user.Id,
            ConfirmationCodeHash = hasherService.HashConfirmationCode(originalCode),
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(expirationTimeInMinutes)
        });
        await applicationContext.SaveChangesAsync();

        var request = new ConfirmEmailRequest
        {
            Email = userEmail,
            ConfirmationCode = notMatchingCode
        };

        // Act
        var response = await client.PostAsJsonAsync("api/account/email/confirm", request);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await TestHelpers.AssertProblemDetails(response, "invalid");
    }

    [Fact]
    public async Task ConfirmEmailAndSignIn_SetsEmailAsConfirmed()
    {
        // Arrange
        var client = _factory.CreateClient();

        const string userEmail = "user@example.com";
        const string userCode = "123456";
        const int expirationTimeInMinutes = 10;

        var hasherService = _factory.Services.GetRequiredService<IHasherService>();

        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

            var user = new User
            {
                Email = userEmail,
                Username = userEmail,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();

            context.EmailConfirmationCodes.Add(new EmailConfirmationCode
            {
                UserId = user.Id,
                ConfirmationCodeHash = hasherService.HashConfirmationCode(userCode),
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(expirationTimeInMinutes)
            });

            await context.SaveChangesAsync();
        }

        var request = new ConfirmEmailRequest
        {
            Email = userEmail,
            ConfirmationCode = userCode
        };

        // Act
        var response = await client.PostAsJsonAsync("api/account/email/confirm", request);

        // Assert
        response.EnsureSuccessStatusCode();

        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

            var confirmedUser = await context.Users
                .FirstOrDefaultAsync(u => u.Email == userEmail);

            Assert.NotNull(confirmedUser);
            Assert.True(confirmedUser.IsEmailConfirmed);
        }
    }

    [Fact]
    public async Task ConfirmEmailAndSignIn_RemovesAllConfirmationCodeEntries()
    {
        // Arrange
        var client = _factory.CreateClient();

        const string userEmail = "user@example.com";
        const string userCode = "123456";
        const int expirationTimeInMinutes = 10;

        var hasherService = _factory.Services.GetRequiredService<IHasherService>();

        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

            var user = new User
            {
                Email = userEmail,
                Username = userEmail,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();

            context.EmailConfirmationCodes.Add(new EmailConfirmationCode
            {
                UserId = user.Id,
                ConfirmationCodeHash = hasherService.HashConfirmationCode(userCode),
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(expirationTimeInMinutes)
            });

            await context.SaveChangesAsync();
        }

        var request = new ConfirmEmailRequest
        {
            Email = userEmail,
            ConfirmationCode = userCode
        };

        // Act
        var response = await client.PostAsJsonAsync("api/account/email/confirm", request);

        // Assert
        response.EnsureSuccessStatusCode();

        using (var scope = _factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

            var user = context.Users.FirstOrDefault(u => u.Email == userEmail);

            Assert.NotNull(user);

            var confirmationCodes = context.EmailConfirmationCodes
                .Where(e => e.UserId == user.Id)
                .ToList();

            Assert.Empty(confirmationCodes);
        }
    }

    [Fact]
    public async Task ConfirmEmailAndSignIn_CreatesRefreshSession()
    {
        // Arrange
        var client = _factory.CreateClient();

        const string userEmail = "user@example.com";
        const string userCode = "123456";
        const int expirationTimeInMinutes = 10;

        var hasherService = _factory.Services.GetRequiredService<IHasherService>();

        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

            var user = new User
            {
                Email = userEmail,
                Username = userEmail,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();

            context.EmailConfirmationCodes.Add(new EmailConfirmationCode
            {
                UserId = user.Id,
                ConfirmationCodeHash = hasherService.HashConfirmationCode(userCode),
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(expirationTimeInMinutes)
            });

            await context.SaveChangesAsync();
        }

        var request = new ConfirmEmailRequest
        {
            Email = userEmail,
            ConfirmationCode = userCode
        };

        // Act
        var response = await client.PostAsJsonAsync("api/account/email/confirm", request);

        // Assert
        response.EnsureSuccessStatusCode();

        using (var scope = _factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

            var user = await context.Users.FirstOrDefaultAsync(u => u.Email == userEmail);

            Assert.NotNull(user);

            var refreshSession = await context.RefreshSessions.SingleOrDefaultAsync(r => r.UserId == user.Id);

            Assert.NotNull(refreshSession);
            Assert.Equal(user.Id, refreshSession.UserId);
        }
    }

    [Fact]
    public async Task ConfirmEmailAndSignin_Returns200OkWithTokens()
    {
        // Arrange
        var client = _factory.CreateClient();

        const string userEmail = "user@example.com";
        const string userCode = "123456";
        const int expirationTimeInMinutes = 10;

        var hasherService = _factory.Services.GetRequiredService<IHasherService>();

        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

            var user = new User
            {
                Email = userEmail,
                Username = userEmail,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();

            context.EmailConfirmationCodes.Add(new EmailConfirmationCode
            {
                UserId = user.Id,
                ConfirmationCodeHash = hasherService.HashConfirmationCode(userCode),
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(expirationTimeInMinutes)
            });

            await context.SaveChangesAsync();
        }

        var request = new ConfirmEmailRequest
        {
            Email = userEmail,
            ConfirmationCode = userCode
        };

        // Act
        var response = await client.PostAsJsonAsync("api/account/email/confirm", request);

        // Assert
        response.EnsureSuccessStatusCode();

        var authResult = await response.Content.ReadFromJsonAsync<AuthResult>();

        Assert.NotNull(authResult);
        Assert.NotNull(authResult.AccessToken);
        Assert.NotNull(authResult.RefreshToken);
    }

    [Fact]
    public async Task ConfirmEmailAndSignIn_RefreshTokensMatch()
    {
        // Arrange
        var client = _factory.CreateClient();

        const string userEmail = "user@example.com";
        const string userCode = "123456";
        const int expirationTimeInMinutes = 10;

        var hasherService = _factory.Services.GetRequiredService<IHasherService>();

        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

            var user = new User
            {
                Email = userEmail,
                Username = userEmail,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();

            context.EmailConfirmationCodes.Add(new EmailConfirmationCode
            {
                UserId = user.Id,
                ConfirmationCodeHash = hasherService.HashConfirmationCode(userCode),
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(expirationTimeInMinutes)
            });

            await context.SaveChangesAsync();
        }

        var request = new ConfirmEmailRequest
        {
            Email = userEmail,
            ConfirmationCode = userCode
        };

        // Act
        var response = await client.PostAsJsonAsync("api/account/email/confirm", request);

        // Assert
        response.EnsureSuccessStatusCode();
        var authResult = await response.Content.ReadFromJsonAsync<AuthResult>();

        Assert.NotNull(authResult);
        Assert.NotNull(authResult.RefreshToken);

        using (var scope = _factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

            var session =
                await context.RefreshSessions.SingleOrDefaultAsync(r => r.RefreshToken == authResult.RefreshToken);

            Assert.NotNull(session);
            Assert.Equal(authResult.RefreshToken, session.RefreshToken);
        }
    }

    [Fact]
    public async Task RefreshTokens_NoRefreshTokenProvided_Returns400BadRequestWithValidationProblemDetails()
    {
        // Arrange
        var client = _factory.CreateClient();

        var request = new GetRefreshTokenRequest();

        // Act
        var response = await client.PostAsJsonAsync("api/account/tokens/refresh", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await TestHelpers.AssertValidationProblemDetails(response, "RefreshToken", "required");
    }

    [Fact]
    public async Task RefreshTokens_NonExistentRefreshToken_Returns401UnauthorizedWithProblemDetails()
    {
        // Arrange
        var client = _factory.CreateClient();

        const string userEmail = "user@example.com";
        var nonExistentRefreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        using (var scope = _factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

            var user = new User
            {
                Email = userEmail,
                Username = userEmail,
                IsEmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();
        }

        var request = new GetRefreshTokenRequest
        {
            RefreshToken = nonExistentRefreshToken
        };

        // Act
        var response = await client.PostAsJsonAsync("api/account/tokens/refresh", request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await TestHelpers.AssertProblemDetails(response, "invalid");
    }

    [Fact]
    public async Task RefreshTokens_EmailNotConfirmed_Returns403ForbiddenWithProblemDetails()
    {
        // Arrange
        var client = _factory.CreateClient();

        const string userEmail = "user@example.com";
        var userRefreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        const string userIp = "192.0.2.1";
        const string userUa =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36";
        const int expirationTimeInDays = 7;

        using (var scope = _factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

            var user = new User
            {
                Email = userEmail,
                Username = userEmail,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();

            var refreshSession = new RefreshSession
            {
                UserId = user.Id,
                RefreshToken = userRefreshToken,
                Ip = userIp,
                UA = userUa,
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(expirationTimeInDays)
            };

            context.RefreshSessions.Add(refreshSession);
            await context.SaveChangesAsync();
        }

        var request = new GetRefreshTokenRequest
        {
            RefreshToken = userRefreshToken
        };

        // Act
        var response = await client.PostAsJsonAsync("api/account/tokens/refresh", request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await TestHelpers.AssertProblemDetails(response, "must be verified");
    }

    [Fact]
    public async Task RefreshTokens_IpMisMatch_Returns401UnauthorizedWithProblemDetails()
    {
        // Arrange
        var client = _factory.CreateClient();

        const string userEmail = "user@example.com";
        var userRefreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        const string userIp = "192.0.2.1";
        const string userUa =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36";
        const int expirationTimeInDays = 7;

        using (var scope = _factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

            var user = new User
            {
                Email = userEmail,
                Username = userEmail,
                IsEmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();

            var refreshSession = new RefreshSession
            {
                UserId = user.Id,
                RefreshToken = userRefreshToken,
                Ip = userIp,
                UA = userUa,
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(expirationTimeInDays)
            };

            context.RefreshSessions.Add(refreshSession);
            await context.SaveChangesAsync();
        }

        var request = new GetRefreshTokenRequest
        {
            RefreshToken = userRefreshToken
        };

        // Act
        var response = await client.PostAsJsonAsync("api/account/tokens/refresh", request);

        // Arrange
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await TestHelpers.AssertProblemDetails(response, "invalid");
    }

    [Fact]
    public async Task RefreshTokens_MoreThan5ActiveSessions_RemovesAllExceptNewOne()
    {
        // Arrange
        var client = _factory.CreateClient();

        var newRefreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        const string userEmail = "user@example.com";
        const string localIp = "127.0.0.1";
        const string userUa =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36";
        const int expirationTimeInDays = 7;
        const int previousRefreshSessionsCount = 5;

        using (var scope = _factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

            var user = new User
            {
                Email = userEmail,
                Username = userEmail,
                IsEmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();

            for (var i = 0; i < previousRefreshSessionsCount; i++)
            {
                var userRefreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
                var creationDateOffsetFromNow = -i;

                var previousRefreshSession = new RefreshSession
                {
                    UserId = user.Id,
                    RefreshToken = userRefreshToken,
                    Ip = localIp,
                    UA = userUa,
                    CreatedAt = DateTimeOffset.UtcNow.AddDays(creationDateOffsetFromNow),
                    ExpiresAt = DateTimeOffset.UtcNow.AddDays(creationDateOffsetFromNow + expirationTimeInDays)
                };
                context.RefreshSessions.Add(previousRefreshSession);
            }

            await context.SaveChangesAsync();

            var newRefreshSession = new RefreshSession
            {
                UserId = user.Id,
                RefreshToken = newRefreshToken,
                Ip = localIp,
                UA = userUa,
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(expirationTimeInDays)
            };

            context.RefreshSessions.Add(newRefreshSession);
            await context.SaveChangesAsync();
        }

        var request = new GetRefreshTokenRequest
        {
            RefreshToken = newRefreshToken
        };

        // Act
        var response = await client.PostAsJsonAsync("api/account/tokens/refresh", request);

        // Assert
        response.EnsureSuccessStatusCode();
        using (var scope = _factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
            var newRefreshSession = await context.RefreshSessions.ToListAsync();

            Assert.Single(newRefreshSession);
        }
    }

    [Fact]
    public async Task RefreshTokens_Returns200OkWithTokens()
    {
        // Arrange
        var client = _factory.CreateClient();

        const string userEmail = "user@example.com";
        var userRefreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        const string localIp = "127.0.0.1";
        const string userUa =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36";
        const int expirationTimeInDays = 7;

        using (var scope = _factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

            var user = new User
            {
                Email = userEmail,
                Username = userEmail,
                IsEmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();

            var refreshSession = new RefreshSession
            {
                UserId = user.Id,
                RefreshToken = userRefreshToken,
                Ip = localIp,
                UA = userUa,
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(expirationTimeInDays)
            };

            context.RefreshSessions.Add(refreshSession);
            await context.SaveChangesAsync();
        }

        var request = new GetRefreshTokenRequest
        {
            RefreshToken = userRefreshToken
        };

        // Act
        var response = await client.PostAsJsonAsync("api/account/tokens/refresh", request);

        // Assert
        response.EnsureSuccessStatusCode();

        var authResult = await response.Content.ReadFromJsonAsync<AuthResult>();

        Assert.NotNull(authResult);
        Assert.NotNull(authResult.AccessToken);
        Assert.NotNull(authResult.RefreshToken);
    }

    [Fact]
    public async Task Logout_NoAccessTokenProvided_Returns401Unauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        var request = new GetRefreshTokenRequest();

        // Act
        var response = await client.PostAsJsonAsync("api/account/logout", request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_NoRefreshTokenProvided_Returns400BadRequestWithValidationProblemDetails()
    {
        // Arrange
        var client = _factory.CreateClient();

        string accessToken;

        using (var scope = _factory.CreateScope())
        {
            var tokensProvider = scope.ServiceProvider.GetRequiredService<ITokensProviderService>();

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "user@example.com",
                Username = "user@example.com",
                IsEmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            accessToken = tokensProvider.GenerateAccessToken(user);
        }

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, accessToken);

        var request = new GetRefreshTokenRequest();

        // Act
        var response = await client.PostAsJsonAsync("api/account/logout", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Logout_NonExistentRefreshToken_Returns204NoContent()
    {
        // Arrange
        var client = _factory.CreateClient();

        const string userEmail = "user@example.com";
        var nonExistentRefreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        string accessToken;

        using (var scope = _factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
            var tokensProviderService = scope.ServiceProvider.GetRequiredService<ITokensProviderService>();

            var user = new User
            {
                Email = userEmail,
                Username = userEmail,
                IsEmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();

            accessToken = tokensProviderService.GenerateAccessToken(user);
        }

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, accessToken);

        var request = new GetRefreshTokenRequest
        {
            RefreshToken = nonExistentRefreshToken
        };

        // Act
        var response = await client.PostAsJsonAsync("api/account/logout", request);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Logout_RefreshTokenUsersDoNotMatch_Returns403ForbiddenWithProblemDetails()
    {
        // Arrange
        var client = _factory.CreateClient();

        const string userAEmail = "userA@example.com";
        const string userBEmail = "userB@example.com";

        var userBRefreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        const string userIp = "192.0.2.1";
        const string userUa =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36";

        const int expirationTimeInDays = 7;

        Guid userBId;
        string accessToken;

        using (var scope = _factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

            var userA = new User
            {
                Id = Guid.NewGuid(),
                Email = userAEmail,
                Username = userAEmail,
                IsEmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            var userB = new User
            {
                Id = Guid.NewGuid(),
                Email = userBEmail,
                Username = userBEmail,
                IsEmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            userBId = userB.Id;

            context.Users.AddRange(userA, userB);

            var refreshSession = new RefreshSession
            {
                UserId = userBId,
                RefreshToken = userBRefreshToken,
                Ip = userIp,
                UA = userUa,
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(expirationTimeInDays)
            };

            context.RefreshSessions.Add(refreshSession);

            await context.SaveChangesAsync();

            var tokensProviderService = scope.ServiceProvider.GetRequiredService<ITokensProviderService>();

            accessToken = tokensProviderService.GenerateAccessToken(userA);
        }

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, accessToken);

        var request = new GetRefreshTokenRequest
        {
            RefreshToken = userBRefreshToken
        };

        // Act
        var response = await client.PostAsJsonAsync("api/account/logout", request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await TestHelpers.AssertProblemDetails(response, "permission");
    }

    [Fact]
    public async Task Logout_RemovesRefreshSessionFromDb()
    {
        // Arrange
        var client = _factory.CreateClient();

        const string userIp = "192.0.2.1";
        const string userUa =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36";
        const string userEmail = "user@example.com";

        var userRefreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        Guid userId;
        string accessToken;

        using (var scope = _factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
            var tokensProvider = scope.ServiceProvider.GetRequiredService<ITokensProviderService>();

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = userEmail,
                Username = userEmail,
                IsEmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            userId = user.Id;

            context.Users.Add(user);

            var refreshSession = new RefreshSession
            {
                UserId = userId,
                RefreshToken = userRefreshToken,
                Ip = userIp,
                UA = userUa,
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(7)
            };

            context.RefreshSessions.Add(refreshSession);

            await context.SaveChangesAsync();

            accessToken = tokensProvider.GenerateAccessToken(user);
        }

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, accessToken);

        var request = new GetRefreshTokenRequest
        {
            RefreshToken = userRefreshToken
        };

        // Act
        var response = await client.PostAsJsonAsync("api/account/logout", request);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using (var scope = _factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

            var session = await context.RefreshSessions
                .FirstOrDefaultAsync(x => x.RefreshToken == userRefreshToken);

            Assert.Null(session);
        }
    }

    [Fact]
    public async Task GetMyInformation_NoAccessTokenProvided_Returns401Unauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("api/account/me");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMyInformation_NonExistentUser_Return404NotFoundWithProblemDetails()
    {
        // Arrange
        var client = _factory.CreateClient();

        string accessToken;

        using (var scope = _factory.CreateScope())
        {
            var tokensProvider = scope.ServiceProvider.GetRequiredService<ITokensProviderService>();

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "user@example.com",
                Username = "user@example.com",
                IsEmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            accessToken = tokensProvider.GenerateAccessToken(user);
        }

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, accessToken);

        // Act
        var response = await client.GetAsync("api/account/me");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await TestHelpers.AssertProblemDetails(response, "not found");
    }

    [Fact]
    public async Task GetMyInformation_ReturnsUserIdAndEmailWithDbMatchingValues()
    {
        // Arrange
        var client = _factory.CreateClient();

        const string userEmail = "user@example.com";
        string accessToken;

        using (var scope = _factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
            var tokensProvider = scope.ServiceProvider.GetRequiredService<ITokensProviderService>();

            var user = new User
            {
                Email = userEmail,
                Username = userEmail,
                IsEmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();

            accessToken = tokensProvider.GenerateAccessToken(user);
        }

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, accessToken);

        // Act
        var response = await client.GetAsync("api/account/me");

        // Assert
        response.EnsureSuccessStatusCode();

        var userInfo = await response.Content.ReadFromJsonAsync<GetUserInformationResponse>();

        Assert.NotNull(userInfo);
        Assert.NotNull(userInfo.Email);
        Assert.Equal(userEmail, userInfo.Email);

        using (var scope = _factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

            var user = await context.Users.FirstOrDefaultAsync(x => x.Email == userInfo.Email);

            Assert.NotNull(user);
            Assert.Equal(user.Email, userInfo.Email);
            Assert.Equal(user.Id, userInfo.Id);
        }
    }

    [Fact]
    public async Task GetUserInformation_NoAccessTokenProvided_Returns401Unauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        const string testEmail = "user@example.com";

        // Act
        var response = await client.GetAsync($"api/account?email={testEmail}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetUserInformation_EmptyEmailProvided_Returns400BadRequestWithValidationProblemDetails()
    {
        // Arrange
        var client = _factory.CreateClient();

        const string testEmail = "";

        string accessToken;
        using (var scope = _factory.CreateScope())
        {
            var tokensProviderService = scope.ServiceProvider.GetRequiredService<ITokensProviderService>();

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "user@example.com",
                Username = "user@example.com",
                IsEmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            accessToken = tokensProviderService.GenerateAccessToken(user);
        }

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, accessToken);

        // Act
        var response = await client.GetAsync($"api/account?email={testEmail}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await TestHelpers.AssertValidationProblemDetails(response, "email", "required");
    }

    [Fact]
    public async Task GetUserInformation_NonExistentRequester_Returns401UnauthorizedWithProblemDetails()
    {
        // Arrange
        var client = _factory.CreateClient();

        const string exampleRequestedUserEmail = "nonexistentuser@example.com";
        const string requesterEmail = "user@example.com";
        string accessToken;

        using (var scope = _factory.CreateScope())
        {
            var tokensProviderService = scope.ServiceProvider.GetRequiredService<ITokensProviderService>();

            var requester = new User
            {
                Email = requesterEmail,
                Username = requesterEmail,
                IsEmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            accessToken = tokensProviderService.GenerateAccessToken(requester);
        }

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, accessToken);

        // Act
        var response = await client.GetAsync($"api/account?email={exampleRequestedUserEmail}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await TestHelpers.AssertProblemDetails(response, "authorized");
    }

    [Fact]
    public async Task GetUserInformation_NonExistentRequestedUser_Returns404NotFoundWithProblemDetails()
    {
        // Arrange
        var client = _factory.CreateClient();

        const string nonExistentRequestedUserEmail = "nonexistentuser@example.com";
        const string requesterEmail = "user@example.com";
        string accessToken;

        using (var scope = _factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
            var tokensProviderService = scope.ServiceProvider.GetRequiredService<ITokensProviderService>();

            var requester = new User
            {
                Email = requesterEmail,
                Username = requesterEmail,
                IsEmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            context.Users.Add(requester);
            await context.SaveChangesAsync();

            accessToken = tokensProviderService.GenerateAccessToken(requester);
        }

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, accessToken);

        // Act
        var response = await client.GetAsync($"api/account?email={nonExistentRequestedUserEmail}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await TestHelpers.AssertProblemDetails(response, "not found");
    }

    [Fact]
    public async Task GetUserInformation_ReturnsUserIdAndEmailWithDbMatchingValues()
    {
        // Arrange
        var client = _factory.CreateClient();

        const string requestedUserEmail = "nonexistentuser@example.com";
        const string requesterEmail = "user@example.com";
        string accessToken;

        using (var scope = _factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
            var tokensProviderService = scope.ServiceProvider.GetRequiredService<ITokensProviderService>();

            var requester = new User
            {
                Email = requesterEmail,
                Username = requesterEmail,
                IsEmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            var requested = new User
            {
                Email = requestedUserEmail,
                Username = requestedUserEmail,
                IsEmailConfirmed = true,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            context.Users.AddRange(requester, requested);
            await context.SaveChangesAsync();

            accessToken = tokensProviderService.GenerateAccessToken(requester);
        }

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, accessToken);

        // Act
        var response = await client.GetAsync($"api/account?email={requestedUserEmail}");

        // Assert
        response.EnsureSuccessStatusCode();

        var userInfo = await response.Content.ReadFromJsonAsync<GetUserInformationResponse>();

        Assert.NotNull(userInfo);
        Assert.NotNull(userInfo.Email);
        Assert.Equal(requestedUserEmail, userInfo.Email);

        using (var scope = _factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

            var requested = await context.Users.FirstOrDefaultAsync(u => u.Email == requestedUserEmail);

            Assert.NotNull(requested);
            Assert.Equal(requested.Email, userInfo.Email);
            Assert.Equal(requested.Id, userInfo.Id);
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;
}