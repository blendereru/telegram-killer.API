using Microsoft.AspNetCore.Mvc;
using telegram_killer.API.DTOs.Request_DTOs;
using telegram_killer.API.DTOs.Response_DTOs;
using telegram_killer.API.Services.Interfaces;

namespace telegram_killer.API.Controllers;

[Route("api/account")]
[ApiController]
public class AccountController : ControllerBase
{
    private readonly IAccountService _accountService;
    public AccountController(IAccountService accountService)
    {
        _accountService = accountService;
    }
    
    [EndpointSummary("The endpoint needed for registering a user")]
    [EndpointDescription("Registers a user by requiring an email in the request body." +
                         " This sends email confirmation code to user's email")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [ProducesResponseType<RegisterUserResponse>(StatusCodes.Status200OK, "application/json")]
    [Consumes("application/json")]
    [HttpPost("signup")]
    public async Task<IActionResult> Register(GetUserEmailRequest request)
    {
        var user = await _accountService.RegisterUserAsync(request.Email);
        var response = new RegisterUserResponse
        {
            UserId = user.Id,
            RegisteredAt = user.RegisteredAt,
            Message = "User successfully registered. Waiting for email confirmation."
        };
        return Ok(response);
    }

    [EndpointSummary("The endpoint needed for confirming the email of the registered user")]
    [EndpointDescription("Confirms user's email by accepting user's Id and confirmation code in the body. Note: This endpoint is idempotent")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [ProducesResponseType<AuthResult>(StatusCodes.Status200OK, "application/json")]
    [HttpPost("email/confirm")]
    public async Task<IActionResult> ConfirmEmailAndSignIn(ConfirmEmailRequest request)
    {
         var result = await _accountService.ConfirmEmailAndSignInAsync(request.UserId, request.ConfirmationCode);
         return Ok(result);
    }
}