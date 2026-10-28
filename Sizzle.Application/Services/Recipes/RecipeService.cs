using Sizzle.Application.Dtos;
using Sizzle.Application.Dtos.Recipes;
using Sizzle.Application.Repositories;
using Sizzle.Application.Services;
using Sizzle.Domain.Common;
using Sizzle.Domain.Entities;

namespace Sizzle.Application.Services.Recipes;

public class RecipeService(IRecipeRepository recipeRepository, IIngredientRepository ingredientRepository, IUnitOfWork unitOfWork, IFileService fileService) : IRecipeService
{
    public async Task<PageResponse<RecipeDto>> GetRecipesAsync(GetRecipesDto dto)
    {
        var page = await recipeRepository.GetRecipesAsync(dto);

        return new PageResponse<RecipeDto>
        {
            Data = page.Data.Select(recipe => new RecipeDto
            {
                Id = recipe.Id,
                Name = recipe.Name,
                Description = recipe.Description,
                FileUrl = recipe.FilePath == null ? null : fileService.GetFileUrl(recipe.FilePath),
                ViewCount = recipe.ViewCount,
                CreationDate = recipe.CreationDate,
                IsLiked = dto.ApplicationUserId.HasValue && recipe.ApplicationUserRecipes.Any(link => link.IsLiked)
            }).ToList(),
            TotalSize = page.TotalSize,
            PageIndex = page.PageIndex,
            PageSize = page.PageSize
        };
    }

    public async Task<Result<RecipeDto>> CreateRecipeAsync(CreateRecipeDto dto)
    {
        var mediaError = ValidateMedia(dto.File, dto.Steps);
        if (mediaError != null)
        {
            return new Result<RecipeDto> { Errors = [mediaError] };
        }

        var ingredients = await ResolveIngredientIdsAsync(dto.IngredientIds, dto.NewIngredientNames);
        if (!ingredients.IsSuccess)
        {
            return new Result<RecipeDto> { Errors = ingredients.Errors };
        }

        var recipe = new Recipe
        {
            Id = Guid.NewGuid(),
            ApplicationUserId = dto.ApplicationUserId,
            Name = dto.Name,
            Description = dto.Description,
            CreationDate = DateTime.UtcNow,
            ViewCount = 0,
            RecipeSteps = dto.Steps.Select((step, index) => new RecipeStep
            {
                Id = Guid.NewGuid(),
                Name = step.Name,
                Description = step.Description,
                Position = index
            }).ToList(),
            RecipeIngredients = ingredients.Value!.Select(id => new RecipeIngredient { IngredientId = id }).ToList()
        };

        var savedPaths = new List<string>();
        try
        {
            if (dto.File != null)
            {
                recipe.FilePath = await SaveMediaAsync(dto.File, recipe.Id, savedPaths);
            }

            await SaveStepMediaAsync(recipe.RecipeSteps, dto.Steps, recipe.Id, savedPaths);
            recipeRepository.Add(recipe);
            await unitOfWork.CommitAsync();
        }
        catch
        {
            foreach (var path in savedPaths)
            {
                fileService.DeleteFile(path);
            }

            throw;
        }

        return new Result<RecipeDto>
        {
            Value = new RecipeDto
            {
                Id = recipe.Id,
                Name = recipe.Name,
                Description = recipe.Description,
                FileUrl = recipe.FilePath == null ? null : fileService.GetFileUrl(recipe.FilePath),
                ViewCount = recipe.ViewCount,
                CreationDate = recipe.CreationDate
            }
        };
    }

