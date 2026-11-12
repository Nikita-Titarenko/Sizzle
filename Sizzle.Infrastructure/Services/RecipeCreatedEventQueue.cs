using System.Threading.Channels;
using Sizzle.Application.Services.Recipes;

namespace Sizzle.Infrastructure.Services;

public sealed class RecipeCreatedEventQueue : IRecipeCreatedEventQueue
{
    private readonly Channel<RecipeCreatedEvent> channel = Channel.CreateUnbounded<RecipeCreatedEvent>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false,
        AllowSynchronousContinuations = false
    });

    public ValueTask EnqueueAsync(RecipeCreatedEvent item, CancellationToken cancellationToken = default)
    {
        return channel.Writer.WriteAsync(item, cancellationToken);
    }

    public IAsyncEnumerable<RecipeCreatedEvent> DequeueAllAsync(CancellationToken cancellationToken = default)
    {
        return channel.Reader.ReadAllAsync(cancellationToken);
    }
}
