using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sizzle.Application.Dtos.Comments;
using Sizzle.Application.Services.Comments;
using Sizzle.WebApi.Requests.Comments;

namespace Sizzle.WebApi.Controllers;

[Route("api/[controller]")]
public class CommentsController(ICommentService commentService) : BaseController
{
    [HttpGet("recipe/{recipeId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<CommentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CommentDto>>> GetByRecipeId(Guid recipeId)
    {
        var result = await commentService.GetByRecipeIdAsync(recipeId);
        return !result.IsSuccess ? HandleErrors(result) : Ok(result.Value);
    }

    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(CommentDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<CommentDto>> Create(CreateCommentRequestModel request)
    {
        var result = await commentService.CreateAsync(new CreateCommentDto
        {
            RecipeId = request.RecipeId,
            ParentId = request.ParentId,
            Content = request.Content,
            UserId = GetUserId()
        });

        return !result.IsSuccess ? HandleErrors(result) : StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPut("{commentId:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(CommentDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CommentDto>> Update(Guid commentId, UpdateCommentRequestModel request)
    {
        var result = await commentService.UpdateAsync(commentId, GetUserId(), request.Content);
        return !result.IsSuccess ? HandleErrors(result) : Ok(result.Value);
    }

    [HttpDelete("{commentId:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Delete(Guid commentId)
    {
        var result = await commentService.DeleteAsync(commentId, GetUserId());
        return !result.IsSuccess ? HandleErrors(result) : NoContent();
    }
}