    public async Task<Result<RecipeDetailsDto>> GetRecipeByIdAsync(GetRecipeByIdDto dto)
    {
        var recipe = await recipeRepository.GetWithFullDetailsAsync(dto.Id, dto.ApplicationUserId);
        if (recipe == null)
        {
            return new Result<RecipeDetailsDto>
            {
                Errors = [new Error { Message = "Recipe not found", Key = ErrorKey.NotFound }]
            };
        }

        return new Result<RecipeDetailsDto>
        {
            Value = new RecipeDetailsDto
            {
                Id = recipe.Id,
                Name = recipe.Name,
                Description = recipe.Description,
                FileUrl = recipe.FilePath == null ? null : fileService.GetFileUrl(recipe.FilePath),
                ViewCount = recipe.ViewCount,
                CreationDate = recipe.CreationDate,
                IsLiked = dto.ApplicationUserId.HasValue && recipe.ApplicationUserRecipes.Any(link => link.IsLiked),
                Steps = recipe.RecipeSteps.OrderBy(step => step.Position).Select(step => new RecipeStepDto
                {
                    Id = step.Id,
                    Name = step.Name,
                    Description = step.Description,
                    FileUrls = step.Files.Select(file => fileService.GetFileUrl(file.FilePath))
                        .Where(url => url != null).Select(url => url!).ToList()
                }).ToList(),
                Ingredients = recipe.RecipeIngredients.Select(link => new RecipeIngredientDto
                {
                    Id = link.IngredientId,
                    Name = link.Ingredient.Translations.FirstOrDefault(t => t.LanguageCode == dto.LanguageCode)?.Name
                        ?? link.Ingredient.Translations.FirstOrDefault(t => t.LanguageCode == "en")?.Name
                        ?? string.Empty
                }).ToList()
            }
        };
    }

    public async Task<Result<RecipeDto>> UpdateRecipeAsync(UpdateRecipeDto dto)
    {
        var recipe = await recipeRepository.GetWithDetailsAsync(dto.Id);
        if (recipe == null)
        {
            return new Result<RecipeDto>
            {
                Errors = [new Error { Message = "Recipe not found", Key = ErrorKey.NotFound }]
            };
        }

        if (recipe.ApplicationUserId != dto.ApplicationUserId)
        {
            return new Result<RecipeDto>
            {
                Errors = [new Error { Message = "Recipe does not belong to current user", Key = ErrorKey.BadRequest }]
            };
        }

        var mediaError = ValidateMedia(dto.File, dto.Steps);
        if (mediaError != null)
        {
            return new Result<RecipeDto> { Errors = [mediaError] };
        }

        var ingredients = await ResolveIngredientIdsAsync(dto.IngredientIds, dto.NewIngredientNames);
        if (!ingredients.IsSuccess)
        {
            return new Result<RecipeDto> { Errors = ingredients.Errors };
        }

        recipe.Name = dto.Name;
        recipe.Description = dto.Description;

        var oldPaths = recipe.RecipeSteps.SelectMany(step => step.Files).Select(file => file.FilePath).ToList();
        recipeRepository.RemoveSteps(recipe.RecipeSteps.ToList());
        recipe.RecipeSteps.Clear();
        var newSteps = dto.Steps.Select((step, index) => new RecipeStep
        {
            Id = Guid.NewGuid(),
            RecipeId = recipe.Id,
            Name = step.Name,
            Description = step.Description,
            Position = index
        }).ToList();

        var selectedIds = ingredients.Value!.ToHashSet();
        var removedIngredients = recipe.RecipeIngredients.Where(link => !selectedIds.Contains(link.IngredientId)).ToList();
        recipeRepository.RemoveIngredients(removedIngredients);
        foreach (var link in removedIngredients)
        {
            recipe.RecipeIngredients.Remove(link);
        }

        var currentIds = recipe.RecipeIngredients.Select(link => link.IngredientId).ToHashSet();
        foreach (var ingredientId in selectedIds.Except(currentIds))
        {
            recipe.RecipeIngredients.Add(new RecipeIngredient { IngredientId = ingredientId });
        }

        var savedPaths = new List<string>();
        try
        {
            if (dto.File != null)
            {
                if (recipe.FilePath != null)
                {
                    oldPaths.Add(recipe.FilePath);
                }

                recipe.FilePath = await SaveMediaAsync(dto.File, recipe.Id, savedPaths);
            }

            await SaveStepMediaAsync(newSteps, dto.Steps, recipe.Id, savedPaths);
            recipeRepository.AddSteps(newSteps);
            await unitOfWork.CommitAsync();
        }
        catch
        {
            foreach (var path in savedPaths)
            {
                fileService.DeleteFile(path);
            }

            throw;
        }

        foreach (var path in oldPaths)
        {
            fileService.DeleteFile(path);
        }

        return new Result<RecipeDto>
        {
            Value = new RecipeDto
            {
                Id = recipe.Id,
                Name = recipe.Name,
                Description = recipe.Description,
                FileUrl = recipe.FilePath == null ? null : fileService.GetFileUrl(recipe.FilePath),
                ViewCount = recipe.ViewCount,
                CreationDate = recipe.CreationDate
            }
        };
    }

