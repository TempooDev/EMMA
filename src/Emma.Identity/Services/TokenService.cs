using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Emma.Identity.Models;
using Microsoft.IdentityModel.Tokens;

namespace Emma.Identity.Services;

public interface ITokenService
{
    string CreateToken(ApplicationUser user);
}

public class TokenService(IConfiguration configuration) : ITokenService
{
    public string CreateToken(ApplicationUser user)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? ""),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("tenant_id", user.TenantId ?? ""),
            new("assigned_assets", user.AssignedAssets ?? "")
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"] ?? "a-very-long-secret-key-that-should-be-in-config"));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddHours(2);

        var token = new JwtSecurityToken(
            configuration["Jwt:Issuer"],
            configuration["Jwt:Audience"],
            claims,
            expires: expires,
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
