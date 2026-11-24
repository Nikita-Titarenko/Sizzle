using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sizzle.Application.Services.Monetization;

namespace Sizzle.Infrastructure.Services;

public sealed class AdEarningsPayoutWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<AdEarningsPayoutWorker> logger) : BackgroundService
{
    private const int MonthlyRunDay = 1;
    private const int MonthlyRunHourUtc = 12;
    private const int MonthlyRunMinute = 0;
    private static readonly DateTime InitialProcessedMonthUtc = DateTime.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var processedMonthUtc = InitialProcessedMonthUtc;

        while (!stoppingToken.IsCancellationRequested)
        {
            var nowUtc = DateTime.UtcNow;
            var currentMonthRunUtc = new DateTime(nowUtc.Year, nowUtc.Month, MonthlyRunDay, MonthlyRunHourUtc,
                MonthlyRunMinute, 0, DateTimeKind.Utc);

            if (nowUtc < currentMonthRunUtc || processedMonthUtc == currentMonthRunUtc)
            {
                var nextRunUtc = nowUtc < currentMonthRunUtc ? currentMonthRunUtc : currentMonthRunUtc.AddMonths(1);
                var delay = nextRunUtc - nowUtc;
                await Task.Delay(delay, stoppingToken);
                continue;
            }

            try
            {
                processedMonthUtc = currentMonthRunUtc;
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IMonetizationService>();
                await service.ProcessAdEarningsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Ad earnings payout worker failed");
            }
        }
    }
}
