namespace Sizzle.Domain.Entities;

public class RecipeStepFile
{
    public Guid Id { get; set; }

    public Guid RecipeStepId { get; set; }

    public RecipeStep RecipeStep { get; set; } = null!;

    public string FilePath { get; set; } = string.Empty;
}