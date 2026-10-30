using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sizzle.Application.Dtos;
using Sizzle.Application.Dtos.Comments;
using Sizzle.Application.Services.Comments;
using Sizzle.Domain.Common;
using Sizzle.WebApi.Requests.Base;
using Sizzle.WebApi.Requests.Comments;

namespace Sizzle.WebApi.Controllers;

[Route("api/recipes/{recipeId:guid}/comments")]
public class CommentsController(ICommentService commentService) : BaseController
{
    /// <summary>Get a page of comments for a recipe.</summary>
    /// <remarks>
    /// <para>ErrorKey.NotFound — the recipe does not exist.</para>
    /// <para>ErrorKey.BadRequest — invalid pagination parameters.</para>
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(PageResponse<CommentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PageResponse<CommentDto>>> GetByRecipeId(Guid recipeId, [FromQuery] PaginationRequestModel request)
    {
        var result = await commentService.GetByRecipeIdAsync(new GetCommentsByRecipeIdDto
        {
            RecipeId = recipeId,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize
        });
        return !result.IsSuccess ? HandleErrors(result) : Ok(result.Value);
    }

    /// <summary>Add a comment or reply to a comment.</summary>
    /// <remarks>
    /// <para>ErrorKey.BadRequest — the content is empty or too long, the parent comment belongs to another recipe, or the request is invalid.</para>
    /// <para>ErrorKey.NotFound — the recipe or parent comment does not exist.</para>
    /// <para>ErrorKey.Incorrect — missing or invalid JWT (401).</para>
    /// </remarks>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(CommentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CommentDto>> Create(Guid recipeId, CreateCommentRequestModel request)
    {
        var result = await commentService.CreateAsync(new CreateCommentDto
        {
            RecipeId = recipeId,
            ParentId = request.ParentId,
            Content = request.Content,
            UserId = GetUserId()
        });

        return !result.IsSuccess ? HandleErrors(result) : StatusCode(StatusCodes.Status201Created, result.Value);
    }

    /// <summary>Update the current user's comment.</summary>
    /// <remarks>
    /// <para>ErrorKey.BadRequest — the content is empty or too long, or the request is invalid.</para>
    /// <para>ErrorKey.NotFound — the comment does not exist.</para>
    /// <para>ErrorKey.Forbidden — another user owns the comment.</para>
    /// <para>ErrorKey.Incorrect — missing or invalid JWT (401).</para>
    /// </remarks>
    [HttpPut("~/api/comments/{commentId:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(CommentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CommentDto>> Update(Guid commentId, UpdateCommentRequestModel request)
    {
        var result = await commentService.UpdateAsync(commentId, GetUserId(), request.Content);
        return !result.IsSuccess ? HandleErrors(result) : Ok(result.Value);
    }

    /// <summary>Delete the current user's comment.</summary>
    /// <remarks>
    /// <para>ErrorKey.NotFound — the comment does not exist.</para>
    /// <para>ErrorKey.Forbidden — another user owns the comment.</para>
    /// <para>ErrorKey.Incorrect — missing or invalid JWT (401).</para>
    /// </remarks>
    [HttpDelete("~/api/comments/{commentId:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(List<Error>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(Guid commentId)
    {
        var result = await commentService.DeleteAsync(commentId, GetUserId());
        return !result.IsSuccess ? HandleErrors(result) : NoContent();
    }
}