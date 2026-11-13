using Sizzle.Application.Dtos.Recipes;

namespace Sizzle.Application.Services.Recipes;

public interface IAIVideoStorage
{
    Task<string> UploadAsync(RecipeMediaUploadDto video, string mimeType, CancellationToken cancellationToken);

    Task DeleteAsync(string fileUri, CancellationToken cancellationToken);
}