using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using telegram_killer.API.Data;
using telegram_killer.API.DTOs.Request_DTOs;
using telegram_killer.API.DTOs.Response_DTOs;
using telegram_killer.API.IntegrationTests.Helpers;
using telegram_killer.API.Models;

namespace telegram_killer.API.IntegrationTests;

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
    public async Task Register_NoBodyProvided_Returns400BadRequestWithProblemDetails()
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
    public async Task Register_AlreadyExistingUser_Returns409ConflictWithProblemDetails()
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
    public async Task Register_ParseError_Returns500InternalServerErrorWithProblemDetails()
    {
        // Arrange
        var client = _factory.CreateClient();

        const string invalidEmail = "email@-example.com";

        var request = new GetUserEmailRequest
        {
            Email = invalidEmail
        };
        
        // Act
        var response = await client.PostAsJsonAsync("api/account/signup", request);
        
        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await TestHelpers.AssertProblemDetails(response, "error");
    }

    [Fact]
    public async Task Register_ParseError_UserNotStoredInDb()
    {
        // Arrange
        var client = _factory.CreateClient();

        const string invalidEmail = "email@-example.com";

        var request = new GetUserEmailRequest
        {
            Email = invalidEmail
        };
        
        // Act
        var response = await client.PostAsJsonAsync("api/account/signup", request);
        
        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        
        using var scope = _factory.CreateScope();
        var applicationContext = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        
        var user = await applicationContext.Users.FirstOrDefaultAsync(u => u.Email == invalidEmail);
        Assert.Null(user);
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
    public async Task Register_UserAndCodeStoredInDb()
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
        
        var code = await applicationContext.EmailConfirmationCodes.FirstOrDefaultAsync(e => e.UserId == user.Id);
        Assert.NotNull(code);
    }
    public Task DisposeAsync() => Task.CompletedTask;
}