    public async Task<Result> DeleteRecipeAsync(Guid recipeId, Guid applicationUserId)
    {
        var recipe = await recipeRepository.GetWithDetailsAsync(recipeId);
        if (recipe == null)
        {
            return new Result
            {
                Errors = [new Error { Message = "Recipe not found", Key = ErrorKey.NotFound }]
            };
        }

        if (recipe.ApplicationUserId != applicationUserId)
        {
            return new Result
            {
                Errors = [new Error { Message = "Recipe does not belong to current user", Key = ErrorKey.BadRequest }]
            };
        }

        var paths = recipe.RecipeSteps.SelectMany(step => step.Files).Select(file => file.FilePath).ToList();
        if (recipe.FilePath != null)
        {
            paths.Add(recipe.FilePath);
        }

        recipeRepository.Remove(recipe);
        await unitOfWork.CommitAsync();

        foreach (var path in paths)
        {
            fileService.DeleteFile(path);
        }

        return new Result();
    }

    public async Task<Result<RecipeLikeDto>> ToggleLikeAsync(ToggleLikeRecipeDto dto)
    {
        var recipe = await recipeRepository.GetAsync(dto.RecipeId);
        if (recipe == null)
        {
            return new Result<RecipeLikeDto>
            {
                Errors = [new Error { Message = "Recipe not found", Key = ErrorKey.NotFound }]
            };
        }

        var userRecipe = await recipeRepository.GetUserRecipeAsync(dto.ApplicationUserId, dto.RecipeId);
        if (userRecipe == null)
        {
            userRecipe = new ApplicationUserRecipe
            {
                ApplicationUserId = dto.ApplicationUserId,
                RecipeId = dto.RecipeId,
                IsLiked = true,
                ViewUpdatedAt = null
            };
            recipeRepository.AddUserRecipe(userRecipe);
        }
        else
        {
            userRecipe.IsLiked = !userRecipe.IsLiked;
        }

        await unitOfWork.CommitAsync();

        return new Result<RecipeLikeDto>
        {
            Value = new RecipeLikeDto
            {
                RecipeId = dto.RecipeId,
                IsLiked = userRecipe.IsLiked
            }
        };
    }

    public async Task<Result<RecipeDto>> UpdateViewAsync(UpdateRecipeViewDto dto)
    {
        var recipe = await recipeRepository.GetAsync(dto.RecipeId);
        if (recipe == null)
        {
            return new Result<RecipeDto>
            {
                Errors = [new Error { Message = "Recipe not found", Key = ErrorKey.NotFound }]
            };
        }

        var userRecipe = await recipeRepository.GetUserRecipeAsync(dto.ApplicationUserId, dto.RecipeId);
        var now = DateTime.UtcNow;

        if (userRecipe == null)
        {
            userRecipe = new ApplicationUserRecipe
            {
                ApplicationUserId = dto.ApplicationUserId,
                RecipeId = dto.RecipeId,
                IsLiked = false,
                ViewUpdatedAt = now
            };
            recipeRepository.AddUserRecipe(userRecipe);
            recipe.ViewCount += 1;
            await unitOfWork.CommitAsync();
        }
        else
        {
            var canUpdateView = !userRecipe.ViewUpdatedAt.HasValue
                || now >= userRecipe.ViewUpdatedAt.Value.AddMinutes(dto.ViewUpdateIntervalMinutes);

            if (canUpdateView)
            {
                recipe.ViewCount += 1;
                userRecipe.ViewUpdatedAt = now;
                await unitOfWork.CommitAsync();
            }
        }

        return new Result<RecipeDto>
        {
            Value = new RecipeDto
            {
                Id = recipe.Id,
                Name = recipe.Name,
                Description = recipe.Description,
                FileUrl = recipe.FilePath == null ? null : fileService.GetFileUrl(recipe.FilePath),
                ViewCount = recipe.ViewCount,
                CreationDate = recipe.CreationDate
            }
        };
    }

