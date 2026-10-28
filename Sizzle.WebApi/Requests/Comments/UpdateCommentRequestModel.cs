using System.ComponentModel.DataAnnotations;

namespace Sizzle.WebApi.Requests.Comments;

public class UpdateCommentRequestModel
{
    [Required]
    [StringLength(2000, MinimumLength = 1)]
    public required string Content { get; init; }
}