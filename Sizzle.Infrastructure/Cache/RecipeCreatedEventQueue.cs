using System.Threading.Channels;
using Sizzle.Application.Dtos;
using Sizzle.Application.Services.Recipes;

namespace Sizzle.Infrastructure.Cache;

public sealed class RecipeCreatedEventQueue : IRecipeCreatedEventQueue
{
    private readonly Channel<RecipeCreatedEventDto> channel = Channel.CreateUnbounded<RecipeCreatedEventDto>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false,
        AllowSynchronousContinuations = false
    });

    public ValueTask EnqueueAsync(RecipeCreatedEventDto item, CancellationToken cancellationToken = default)
    {
        return channel.Writer.WriteAsync(item, cancellationToken);
    }

    public IAsyncEnumerable<RecipeCreatedEventDto> DequeueAllAsync(CancellationToken cancellationToken = default)
    {
        return channel.Reader.ReadAllAsync(cancellationToken);
    }
}
