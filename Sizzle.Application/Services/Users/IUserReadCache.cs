using Sizzle.Application.Dtos.Users;

namespace Sizzle.Application.Services.Users;

public interface IUserReadCache
{
    Task<UserDto?> GetProfileAsync(Guid userId, Func<Task<UserDto?>> factory);
    Task<List<UserDto>> GetFollowersAsync(Guid userId, Func<Task<List<UserDto>>> factory);
    Task<List<UserDto>> GetFollowingAsync(Guid userId, Func<Task<List<UserDto>>> factory);
    Task UpdateProfileAsync(UserDto user);
    Task UpdateFollowAsync(Guid followerId, Guid followingId, bool following);
}
