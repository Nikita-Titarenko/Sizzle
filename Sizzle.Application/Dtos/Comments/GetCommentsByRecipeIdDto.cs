using Sizzle.Application.Dtos.Base;

namespace Sizzle.Application.Dtos.Comments;

public class GetCommentsByRecipeIdDto : PaginationDto
{
    public Guid RecipeId { get; init; }
}
