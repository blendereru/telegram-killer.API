using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using telegram_killer.API.DTOs.Request_DTOs;
using telegram_killer.API.DTOs.Response_DTOs;
using telegram_killer.API.Exceptions;
using telegram_killer.API.Services.Interfaces;

namespace telegram_killer.API.Controllers;

[Authorize]
[Route("api/chat")]
[ApiController]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;
    public ChatController(IChatService chatService)
    {
        _chatService = chatService;
    }
    
    [EndpointSummary("This endpoint is needed to create a chat with user")]
    [EndpointDescription("This endpoint is needed to create a chat with user whose Id is provided in the body. This endpoint requires authentication.")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    [ProducesResponseType<CreateChatResponse>(StatusCodes.Status201Created, "application/json")]
    [Consumes("application/json")]
    [HttpPost]
    public async Task<IActionResult> Create(CreateChatRequest request)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var chat = await _chatService.CreateDirectChatAsync(userId, request.OtherUserId);

        var chatResponse = new CreateChatResponse
        {
            ChatId = chat.Id,
            Participants = chat.Participants.Select(p => p.UserId).ToList(),
            CreatedAt = chat.CreatedAt
        };

        var location = $"api/chat/{chat.Id}"; // refactor later.
        
        return Created(location, chatResponse);
    }

    [Authorize]
    [EndpointSummary("Retrieve messages for a specific chat")]
    [EndpointDescription("Returns all messages belonging to the specified chat")]
    [ProducesResponseType<List<MessageDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [HttpGet]
    public async Task<IActionResult> GetMessages([FromQuery, Required] Guid chatId)
    {
        var userIdClaim = User.FindFirst("sub")?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            // log this action later...
            throw new UnauthorizedException("Invalid token");
        }

        var messages = await _chatService.GetMessagesAsync(chatId, userId);

        return Ok(messages);
    }
}