using System.ComponentModel.DataAnnotations;

namespace telegram_killer.API.DTOs.Request_DTOs;

public class GetRefreshTokenRequest
{
    [Required]
    public string RefreshToken { get; set; }
}