namespace Sizzle.Application.Services;

public interface IJwtTokenService
{
    string GenerateToken(Guid userId, string role);
}
