using AutoMapper;
using Sizzle.Application.Dtos.Comments;
using Sizzle.Application.Services;
using Sizzle.Domain.Entities;

namespace Sizzle.Application.Mapping;

public class CommentImageUrlResolver(IFileService fileService) : IValueResolver<Comment, CommentDto, string?>
{
    public string? Resolve(Comment source, CommentDto destination, string? member, ResolutionContext context) =>
        fileService.GetFileUrl(Path.Combine("icons", source.UserId.ToString()));
}