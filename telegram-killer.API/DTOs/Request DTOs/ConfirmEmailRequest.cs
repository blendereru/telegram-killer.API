using System.ComponentModel.DataAnnotations;

namespace telegram_killer.API.DTOs.Request_DTOs;

public class ConfirmEmailRequest
{
    [Required]
    public Guid UserId { get; set; }
    
    [Required]
    public string ConfirmationCode { get; set; }
}