using AutoMapper;
using Sizzle.Application.Dtos.Recipes;
using Sizzle.Application.Services;
using Sizzle.Domain.Entities;

namespace Sizzle.Application.Mapping;

public class RecipeFileUrlResolver(IFileService fileService) : IValueResolver<Recipe, RecipeDto, string?>
{
    public string? Resolve(Recipe source, RecipeDto destination, string? member, ResolutionContext context) =>
        source.FilePath == null ? null : fileService.GetFileUrl(source.FilePath);
}