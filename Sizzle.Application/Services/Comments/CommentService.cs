using AutoMapper;
using Microsoft.Extensions.Logging;
using Sizzle.Application.Dtos;
using Sizzle.Application.Dtos.Comments;
using Sizzle.Application.Repositories;
using Sizzle.Domain.Common;
using Sizzle.Domain.Entities;

namespace Sizzle.Application.Services.Comments;

public class CommentService(ICommentRepository commentRepository, IRecipeRepository recipeRepository, IUnitOfWork unitOfWork,
    IMapper mapper, ICommentReadCache cache, ILogger<CommentService> logger) : ICommentService
{
    public async Task<Result<PageResponse<CommentDto>>> GetByRecipeIdAsync(GetCommentsByRecipeIdDto dto, CancellationToken cancellationToken = default)
    {
        var page = await cache.GetOrCreateAsync(dto.RecipeId, dto.PageIndex, dto.PageSize, async () =>
        {
            if (!await recipeRepository.Exists(cancellationToken, dto.RecipeId)) return null;

            var comments = await commentRepository.GetByRecipeIdAsync(dto.RecipeId, dto.PageIndex, dto.PageSize, cancellationToken);
            return new PageResponse<CommentDto>
            {
                Data = comments.Data.Select(comment => mapper.Map<CommentDto>(comment)).ToList(),
                TotalSize = comments.TotalSize,
                PageIndex = comments.PageIndex,
                PageSize = comments.PageSize
            };
        });
        if (page == null)
        {
            return new Result<PageResponse<CommentDto>> { Errors = [new Error { Message = "Recipe not found", Key = ErrorKey.NotFound }] };
        }

        return new Result<PageResponse<CommentDto>> { Value = page };
    }

    public async Task<Result<CommentDto>> CreateAsync(CreateCommentDto dto, CancellationToken cancellationToken = default)
    {
        var contentError = ValidateContent(dto.Content);
        if (contentError != null)
        {
            logger.LogWarning("Comment creation rejected for user {UserId} on recipe {RecipeId}: {ErrorKey}", dto.UserId, dto.RecipeId, contentError.Key);
            return new Result<CommentDto> { Errors = [contentError] };
        }

        if (!await recipeRepository.Exists(cancellationToken, dto.RecipeId))
        {
            logger.LogWarning("Comment creation rejected: recipe {RecipeId} not found", dto.RecipeId);
            return new Result<CommentDto> { Errors = [new Error { Message = "Recipe not found", Key = ErrorKey.NotFound }] };
        }

        if (dto.ParentId.HasValue)
        {
            var parent = await commentRepository.GetAsync(cancellationToken, dto.ParentId.Value);
            if (parent == null)
            {
                logger.LogWarning("Comment creation rejected: parent {ParentId} not found for recipe {RecipeId}", dto.ParentId, dto.RecipeId);
                return new Result<CommentDto> { Errors = [new Error { Message = "Parent comment not found", Key = ErrorKey.NotFound }] };
            }

            if (parent.RecipeId != dto.RecipeId)
            {
                logger.LogWarning("Comment creation rejected: parent {ParentId} belongs to another recipe", dto.ParentId);
                return new Result<CommentDto> { Errors = [new Error { Message = "Parent comment belongs to another recipe", Key = ErrorKey.BadRequest }] };
            }
        }

        var comment = mapper.Map<Comment>(dto);
        comment.Id = Guid.NewGuid();
        comment.CreatedAt = DateTime.UtcNow;

        commentRepository.Add(comment);
        await unitOfWork.CommitAsync(cancellationToken);
        logger.LogInformation("Comment {CommentId} created on recipe {RecipeId} by user {UserId}", comment.Id, dto.RecipeId, dto.UserId);
        var userName = await commentRepository.GetUserNameAsync(dto.UserId, cancellationToken);
        var created = mapper.Map<CommentDto>(comment, options => options.Items["UserName"] = userName);
        await cache.AddAsync(created);
        return new Result<CommentDto> { Value = created };
    }

    public async Task<Result<CommentDto>> UpdateAsync(Guid commentId, Guid userId, string content, CancellationToken cancellationToken = default)
    {
        var contentError = ValidateContent(content);
        if (contentError != null)
        {
            logger.LogWarning("Comment update rejected for comment {CommentId}: {ErrorKey}", commentId, contentError.Key);
            return new Result<CommentDto> { Errors = [contentError] };
        }

        var comment = await commentRepository.GetWithUserAsync(commentId, cancellationToken);
        if (comment == null)
        {
            logger.LogWarning("Comment update rejected: comment {CommentId} not found", commentId);
            return new Result<CommentDto> { Errors = [new Error { Message = "Comment not found", Key = ErrorKey.NotFound }] };
        }

        if (comment.UserId != userId)
        {
            logger.LogWarning("Comment update denied for user {UserId} on comment {CommentId}", userId, commentId);
            return new Result<CommentDto> { Errors = [new Error { Message = "Only the author can edit this comment", Key = ErrorKey.Forbidden }] };
        }

        comment.Content = content.Trim();
        await unitOfWork.CommitAsync(cancellationToken);
        logger.LogInformation("Comment {CommentId} updated by user {UserId}", commentId, userId);
        var updated = mapper.Map<CommentDto>(comment);
        await cache.UpdateAsync(updated);
        return new Result<CommentDto> { Value = updated };
    }

    public async Task<Result> DeleteAsync(Guid commentId, Guid userId, CancellationToken cancellationToken = default)
    {
        var comment = await commentRepository.GetAsync(cancellationToken, commentId);
        if (comment == null)
        {
            logger.LogWarning("Comment deletion rejected: comment {CommentId} not found", commentId);
            return new Result { Errors = [new Error { Message = "Comment not found", Key = ErrorKey.NotFound }] };
        }

        if (comment.UserId != userId)
        {
            logger.LogWarning("Comment deletion denied for user {UserId} on comment {CommentId}", userId, commentId);
            return new Result { Errors = [new Error { Message = "Only the author can delete this comment", Key = ErrorKey.Forbidden }] };
        }

        commentRepository.Remove(comment);
        await unitOfWork.CommitAsync(cancellationToken);
        logger.LogInformation("Comment {CommentId} deleted by user {UserId}", commentId, userId);
        await cache.InvalidateAsync(comment.RecipeId);
        return new Result();
    }

    private static Error? ValidateContent(string content)
    {
        return string.IsNullOrWhiteSpace(content) || content.Length > 2000
            ? new Error { Message = "Content must contain 1 to 2000 characters", Key = ErrorKey.BadRequest }
            : null;
    }

}