using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sizzle.Application.Dtos;
using Sizzle.Application.Dtos.Comments;
using Sizzle.Application.Services.Comments;
using Sizzle.WebApi.Requests.Base;
using Sizzle.WebApi.Requests.Comments;

namespace Sizzle.WebApi.Controllers;

[Route("api/recipes/{recipeId:guid}/comments")]
public class CommentsController(ICommentService commentService) : BaseController
{
    [HttpGet]
    [ProducesResponseType(typeof(PageResponse<CommentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PageResponse<CommentDto>>> GetByRecipeId(Guid recipeId, [FromQuery] PaginationRequestModel request)
    {
        var result = await commentService.GetByRecipeIdAsync(recipeId, request.PageIndex, request.PageSize);
        return !result.IsSuccess ? HandleErrors(result) : Ok(result.Value);
    }

    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(CommentDto), StatusCodes.Status201Created)]
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

    [HttpPut("~/api/comments/{commentId:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(CommentDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CommentDto>> Update(Guid commentId, UpdateCommentRequestModel request)
    {
        var result = await commentService.UpdateAsync(commentId, GetUserId(), request.Content);
        return !result.IsSuccess ? HandleErrors(result) : Ok(result.Value);
    }

    [HttpDelete("~/api/comments/{commentId:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Delete(Guid commentId)
    {
        var result = await commentService.DeleteAsync(commentId, GetUserId());
        return !result.IsSuccess ? HandleErrors(result) : NoContent();
    }
}