using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace telegram_killer.API.Options;

public class JwtConfigurationOptions
{
    public string Audience { get; set; }
    public string Issuer { get; set; }
    public int Lifetime { get; set; }
    public string Key { get; set; }
    
    public SymmetricSecurityKey GetSymmetricSecurityKey() => 
        new(Encoding.UTF8.GetBytes(Key));
}