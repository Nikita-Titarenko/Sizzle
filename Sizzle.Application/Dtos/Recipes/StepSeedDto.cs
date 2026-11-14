namespace Sizzle.Application.Dtos.Recipes;

public class StepSeedDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string[] FilePaths { get; set; } = [];
}