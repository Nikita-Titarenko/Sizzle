namespace Sizzle.Application.Services.Recipes;

public sealed record RecipeCreatedEvent(Guid RecipeId, Guid AuthorId, DateTime CreatedAt);

public interface IRecipeCreatedEventQueue
{
    ValueTask EnqueueAsync(RecipeCreatedEvent item, CancellationToken cancellationToken = default);
    IAsyncEnumerable<RecipeCreatedEvent> DequeueAllAsync(CancellationToken cancellationToken = default);
}
