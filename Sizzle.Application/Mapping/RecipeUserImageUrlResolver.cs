using AutoMapper;
using Sizzle.Application.Dtos.Recipes;
using Sizzle.Application.Services;
using Sizzle.Domain.Entities;

namespace Sizzle.Application.Mapping;

public class RecipeUserImageUrlResolver(IFileService fileService) : IValueResolver<Recipe, RecipeDto, string?>
{
    public string? Resolve(Recipe source, RecipeDto destination, string? member, ResolutionContext context) =>
        fileService.GetFileUrl(Path.Combine("icons", source.ApplicationUserId.ToString()));
}