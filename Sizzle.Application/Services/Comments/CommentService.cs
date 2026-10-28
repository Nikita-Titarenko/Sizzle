using Sizzle.Application.Dtos.Comments;
using Sizzle.Application.Repositories;
using Sizzle.Domain.Common;
using Sizzle.Domain.Entities;

namespace Sizzle.Application.Services.Comments;

public class CommentService(ICommentRepository commentRepository, IRecipeRepository recipeRepository, IUnitOfWork unitOfWork) : ICommentService
{
    public async Task<Result<IReadOnlyList<CommentDto>>> GetByRecipeIdAsync(Guid recipeId)
    {
        if (!await recipeRepository.Exists(recipeId))
        {
            return new Result<IReadOnlyList<CommentDto>> { Errors = [new Error { Message = "Recipe not found", Key = ErrorKey.NotFound }] };
        }

        var comments = await commentRepository.GetByRecipeIdAsync(recipeId);
        return new Result<IReadOnlyList<CommentDto>> { Value = comments.Select(ToDto).ToList() };
    }

    public async Task<Result<CommentDto>> CreateAsync(CreateCommentDto dto)
    {
        var contentError = ValidateContent(dto.Content);
        if (contentError != null)
        {
            return new Result<CommentDto> { Errors = [contentError] };
        }

        if (!await recipeRepository.Exists(dto.RecipeId))
        {
            return new Result<CommentDto> { Errors = [new Error { Message = "Recipe not found", Key = ErrorKey.NotFound }] };
        }

        if (dto.ParentId.HasValue)
        {
            var parent = await commentRepository.GetAsync(dto.ParentId.Value);
            if (parent == null)
            {
                return new Result<CommentDto> { Errors = [new Error { Message = "Parent comment not found", Key = ErrorKey.NotFound }] };
            }

            if (parent.RecipeId != dto.RecipeId)
            {
                return new Result<CommentDto> { Errors = [new Error { Message = "Parent comment belongs to another recipe", Key = ErrorKey.BadRequest }] };
            }
        }

        var comment = new Comment
        {
            Id = Guid.NewGuid(),
            ParentId = dto.ParentId,
            Content = dto.Content.Trim(),
            CreatedAt = DateTime.UtcNow,
            UserId = dto.UserId,
            RecipeId = dto.RecipeId
        };

        commentRepository.Add(comment);
        await unitOfWork.CommitAsync();
        return new Result<CommentDto> { Value = ToDto(comment) };
    }

    public async Task<Result<CommentDto>> UpdateAsync(Guid commentId, Guid userId, string content)
    {
        var contentError = ValidateContent(content);
        if (contentError != null)
        {
            return new Result<CommentDto> { Errors = [contentError] };
        }

        var comment = await commentRepository.GetAsync(commentId);
        if (comment == null)
        {
            return new Result<CommentDto> { Errors = [new Error { Message = "Comment not found", Key = ErrorKey.NotFound }] };
        }

        if (comment.UserId != userId)
        {
            return new Result<CommentDto> { Errors = [new Error { Message = "Only the author can edit this comment", Key = ErrorKey.Forbidden }] };
        }

        comment.Content = content.Trim();
        await unitOfWork.CommitAsync();
        return new Result<CommentDto> { Value = ToDto(comment) };
    }

    public async Task<Result> DeleteAsync(Guid commentId, Guid userId)
    {
        var comment = await commentRepository.GetAsync(commentId);
        if (comment == null)
        {
            return new Result { Errors = [new Error { Message = "Comment not found", Key = ErrorKey.NotFound }] };
        }

        if (comment.UserId != userId)
        {
            return new Result { Errors = [new Error { Message = "Only the author can delete this comment", Key = ErrorKey.Forbidden }] };
        }

        commentRepository.Remove(comment);
        await unitOfWork.CommitAsync();
        return new Result();
    }

    private static Error? ValidateContent(string content)
    {
        return string.IsNullOrWhiteSpace(content) || content.Length > 2000
            ? new Error { Message = "Content must contain 1 to 2000 characters", Key = ErrorKey.BadRequest }
            : null;
    }

    private static CommentDto ToDto(Comment comment)
    {
        return new CommentDto
        {
            Id = comment.Id,
            ParentId = comment.ParentId,
            Content = comment.Content,
            CreatedAt = comment.CreatedAt,
            UserId = comment.UserId,
            RecipeId = comment.RecipeId
        };
    }
}