using System.ComponentModel.DataAnnotations;

namespace telegram_killer.API.DTOs.Request_DTOs;

public class CreateChatRequest
{
    [Required]
    public Guid OtherUserId { get; set; }
}