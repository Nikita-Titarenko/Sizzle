using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Sizzle.Application.Services;
using Sizzle.Infrastructure.Options;

namespace Sizzle.Infrastructure.Services;

public class JwtTokenService(IOptions<JwtTokenOptions> jwtBearerOptions) : IJwtTokenService
{
    private readonly JwtTokenOptions _jwtTokenOptions = jwtBearerOptions.Value;

    public string GenerateToken(Guid userId, string role)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, role)
            ]),
            Issuer = _jwtTokenOptions.Issuer,
            Audience = _jwtTokenOptions.Audience,
            Expires = DateTime.UtcNow.AddDays(_jwtTokenOptions.ExpiresDay),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtTokenOptions.Key)),
                SecurityAlgorithms.HmacSha256)
        };

        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(descriptor);
        return handler.WriteToken(token);
    }
}
