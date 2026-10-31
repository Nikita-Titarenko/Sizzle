using System.Data;
using Microsoft.EntityFrameworkCore;
using Sizzle.Application.Repositories;
using Sizzle.Domain.Entities;

namespace Sizzle.Infrastructure.Repositories;

public sealed class SubscriptionOrderRepository(ApplicationDbContext dbContext) : ISubscriptionOrderRepository
{
    public async Task AddAsync(SubscriptionOrder order)
    {
        await dbContext.SubscriptionOrders.AddAsync(order);
        await dbContext.SaveChangesAsync();
    }

    public Task<SubscriptionOrder?> GetAsync(Guid orderId)
    {
        return dbContext.SubscriptionOrders.AsNoTracking().FirstOrDefaultAsync(order => order.Id == orderId);
    }

    public async Task<IReadOnlyList<SubscriptionOrder>> GetPaidByUserAsync(Guid userId)
    {
        return await dbContext.SubscriptionOrders.AsNoTracking()
            .Where(order => order.UserId == userId && order.IsPaid)
            .OrderByDescending(order => order.StartsAt)
            .ToListAsync();
    }

    public async Task<SubscriptionOrder?> ActivateAsync(Guid orderId, DateTime now)
    {
        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable)
            : null;

        var order = await dbContext.SubscriptionOrders.FirstOrDefaultAsync(item => item.Id == orderId);
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
        await dbContext.SaveChangesAsync();
        if (transaction != null)
        {
            await transaction.CommitAsync();
        }

        return order;
    }
}