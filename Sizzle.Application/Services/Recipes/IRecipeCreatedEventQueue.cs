using Sizzle.Application.Dtos;

namespace Sizzle.Application.Services.Recipes;

public interface IRecipeCreatedEventQueue
{
    ValueTask EnqueueAsync(RecipeCreatedEventDto item, CancellationToken cancellationToken = default);
    IAsyncEnumerable<RecipeCreatedEventDto> DequeueAllAsync(CancellationToken cancellationToken = default);
}
