using AutoMapper;
using Sizzle.Application.Dtos.Recipes;
using Sizzle.Application.Services;
using Sizzle.Domain.Entities;

namespace Sizzle.Application.Mapping;

public class RecipeDetailsStepsResolver(IFileService fileService)
    : IValueResolver<Recipe, RecipeDetailsDto, IReadOnlyList<RecipeStepDto>>
{
    public IReadOnlyList<RecipeStepDto> Resolve(Recipe source, RecipeDetailsDto destination,
        IReadOnlyList<RecipeStepDto> member, ResolutionContext context) =>
        source.RecipeSteps.OrderBy(step => step.Position).Select(step => new RecipeStepDto
        {
            Id = step.Id,
            Name = step.Name,
            Description = step.Description,
            FileUrls = step.Files.Select(file => fileService.GetFileUrl(file.FilePath))
                .Where(url => url != null).Select(url => url!).ToList()
        }).ToList();
}