using System.ComponentModel.DataAnnotations;

namespace telegram_killer.API.DTOs.Request_DTOs;

public class ConfirmEmailRequest
{
    [Required]
    public string Email { get; set; }
    
    [Required]
    public string ConfirmationCode { get; set; }
}