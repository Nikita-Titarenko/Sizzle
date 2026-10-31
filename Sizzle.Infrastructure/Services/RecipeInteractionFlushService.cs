using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sizzle.Application.Options;
using Sizzle.Application.Services.Recipes;

namespace Sizzle.Infrastructure.Services;

public sealed class RecipeInteractionFlushService(IServiceScopeFactory scopeFactory, ILogger<RecipeInteractionFlushService> logger,
    IOptions<RecipeCacheOptions> options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(options.Value.InteractionFlushIntervalMinutes));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var buffer = scope.ServiceProvider.GetRequiredService<RecipeInteractionBuffer>();
                await buffer.FlushAsync(stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(exception, "Failed to flush recipe interactions. Pending entries remain in Redis.");
            }
        }
    }
}