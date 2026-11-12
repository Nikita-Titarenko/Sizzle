using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Sizzle.Application.Repositories;
using Sizzle.Domain.Entities;

namespace Sizzle.Infrastructure.Repositories;

public sealed class SubscriptionOrderRepository(ApplicationDbContext dbContext, ILogger<SubscriptionOrderRepository>? logger = null) : ISubscriptionOrderRepository
{
    private readonly ILogger<SubscriptionOrderRepository> logger = logger ?? NullLogger<SubscriptionOrderRepository>.Instance;

    public async Task AddAsync(SubscriptionOrder order, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("db.subscription_orders.add");
        await dbContext.SubscriptionOrders.AddAsync(order, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<SubscriptionOrder?> GetAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("db.subscription_orders.get");
        return dbContext.SubscriptionOrders.AsNoTracking().FirstOrDefaultAsync(order => order.Id == orderId, cancellationToken);
    }

    public async Task<IReadOnlyList<SubscriptionOrder>> GetPaidByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("db.subscription_orders.get_paid_by_user");
        return await dbContext.SubscriptionOrders.AsNoTracking()
            .Where(order => order.UserId == userId && order.IsPaid)
            .OrderByDescending(order => order.StartsAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<SubscriptionOrder?> ActivateAsync(Guid orderId, DateTime now, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("db.subscription_orders.activate");
        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;

        var order = await dbContext.SubscriptionOrders.FirstOrDefaultAsync(item => item.Id == orderId, cancellationToken);
        if (order == null || order.IsPaid)
        {
            return null;
        }

        order.StartsAt = now;
        order.ExpiresAt = order.Plan switch
        {
            SubscriptionPlan.Monthly => now.AddMonths(1),
            SubscriptionPlan.HalfYearly => now.AddMonths(6),
            SubscriptionPlan.Yearly => now.AddYears(1),
            _ => throw new InvalidOperationException("Unknown subscription plan")
        };
        order.IsPaid = true;
        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction != null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return order;
    }
}