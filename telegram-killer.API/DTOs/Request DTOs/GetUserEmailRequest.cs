using System.ComponentModel.DataAnnotations;

namespace telegram_killer.API.DTOs.Request_DTOs;

public class GetUserEmailRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; }
}