    private Error? ValidateMedia(RecipeMediaUploadDto? file, RecipeStepInputDto[] steps)
    {
        var files = steps.SelectMany(step => step.Files);
        if (file != null)
        {
            files = files.Append(file);
        }

        return files.Any(upload => !fileService.IsValidMedia(upload.FileName, upload.Length))
            ? new Error { Message = "Unsupported media type or file size", Key = ErrorKey.BadRequest }
            : null;
    }

    private async Task<string> SaveMediaAsync(RecipeMediaUploadDto upload, Guid recipeId, List<string> savedPaths)
    {
        var path = Path.Combine("recipes", recipeId.ToString("N"), $"{Guid.NewGuid():N}{Path.GetExtension(upload.FileName).ToLowerInvariant()}");
        savedPaths.Add(path);
        await using var stream = upload.OpenReadStream();
        await fileService.SaveFile(stream, path);
        return path;
    }

    private async Task SaveStepMediaAsync(IEnumerable<RecipeStep> steps, RecipeStepInputDto[] inputs, Guid recipeId, List<string> savedPaths)
    {
        foreach (var (step, input) in steps.Zip(inputs))
        {
            foreach (var upload in input.Files)
            {
                step.Files.Add(new RecipeStepFile
                {
                    Id = Guid.NewGuid(),
                    FilePath = await SaveMediaAsync(upload, recipeId, savedPaths)
                });
            }
        }
    }

    private async Task<Result<Guid[]>> ResolveIngredientIdsAsync(Guid[] ingredientIds, string[] newIngredientNames)
    {
        var ids = ingredientIds.Distinct().ToHashSet();
        if (ids.Contains(Guid.Empty))
        {
            return new Result<Guid[]> { Errors = [new Error { Message = "Invalid ingredient id", Key = ErrorKey.BadRequest }] };
        }

        var existing = await ingredientRepository.GetByIdsAsync(ids.ToArray());
        if (existing.Count != ids.Count)
        {
            return new Result<Guid[]> { Errors = [new Error { Message = "Ingredient not found", Key = ErrorKey.NotFound }] };
        }

        if (newIngredientNames.Any(string.IsNullOrWhiteSpace))
        {
            return new Result<Guid[]> { Errors = [new Error { Message = "Ingredient name cannot be empty", Key = ErrorKey.BadRequest }] };
        }

        var names = newIngredientNames.Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (names.Length == 0)
        {
            return new Result<Guid[]> { Value = ids.ToArray() };
        }

        var matched = await ingredientRepository.GetByNamesAsync(names.Select(name => name.ToLowerInvariant()).ToArray());
        foreach (var name in names)
        {
            var ingredient = matched.FirstOrDefault(i => i.Translations.Any(t =>
                string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)));

            if (ingredient == null)
            {
                ingredient = new Ingredient
                {
                    Id = Guid.NewGuid(),
                    Translations = [new IngredientTranslation { LanguageCode = "en", Name = name }]
                };
                ingredientRepository.Add(ingredient);
                matched.Add(ingredient);
            }

            ids.Add(ingredient.Id);
        }

        return new Result<Guid[]> { Value = ids.ToArray() };
    }
}
