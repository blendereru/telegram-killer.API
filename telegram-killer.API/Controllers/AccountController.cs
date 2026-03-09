using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using telegram_killer.API.DTOs.Request_DTOs;
using telegram_killer.API.DTOs.Response_DTOs;
using telegram_killer.API.Exceptions;
using telegram_killer.API.Services.Interfaces;

namespace telegram_killer.API.Controllers;

[Route("api/account")]
[ApiController]
public class AccountController : ControllerBase
{
    private readonly IAccountService _accountService;
    private readonly ILogger<AccountController> _logger;
    public AccountController(IAccountService accountService, ILogger<AccountController> logger)
    {
        _accountService = accountService;
        _logger = logger;
    }
    
    [EndpointSummary("The endpoint needed for registering a user")]
    [EndpointDescription("Registers a user by requiring an email in the request body." +
                         " This sends email confirmation code to user's email")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [ProducesResponseType<RegisterUserResponse>(StatusCodes.Status200OK, "application/json")]
    [Consumes("application/json")]
    [HttpPost("signup")]
    public async Task<IActionResult> Register(GetUserEmailRequest request)
    {
        var user = await _accountService.RegisterUserAsync(request.Email);
        var response = new RegisterUserResponse
        {
            RegisteredAt = user.RegisteredAt,
            Message = "User successfully registered. Waiting for email confirmation."
        };
        return Ok(response);
    }

    [EndpointSummary("The endpoint needed for logging in a user")]
    [EndpointDescription("Registers a user by requiring an email in the request body." +
                         " This sends email confirmation code to user's email")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [ProducesResponseType<LoginUserResponse>(StatusCodes.Status200OK, "application/json")]
    [Consumes("application/json")]
    [HttpPost("signin")]
    public async Task<IActionResult> Login(GetUserEmailRequest request)
    {
        await _accountService.LoginUserAsync(request.Email);
        var response = new LoginUserResponse
        {
            Message = "User successfully logged in. Waiting for email confirmation."
        };
        
        return Ok(response);
    }
    
    [EndpointSummary("The endpoint needed for confirming the email of the registered user")]
    [EndpointDescription("Confirms user's email by accepting user's Id and confirmation code in the body")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [ProducesResponseType<AuthResult>(StatusCodes.Status200OK, "application/json")]
    [Consumes("application/json")]
    [HttpPost("email/confirm")]
    public async Task<IActionResult> ConfirmEmailAndSignIn(ConfirmEmailRequest request)
    {
         var result = await _accountService.ConfirmEmailAndSignInAsync(request.Email, request.ConfirmationCode);
         return Ok(result);
    }

    [EndpointSummary("The endpoint needed for refreshing(updating, renewing) the tokens")]
    [EndpointDescription(
        "Refreshes current user's session by refreshing the tokens. This requires refresh token in the body")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [ProducesResponseType<AuthResult>(StatusCodes.Status200OK, "application/json")]
    [Consumes("application/json")]
    [HttpPost("tokens/refresh")]
    public async Task<IActionResult> RefreshTokens(GetRefreshTokenRequest request)
    {
        var result = await _accountService.RefreshTokensAsync(request.RefreshToken);
        return Ok(result);
    }
    
    [Authorize]
    [EndpointSummary("The endpoint needed to log user out from the system. This requires both access token in the header and refresh token in the body.")]
    [EndpointDescription("Logs out user from the system by deleting his refresh session from db. This requires access token in the Authorization header and refresh token in the body")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [Consumes("application/json")]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(GetRefreshTokenRequest request) 
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        if (!Guid.TryParse(userId, out var userGuid))
        {
            _logger.LogWarning("Invalid UserId format in JWT token");
            throw new UnauthorizedException("Invalid token");
        }
        
        await _accountService.LogoutAsync(request.RefreshToken, userGuid);
        return NoContent();
    }
    
    [Authorize]
    [EndpointSummary("The endpoint needed to retrieve current user's information. Note: this requires user access token in the Authorization header")]
    [EndpointDescription("Retrieves current user's information. This requires user access token in the Authorization header")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [ProducesResponseType<GetUserInformationResponse>(StatusCodes.Status200OK, "application/json")]
    [HttpGet("me")]
    public async Task<IActionResult> GetMyInformation()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        if (!Guid.TryParse(userId, out var userGuid))
        {
            _logger.LogWarning("Invalid UserId format in JWT token");
            throw new UnauthorizedException("Invalid token");
        }
        
        var response = await _accountService.GetUserInformationAsync(userGuid);
        
        return Ok(response);
    }

    [Authorize]
    [EndpointSummary("The endpoint needed to retrieve user information based on email. User requesting to retrieve information should be authorized")]
    [EndpointDescription("The endpoint needed to retrieve user information based on email. User requesting to retrieve information should be authorized(e.g. Provide access token in Authorization header)")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [ProducesResponseType<GetUserInformationResponse>(StatusCodes.Status200OK, "application/json")]
    [HttpGet]
    public async Task<IActionResult> GetUserInformation([FromQuery, Required] string email)
    {
        var requesterId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        if (!Guid.TryParse(requesterId, out var requesterGuid))
        {
            _logger.LogWarning("Invalid UserId format in JWT token");
            throw new UnauthorizedException("Invalid token");
        }
        
        var response = await _accountService.GetUserInformationAsync(requesterGuid, email);
        
        return Ok(response);
    }
}