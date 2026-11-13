using AutoMapper;
using Sizzle.Application.Dtos.Users;
using Sizzle.Application.Services;
using Sizzle.Domain.Entities;

namespace Sizzle.Application.Mapping;

public class UserImageUrlResolver(IFileService fileService) : IValueResolver<ApplicationUser, UserDto, string?>
{
    public string? Resolve(ApplicationUser source, UserDto destination, string? member, ResolutionContext context) =>
        fileService.GetFileUrl(Path.Combine("icons", source.Id.ToString()));
}